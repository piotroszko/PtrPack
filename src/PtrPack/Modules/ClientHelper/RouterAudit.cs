using System;
using System.Collections.Generic;
using Il2Cpp;

namespace PtrPack.Modules.ClientHelper;

internal enum RouterAction
{
    None,
    FillSubnet,
    FillRoute,
    FillRouteTarget,
}

internal sealed class RouterAuditItem
{
    internal RouterAuditItem(
        ClientApp client,
        RouterAction action,
        string status,
        string detail,
        int sourceVlan,
        string sourceIp)
    {
        Client = client;
        Action = action;
        Status = status;
        Detail = detail;
        SourceVlan = sourceVlan;
        SourceIp = sourceIp;
    }

    internal ClientApp Client { get; }
    internal RouterAction Action { get; }
    internal string Status { get; }
    internal string Detail { get; }
    internal int SourceVlan { get; }
    internal string SourceIp { get; }
}

internal static class RouterAudit
{
    internal static List<RouterAuditItem> Build(RouterConfiguration config)
    {
        var result = new List<RouterAuditItem>();
        Router? router = config.router;
        if (router == null)
            return result;

        var clients = ClientHelperData.Snapshot(null, router.switchId);
        var owned = new List<Router.OwnedSubnet>();
        if (router.ownedSubnets != null)
        {
            for (int i = 0; i < router.ownedSubnets.Count; i++)
            {
                var subnet = router.ownedSubnets[i];
                if (subnet != null)
                    owned.Add(subnet);
            }
        }

        var routes = new List<Router.RouteEntry>();
        if (router.routes != null)
        {
            for (int i = 0; i < router.routes.Count; i++)
            {
                var route = router.routes[i];
                if (route != null)
                    routes.Add(route);
            }
        }

        var map = NetworkMap.instance;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var client in clients)
        {
            if (string.IsNullOrEmpty(client.Subnet))
                continue;

            string key = $"{client.Vlan}:{client.Subnet}";
            if (!seen.Add(key))
                continue;

            Router.OwnedSubnet? ownedSubnet = FindOwned(owned, client);
            if (ownedSubnet == null)
            {
                result.Add(new RouterAuditItem(
                    client,
                    RouterAction.FillSubnet,
                    "Missing subnet",
                    $"VLAN {client.Vlan}  {client.Subnet}",
                    -1,
                    string.Empty));
                continue;
            }

            if (ownedSubnet.vlanId != client.Vlan)
            {
                result.Add(new RouterAuditItem(
                    client,
                    RouterAction.FillSubnet,
                    "Wrong VLAN",
                    $"subnet has VLAN {ownedSubnet.vlanId}, client needs {client.Vlan}",
                    -1,
                    string.Empty));
                continue;
            }

            Router.RouteEntry? route = FindRoute(routes, client);
            if (route == null)
            {
                Router.OwnedSubnet? source = PickSource(owned, client);
                string sourceIp = source == null
                    ? string.Empty
                    : map?.GetGatewayForSubnet(source.subnetCidr) ?? string.Empty;
                string detail = source == null
                    ? $"target VLAN {client.Vlan}  {client.Gateway}"
                    : $"source VLAN {source.vlanId}  {sourceIp}  ->  target VLAN {client.Vlan}  {client.Gateway}";
                result.Add(new RouterAuditItem(
                    client,
                    source == null ? RouterAction.FillRouteTarget : RouterAction.FillRoute,
                    "Missing route",
                    detail,
                    source?.vlanId ?? -1,
                    sourceIp));
                continue;
            }

            if (!IsSourceValid(route, owned))
            {
                result.Add(new RouterAuditItem(
                    client,
                    RouterAction.FillRoute,
                    "Invalid route source",
                    $"route source VLAN {route.sourceVlanId} is not owned by this router",
                    -1,
                    string.Empty));
            }
        }

        return result;
    }

    private static Router.OwnedSubnet? FindOwned(List<Router.OwnedSubnet> owned, ClientApp client)
    {
        for (int i = 0; i < owned.Count; i++)
        {
            if (string.Equals(owned[i].subnetCidr, client.Subnet, StringComparison.OrdinalIgnoreCase))
                return owned[i];
        }

        return null;
    }

    private static Router.RouteEntry? FindRoute(List<Router.RouteEntry> routes, ClientApp client)
    {
        for (int i = 0; i < routes.Count; i++)
        {
            var route = routes[i];
            if (route.targetVlanId != client.Vlan)
                continue;

            if (string.IsNullOrEmpty(route.targetIp) ||
                string.IsNullOrEmpty(client.Gateway) ||
                string.Equals(route.targetIp, client.Gateway, StringComparison.OrdinalIgnoreCase) ||
                IsInSubnet(route.targetIp, client.Subnet))
            {
                return route;
            }
        }

        return null;
    }

    private static bool IsSourceValid(Router.RouteEntry route, List<Router.OwnedSubnet> owned)
    {
        if (string.IsNullOrEmpty(route.sourceIp))
            return false;

        for (int i = 0; i < owned.Count; i++)
        {
            if (owned[i].vlanId == route.sourceVlanId)
                return true;
        }

        return false;
    }

    private static Router.OwnedSubnet? PickSource(List<Router.OwnedSubnet> owned, ClientApp client)
    {
        Router.OwnedSubnet? fallback = null;
        for (int i = 0; i < owned.Count; i++)
        {
            var candidate = owned[i];
            if (candidate == null ||
                candidate.vlanId == client.Vlan ||
                string.IsNullOrEmpty(candidate.subnetCidr))
            {
                continue;
            }

            if (fallback == null)
                fallback = candidate;
        }

        return fallback;
    }

    private static bool IsInSubnet(string ip, string cidr)
    {
        int slash = cidr.IndexOf('/');
        if (slash < 0 || string.IsNullOrEmpty(ip))
            return false;

        if (!TryParseIp(ip, out uint address) ||
            !TryParseIp(cidr.Substring(0, slash), out uint network) ||
            !int.TryParse(cidr.Substring(slash + 1), out int prefix) ||
            prefix < 0 || prefix > 32)
        {
            return false;
        }

        uint mask = prefix == 0 ? 0u : 0xFFFFFFFFu << (32 - prefix);
        return (address & mask) == (network & mask);
    }

    private static bool TryParseIp(string text, out uint value)
    {
        value = 0;
        if (string.IsNullOrEmpty(text))
            return false;

        string[] parts = text.Split('.');
        if (parts.Length != 4)
            return false;

        for (int i = 0; i < parts.Length; i++)
        {
            if (!byte.TryParse(parts[i], out byte part))
                return false;

            value = (value << 8) | part;
        }

        return true;
    }
}
