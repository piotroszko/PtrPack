using System;
using System.Collections.Generic;
using Il2Cpp;
using PtrLib.Mod;
using PtrLib.Settings;
using PtrLib.Ui;
using UnityEngine;

namespace PtrPack.Modules.ClientHelper;

internal sealed class ClientHelperModule : PtrModule
{
    private enum ViewMode
    {
        None,
        Server,
        Router,
        Switch,
    }

    private NativeWindow? _window;
    private SetIP? _setIp;
    private RouterConfiguration? _routerConfig;
    private NetworkSwitchConfiguration? _switchConfig;
    private ToggleSetting? _showOnServer;
    private ToggleSetting? _showOnRouter;
    private ToggleSetting? _showOnSwitch;

    private float _nextReferenceScan;
    private float _nextRefresh;
    private ViewMode _lastMode;
    private ViewMode _placedMode;
    private bool _templateWarningLogged;
    private bool _windowLogged;
    private bool _referencesLogged;
    private ViewMode _renderedMode;
    private string _renderedSignature = string.Empty;

    public override string Id => "ClientHelper";
    public override string DisplayName => "Client Helper";
    public override bool EnabledByDefault => true;

    public override void OnRegisterSettings(ModSettings settings)
    {
        var section = settings.AddSection(Id, DisplayName, Context.Config.Category);
        _showOnServer = section.AddToggle(
            "ShowOnServerView",
            "Show helper on the server IP screen",
            true);
        _showOnRouter = section.AddToggle(
            "ShowOnRouterView",
            "Show route planner on the router screen",
            true);
        _showOnSwitch = section.AddToggle(
            "ShowOnSwitchView",
            "Show port audit on the switch screen",
            true);
    }

    public override void OnGameplayEnter()
    {
        _window = null;
        _setIp = null;
        _routerConfig = null;
        _switchConfig = null;
        _nextReferenceScan = 0f;
        _nextRefresh = 0f;
        _lastMode = ViewMode.None;
        _placedMode = ViewMode.None;
        _templateWarningLogged = false;
        _windowLogged = false;
        _referencesLogged = false;
        _renderedMode = ViewMode.None;
        _renderedSignature = string.Empty;
        RouterAudit.Reset();
        ClientHelperData.ResetCaches();
    }

    public override void OnUpdate()
    {
        if (!Context.InGameplay)
            return;

        float now = Time.unscaledTime;

        if (now >= _nextReferenceScan)
        {
            _nextReferenceScan = now + 0.5f;
            EnsureReferences();
        }

        ViewMode mode = DetectMode();
        if (mode == ViewMode.None)
        {
            _lastMode = ViewMode.None;
            _placedMode = ViewMode.None;
            _window?.Hide();
            return;
        }

        if (_window == null)
        {
            try
            {
                _window = Context.Windows.ShowNative(
                    "PtrPack Client Helper",
                    "Client Helper",
                    new Vector2(660f, 620f),
                    Vector2.zero,
                    25,
                    false);
            }
            catch (Exception exception)
            {
                Context.Log.Warning($"Client helper window could not be created: {exception.Message}");
                _templateWarningLogged = true;
            }

            if (_window == null && !_templateWarningLogged)
            {
                _templateWarningLogged = true;
                Context.Log.Warning("Native window template was not found; client helper is inactive.");
            }
        }

        if (_window == null)
            return;

        if (!_window.IsVisible)
        {
            Context.Log.Msg($"Client helper: showing window for {mode}.");
            _window.Show();
            Context.Log.Msg("Client helper: window shown.");
        }

        if (!_windowLogged)
        {
            _windowLogged = true;
            Context.Log.Msg($"Client helper window ready for {mode} view.");
        }

        var target = FindWindowRect(mode);
        if (target != null && mode != _placedMode)
        {
            Context.Log.Msg("Client helper: placing window.");
            _window.PlaceNextTo(target, 32f, mode == ViewMode.Server);
            _placedMode = mode;
            Context.Log.Msg(
                $"Client helper: window at {_window.WindowRect.anchoredPosition} " +
                $"(target {target.rect.size} at {target.anchoredPosition}).");
        }

        if (mode != _lastMode || now >= _nextRefresh)
        {
            _lastMode = mode;
            _nextRefresh = now + 2f;
            Populate(mode);
        }
    }

    public override void OnGameplayExit() => CloseWindow();
    public override void OnShutdown() => CloseWindow();

    private void EnsureReferences()
    {
        if (_setIp == null)
            _setIp = FindSetIp();
        if (_routerConfig == null)
            _routerConfig = UiFactory.FindSceneComponent<RouterConfiguration>();
        if (_switchConfig == null)
            _switchConfig = UiFactory.FindSceneComponent<NetworkSwitchConfiguration>();

        if (!_referencesLogged &&
            (_setIp != null || _routerConfig != null || _switchConfig != null))
        {
            _referencesLogged = true;
            Context.Log.Msg(
                $"Client helper references: SetIP={_setIp != null}, " +
                $"Router={_routerConfig != null}, Switch={_switchConfig != null}.");
        }
    }

