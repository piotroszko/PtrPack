using System;
using System.Collections.Generic;
using Il2Cpp;
using PtrLib.Mod;
using PtrLib.Ui;
using UnityEngine;

namespace PtrPack.Modules.ClientHelper;

internal sealed partial class ClientHelperModule : PtrModule
{
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

}
