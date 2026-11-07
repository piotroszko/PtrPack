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
    }

    private NativeWindow? _window;
    private SetIP? _setIp;
    private RouterConfiguration? _routerConfig;
    private ToggleSetting? _showOnServer;
    private ToggleSetting? _showOnRouter;

    private float _nextReferenceScan;
    private float _nextRefresh;
    private ViewMode _lastMode;
    private bool _templateWarningLogged;
    private bool _windowLogged;
    private bool _referencesLogged;

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
            "Show helper on the router configuration screen",
            true);
    }

    public override void OnGameplayEnter()
    {
        _window = null;
        _setIp = null;
        _routerConfig = null;
        _nextReferenceScan = 0f;
        _nextRefresh = 0f;
        _lastMode = ViewMode.None;
        _templateWarningLogged = false;
        _windowLogged = false;
        _referencesLogged = false;
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
                    new Vector2(560f, 560f),
                    Vector2.zero,
                    25);
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
        if (target != null)
        {
            Context.Log.Msg("Client helper: placing window.");
            _window.PlaceNextTo(target, 32f, mode == ViewMode.Server);
            Context.Log.Msg(
                $"Client helper: window at {_window.WindowRect.anchoredPosition} " +
                $"(target {target.rect.size} at {target.anchoredPosition}).");
        }

        if (mode != _lastMode || now >= _nextRefresh)
        {
            _lastMode = mode;
            _nextRefresh = now + 1f;
            Populate(mode);
        }
    }

    public override void OnGameplayExit() => CloseWindow();
    public override void OnShutdown() => CloseWindow();

    private void EnsureReferences()
    {
        if (_setIp == null)
            _setIp = UiFactory.FindSceneComponent<SetIP>();
        if (_routerConfig == null)
            _routerConfig = UiFactory.FindSceneComponent<RouterConfiguration>();

        if (!_referencesLogged && (_setIp != null || _routerConfig != null))
        {
            _referencesLogged = true;
            Context.Log.Msg($"Client helper references: SetIP={_setIp != null}, Router={_routerConfig != null}.");
        }
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

        Context.Log.Msg("Client helper: populating.");
        _window.Clear();
        _window.SetTitle(mode == ViewMode.Server
            ? "Client IP Helper"
            : "Client Subnet Helper");

        _window.AddLabel(
            mode == ViewMode.Server
                ? "Use fills the server IP field with the lowest free address."
                : "Use fills the subnet, mask and VLAN fields.",
            14f);
        Context.Log.Msg("Client helper: header added.");

        List<ClientApp> clients;
        try
        {
            clients = ClientHelperData.Snapshot();
        }
        catch (Exception exception)
        {
            Context.Log.Warning($"Client data unavailable: {exception.Message}");
            clients = new List<ClientApp>();
        }
        Context.Log.Msg($"Client helper: snapshot has {clients.Count} app(s).");
        if (clients.Count == 0)
        {
            _window.AddLabel("No signed customers yet.", 15f);
            return;
        }

        foreach (var client in clients)
        {
            Context.Log.Msg($"Client helper: adding row for {client.Title}.");
            var row = _window.AddRow(
                client.Title,
                BuildDetail(client),
                "Use",
                () => Fill(client, mode));

            if (!client.HasFreeIp)
                row.SetActionEnabled(false);
            Context.Log.Msg($"Client helper: row added for {client.Title}.");
        }
        Context.Log.Msg("Client helper: populated.");
    }

    private static string BuildDetail(ClientApp client)
    {
        string free = client.HasFreeIp ? client.LowestFreeIp : "full";
        string subnet = string.IsNullOrEmpty(client.Subnet) ? "no subnet" : client.Subnet;
        string gateway = string.IsNullOrEmpty(client.Gateway) ? "-" : client.Gateway;

        return $"VLAN {client.Vlan}  {subnet}\nGW {gateway}  low {free}  ({client.UsedIps}/{client.TotalIps})";
    }

    private void Fill(ClientApp client, ViewMode mode)
    {
        if (mode == ViewMode.Server)
        {
            if (_setIp == null || !client.HasFreeIp)
                return;

            _setIp.ipAddress = client.LowestFreeIp;
            if (_setIp.ipTextField != null)
                _setIp.ipTextField.text = client.LowestFreeIp;

            return;
        }

        if (_routerConfig == null || string.IsNullOrEmpty(client.Subnet))
            return;

        string mask = MaskFromCidr(client.Subnet);
        _routerConfig.subnet = client.Subnet;
        _routerConfig.mask = mask;
        _routerConfig.subnetVlan = client.Vlan;

        if (_routerConfig.textSubnet != null)
            _routerConfig.textSubnet.text = client.Subnet;
        if (_routerConfig.textMask != null)
            _routerConfig.textMask.text = mask;
        if (_routerConfig.textSubnetVlan != null)
            _routerConfig.textSubnetVlan.text = client.Vlan.ToString();
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