    private static SetIP? FindSetIp()
    {
        var all = Resources.FindObjectsOfTypeAll<SetIP>();
        SetIP? fallback = null;
        for (int i = 0; i < all.Length; i++)
        {
            var candidate = all[i];
            if (candidate == null || !candidate.gameObject.scene.IsValid())
                continue;

            if (fallback == null)
                fallback = candidate;

            if (candidate.server != null &&
                candidate.canvas != null &&
                candidate.canvas.activeInHierarchy)
            {
                return candidate;
            }
        }

        return fallback;
    }

    private ViewMode DetectMode()
    {
        if (_showOnServer?.Value != false &&
            _setIp != null &&
            (_setIp.isActive || IsVisible(_setIp.canvas)))
        {
            return ViewMode.Server;
        }

        if (_showOnRouter?.Value != false &&
            _routerConfig != null &&
            IsVisible(_routerConfig.gameObject))
        {
            return ViewMode.Router;
        }

        if (_showOnSwitch?.Value != false &&
            _switchConfig != null &&
            IsVisible(_switchConfig.gameObject))
        {
            return ViewMode.Switch;
        }

        return ViewMode.None;
    }

    private static bool IsVisible(GameObject? root)
    {
        if (root == null || !root.activeInHierarchy)
            return false;

        var canvas = root.GetComponent<Canvas>();
        return canvas == null || canvas.enabled;
    }

    private RectTransform? FindWindowRect(ViewMode mode)
    {
        Transform? root = null;
        if (mode == ViewMode.Server && _setIp?.canvas != null)
            root = _setIp.canvas.transform;
        else if (mode == ViewMode.Router)
            root = _routerConfig?.transform;
        else if (mode == ViewMode.Switch)
            root = _switchConfig?.transform;

        if (root == null)
            return null;

        var window = UiFactory.FindChild(root, "bcg");
        if (window != null)
        {
            var rect = window.GetComponent<RectTransform>();
            if (rect != null)
                return rect;
        }

        return root.GetComponent<RectTransform>();
    }

    private void Populate(ViewMode mode)
    {
        if (_window == null)
            return;

        switch (mode)
        {
            case ViewMode.Server:
                PopulateServer();
                break;
            case ViewMode.Router:
                PopulateRouter();
                break;
            case ViewMode.Switch:
                PopulateSwitch();
                break;
        }
    }

    private void PopulateServer()
    {
        List<ClientApp> clients;
        try
        {
            var activeServer = _setIp?.server;
            clients = ClientHelperData.Snapshot(activeServer, null, true);
            Context.Log.Msg(
                $"Client helper: server={activeServer?.ServerID ?? "null"} " +
                $"type={activeServer?.serverType ?? -1} app={activeServer?.appID ?? -2}.");
        }
        catch (Exception exception)
        {
            Context.Log.Warning($"Client data unavailable: {exception.Message}");
            clients = new List<ClientApp>();
        }

        string signature = BuildSignature(ViewMode.Server, clients);
        if (!BeginRender(
                ViewMode.Server,
                "Client IP Helper",
                "Use fills the server IP field with the lowest free address.",
                signature))
        {
            return;
        }

        bool anyRow = false;
        foreach (var client in clients)
        {
            if (client.HasFreeIp)
            {
                _window!.AddRow(
                    $"App {client.AppId}",
                    BuildDetail(client),
                    "Use",
                    () => FillServer(client),
                    client.Logo);
                anyRow = true;
            }

            foreach (var option in client.RoutedOptions)
            {
                if (!option.HasFreeIp)
                    continue;

                string ip = option.LowestFreeIp;
                _window!.AddRow(
                    $"App {client.AppId}  [routed]",
                    option.Detail,
                    "Use",
                    () => FillServerIp(ip),
                    client.Logo);
                anyRow = true;
            }
        }

        if (!anyRow)
            _window!.AddLabel("No reachable customers have a free IP.", 15f);

        Context.Log.Msg($"Client helper: populated server view with {clients.Count} app(s).");
    }

    private void PopulateRouter()
    {
        if (_routerConfig == null)
            return;

        List<RouterAuditItem> items;
        try
        {
            items = RouterAudit.Build(_routerConfig);
        }
        catch (Exception exception)
        {
            Context.Log.Warning($"Router audit unavailable: {exception.Message}");
            items = new List<RouterAuditItem>();
        }

        var signature = new System.Text.StringBuilder("R");
        foreach (var item in items)
        {
            signature.Append('|').Append(item.Client.CustomerBaseId)
                .Append(':').Append(item.Client.AppId)
                .Append(':').Append(item.Client.Vlan)
                .Append(':').Append(item.Client.Subnet)
                .Append(':').Append(item.Status)
                .Append(':').Append(item.SourceVlan)
                .Append(':').Append(item.SourceIp);
        }

        if (!BeginRender(
                ViewMode.Router,
                "Router Route Planner",
                "Shows missing or invalid router config; Use fills the native form.",
                signature.ToString()))
        {
            return;
        }

        if (items.Count == 0)
        {
            _window!.AddLabel("No router config issues detected.", 15f);
        }
        else
        {
            foreach (var item in items)
            {
                string action = item.Action switch
                {
                    RouterAction.FillSubnet => "Subnet",
                    RouterAction.FillRouteTarget => "Target",
                    _ => "Route",
                };
                _window!.AddRow(
                    $"{item.Status}  —  App {item.Client.AppId}",
                    item.Detail,
                    action,
                    () => FillRouter(item),
                    item.Client.Logo);
            }
        }

        Context.Log.Msg($"Client helper: populated router view with {items.Count} issue(s).");
    }

    private void PopulateSwitch()
    {
        if (_switchConfig == null)
            return;

        SwitchAuditResult audit;
        try
        {
            audit = SwitchAudit.Build(_switchConfig);
        }
        catch (Exception exception)
        {
            Context.Log.Warning($"Switch audit unavailable: {exception.Message}");
            audit = new SwitchAuditResult(
                new List<SwitchPortAudit>(),
                new List<SwitchVlanGap>(),
                0,
                0);
        }

        var signature = new System.Text.StringBuilder("S")
            .Append(audit.LinkedPorts).Append(':').Append(audit.BlockedPorts);
        foreach (var port in audit.Ports)
        {
            signature.Append('|').Append(port.Index)
                .Append(':').Append(port.Remote)
                .Append(':').Append(port.ServerVlan)
                .Append(':').Append(port.VlanBlocked)
                .Append(':').Append(port.DeniedVlans);
        }
        foreach (var gap in audit.Gaps)
        {
            signature.Append('|').Append(gap.PortIndex)
                .Append(':').Append(gap.Client.Vlan)
                .Append(':').Append(gap.Client.Title);
        }

        if (!BeginRender(
                ViewMode.Switch,
                "Switch Port Audit",
                "Linked ports, server VLANs, denied VLANs, and customer VLAN gaps.",
                signature.ToString()))
        {
            return;
        }

        _window!.AddLabel(
            $"Linked ports: {audit.LinkedPorts}   blocked server VLANs: {audit.BlockedPorts}",
            15f);

        _window.AddLabel("PORTS", 15f);
        if (audit.Ports.Count == 0)
        {
            _window.AddLabel("No linked ports.", 14f);
        }
        else
        {
            foreach (var port in audit.Ports)
            {
                string vlan = port.ServerVlan >= 0 ? $"VLAN {port.ServerVlan}" : "no server VLAN";
                string denied = string.IsNullOrEmpty(port.DeniedVlans)
                    ? "-"
                    : port.DeniedVlans;
                string status = port.VlanBlocked ? "BLOCKED" : "ok";
                _window.AddRow(
                    $"Port {port.Index + 1}  {port.Remote}",
                    $"{vlan}  denied: {denied}  [{status}]",
                    "Select",
                    () => _switchConfig.ClickPort(port.Index));
            }
        }

        _window.AddLabel("CUSTOMER VLAN GAPS", 15f);
        if (audit.Gaps.Count == 0)
        {
            _window.AddLabel("No customer VLANs are denied on linked ports.", 14f);
        }
        else
        {
            foreach (var gap in audit.Gaps)
            {
                int gapPort = gap.PortIndex;
                _window.AddRow(
                    $"App {gap.Client.AppId}  —  VLAN {gap.Client.Vlan}",
                    gap.Detail,
                    gapPort >= 0 ? "Select" : string.Empty,
                    gapPort >= 0 ? () => _switchConfig.ClickPort(gapPort) : null,
                    gap.Client.Logo);
            }
        }

        Context.Log.Msg(
            $"Client helper: populated switch view with {audit.Ports.Count} port(s), " +
            $"{audit.Gaps.Count} gap(s).");
    }

    private bool BeginRender(
        ViewMode mode,
        string title,
        string subtitle,
        string signature)
    {
        if (_window == null)
            return false;
        if (mode == _renderedMode && signature == _renderedSignature)
            return false;

        Context.Log.Msg($"Client helper: populating {mode}.");
        _window.Clear();
        _window.SetTitle(title);
        _window.AddLabel(subtitle, 15f);
        _renderedMode = mode;
        _renderedSignature = signature;
        return true;
    }

    private static string BuildSignature(ViewMode mode, List<ClientApp> clients)
    {
        var text = new System.Text.StringBuilder();
        text.Append((int)mode);
        foreach (var client in clients)
        {
            text.Append('|').Append(client.CustomerBaseId)
                .Append(':').Append(client.AppId)
                .Append(':').Append(client.Subnet)
                .Append(':').Append(client.Vlan)
                .Append(':').Append(client.Gateway)
                .Append(':').Append(client.LowestFreeIp)
                .Append(':').Append(client.UsedIps)
                .Append(':').Append(client.TotalIps);
            foreach (var option in client.RoutedOptions)
            {
                text.Append('~').Append(option.RouterId)
                    .Append(':').Append(option.Subnet)
                    .Append(':').Append(option.Vlan)
                    .Append(':').Append(option.LowestFreeIp)
                    .Append(':').Append(option.UsedIps)
                    .Append(':').Append(option.TotalIps);
            }
        }
        return text.ToString();
    }

    private static string BuildDetail(ClientApp client)
    {
        string free = client.HasFreeIp ? client.LowestFreeIp : "full";
        string subnet = string.IsNullOrEmpty(client.Subnet) ? "no subnet" : client.Subnet;
        string gateway = string.IsNullOrEmpty(client.Gateway) ? "-" : client.Gateway;

        return $"VLAN {client.Vlan}  {subnet}\nGW {gateway}  low {free}  ({client.UsedIps}/{client.TotalIps})";
    }

    private void FillServer(ClientApp client)
    {
        FillServerIp(client.LowestFreeIp);
    }

    private void FillServerIp(string ip)
    {
        if (_setIp == null || string.IsNullOrEmpty(ip))
        {
            Context.Log.Warning("Client helper: Fill skipped; no SetIP or no free IP.");
            return;
        }

        _setIp.ipAddress = ip;
        if (_setIp.ipTextField != null)
            _setIp.ipTextField.text = ip;
        Context.Log.Msg($"Client helper: filled server IP {ip}.");
    }

    private void FillRouter(RouterAuditItem item)
    {
        if (_routerConfig == null)
            return;

        if (item.Action == RouterAction.FillSubnet)
        {
            FillRouterSubnet(item);
            return;
        }

        FillRouterRoute(item);
    }

    private void FillRouterSubnet(RouterAuditItem item)
    {
        if (_routerConfig == null || string.IsNullOrEmpty(item.Subnet))
            return;

        string mask = MaskFromCidr(item.Subnet);
        string subnetIp = item.Subnet;
        int slash = subnetIp.IndexOf('/');
        if (slash >= 0)
            subnetIp = subnetIp.Substring(0, slash);

        _routerConfig.subnet = subnetIp;
        _routerConfig.mask = mask;
        _routerConfig.subnetVlan = item.SourceVlan;

        if (_routerConfig.textSubnet != null)
            _routerConfig.textSubnet.text = subnetIp;
        if (_routerConfig.textMask != null)
            _routerConfig.textMask.text = mask;
        if (_routerConfig.textSubnetVlan != null)
            _routerConfig.textSubnetVlan.text = item.SourceVlan.ToString();

        Context.Log.Msg(
            $"Client helper: filled router subnet {item.Subnet}, VLAN {item.SourceVlan}.");
    }

    private void FillRouterRoute(RouterAuditItem item)
    {
        if (_routerConfig == null)
            return;

        ClientApp client = item.Client;
        if (item.Action == RouterAction.FillRoute && item.SourceVlan >= 0)
            _routerConfig.routeSourceVlan = item.SourceVlan;

        _routerConfig.routeTargetVlan = client.Vlan;
        _routerConfig.routeSourceIp = string.Empty;
        _routerConfig.routeTargetIp = string.Empty;
        if (_routerConfig.textRouteSource != null)
            _routerConfig.textRouteSource.text = item.SourceVlan.ToString();
        if (_routerConfig.textRouteTarget != null)
            _routerConfig.textRouteTarget.text = client.Vlan.ToString();

        Context.Log.Msg(
            $"Client helper: filled router route VLAN {item.SourceVlan} -> VLAN {client.Vlan}.");
    }

    private static string MaskFromCidr(string subnet)
    {
        int slash = subnet.IndexOf('/');
        if (slash < 0)
            return subnet;

        if (!int.TryParse(subnet.Substring(slash + 1), out int prefix) || prefix < 0 || prefix > 32)
            return "255.255.255.0";

        uint mask = prefix == 0 ? 0u : 0xFFFFFFFFu << (32 - prefix);
        return $"{(mask >> 24) & 0xFF}.{(mask >> 16) & 0xFF}.{(mask >> 8) & 0xFF}.{mask & 0xFF}";
    }

    private void CloseWindow()
    {
        if (_window == null)
            return;

        Context.Windows.Close(_window);
        _window = null;
    }
}
