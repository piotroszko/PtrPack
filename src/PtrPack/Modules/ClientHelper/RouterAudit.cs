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
        string subnet,
        int sourceVlan,
        string sourceIp)
    {
        Client = client;
        Action = action;
        Status = status;
        Detail = detail;
        Subnet = subnet;
        SourceVlan = sourceVlan;
        SourceIp = sourceIp;
    }

    internal ClientApp Client { get; }
    internal RouterAction Action { get; }
    internal string Status { get; }
    internal string Detail { get; }
    internal string Subnet { get; }
    internal int SourceVlan { get; }
    internal string SourceIp { get; }
}

internal static class RouterAudit
{
    private sealed class SubnetPlan
    {
        internal SubnetPlan(string subnet, int vlan, string gateway)
        {
            Subnet = subnet;
            Vlan = vlan;
            Gateway = gateway;
        }

        internal string Subnet { get; }
        internal int Vlan { get; }
        internal string Gateway { get; }
    }

    private static readonly Dictionary<string, SubnetPlan> Plans =
        new Dictionary<string, SubnetPlan>(StringComparer.Ordinal);

    internal static void Reset() => Plans.Clear();

    internal static List<RouterAuditItem> Build(RouterConfiguration config)
    {
        var result = new List<RouterAuditItem>();
        Router? router = config.router;
        if (router == null)
            return result;

        var clients = ClientHelperData.SnapshotNetwork(router.switchId);
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

        var usedSubnets = new List<string>();
        var usedVlans = new HashSet<int>();
        for (int i = 0; i < owned.Count; i++)
        {
            usedSubnets.Add(owned[i].subnetCidr);
            usedVlans.Add(owned[i].vlanId);
        }
        for (int i = 0; i < clients.Count; i++)
        {
            usedSubnets.Add(clients[i].Subnet);
            usedVlans.Add(clients[i].Vlan);
        }
        var knownVlans = router.knownVlans;
        if (knownVlans != null)
        {
            foreach (int vlan in knownVlans)
                usedVlans.Add(vlan);
        }

        var map = NetworkMap.instance;
        if (map != null)
        {
            var networkVlans = map.usedVlanIDs;
            if (networkVlans != null)
            {
                foreach (int vlan in networkVlans)
                    usedVlans.Add(vlan);
            }

            var networkSubnets = map.usedSubnets;
            if (networkSubnets != null)
            {
                foreach (string subnet in networkSubnets)
                    usedSubnets.Add(subnet);
            }
        }
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var client in clients)
        {
            if (string.IsNullOrEmpty(client.Subnet))
                continue;

            string key = $"{client.CustomerBaseId}:{client.AppId}";
            if (!seen.Add(key))
                continue;

            Router.RouteEntry? route = FindRoute(routes, client);
            if (route != null)
            {
                Router.OwnedSubnet? source = FindOwnedByVlan(owned, route.sourceVlanId);
                if (source == null)
                {
                    string derived = SubnetForIp(route.sourceIp);
                    if (Plans.TryGetValue(key, out SubnetPlan? routePlan) &&
                        routePlan.Vlan == route.sourceVlanId)
                    {
                        derived = routePlan.Subnet;
                    }

                    result.Add(new RouterAuditItem(
                        client,
                        RouterAction.FillSubnet,
                        "Missing source subnet",
                        $"route source VLAN {route.sourceVlanId} is not owned by this router",
                        derived,
                        route.sourceVlanId,
                        route.sourceIp));
                    continue;
                }

                if (!IsSourceValid(route, owned))
                {
                    result.Add(new RouterAuditItem(
                        client,
                        RouterAction.FillRoute,
                        "Invalid route source",
                        $"route source VLAN {route.sourceVlanId} is not usable",
                        string.Empty,
                        -1,
                        string.Empty));
                }

                continue;
            }

            SubnetPlan plan = GetPlan(key, usedSubnets, usedVlans, map);
            Router.OwnedSubnet? plannedOwned = FindOwnedByVlan(owned, plan.Vlan);
            if (plannedOwned != null)
            {
                string sourceIp = map?.GetGatewayForSubnet(plannedOwned.subnetCidr) ?? plan.Gateway;
                result.Add(new RouterAuditItem(
                    client,
                    RouterAction.FillRoute,
                    "Missing route",
                    $"VLAN {plan.Vlan}  ->  VLAN {client.Vlan}",
                    string.Empty,
                    plan.Vlan,
                    sourceIp));
                continue;
            }

            result.Add(new RouterAuditItem(
                client,
                RouterAction.FillSubnet,
                "Step 1: add subnet",
                $"new {plan.Subnet}  VLAN {plan.Vlan}  ->  customer VLAN {client.Vlan}",
                plan.Subnet,
                plan.Vlan,
                plan.Gateway));
        }

        return result;
    }

    private static Router.RouteEntry? FindRoute(
        List<Router.RouteEntry> routes,
        ClientApp client)
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

    private static Router.OwnedSubnet? FindOwnedByVlan(
        List<Router.OwnedSubnet> owned,
        int vlan)
    {
        for (int i = 0; i < owned.Count; i++)
        {
            if (owned[i].vlanId == vlan && !string.IsNullOrEmpty(owned[i].subnetCidr))
                return owned[i];
        }

        return null;
    }

    private static bool IsSourceValid(
        Router.RouteEntry route,
        List<Router.OwnedSubnet> owned)
    {
        return FindOwnedByVlan(owned, route.sourceVlanId) != null;
    }

    private static SubnetPlan GetPlan(
        string key,
        List<string> usedSubnets,
        HashSet<int> usedVlans,
        NetworkMap? map)
    {
        if (Plans.TryGetValue(key, out SubnetPlan? existing))
        {
            usedSubnets.Add(existing.Subnet);
            usedVlans.Add(existing.Vlan);
            return existing;
        }

        string subnet = string.Empty;
        for (int octet = 1; octet < 255; octet++)
        {
            string candidate = $"10.250.{octet}.0/24";
            if (!OverlapsAny(candidate, usedSubnets))
            {
                subnet = candidate;
                break;
            }
        }

        if (string.IsNullOrEmpty(subnet))
            subnet = $"10.251.{Plans.Count % 254 + 1}.0/24";

        int vlan = 1000;
        while (vlan < 4095 && usedVlans.Contains(vlan))
            vlan++;

        string gateway = map?.GetGatewayForSubnet(subnet) ?? string.Empty;
        if (string.IsNullOrEmpty(gateway))
        {
            int slash = subnet.IndexOf('/');
            string baseIp = slash >= 0 ? subnet.Substring(0, slash) : subnet;
            int lastDot = baseIp.LastIndexOf('.');
            gateway = lastDot >= 0 ? baseIp.Substring(0, lastDot) + ".1" : baseIp;
        }

        var plan = new SubnetPlan(subnet, vlan, gateway);
        Plans[key] = plan;
        usedSubnets.Add(subnet);
        usedVlans.Add(vlan);
        return plan;
    }

    private static string SubnetForIp(string ip)
    {
        if (!TryParseIp(ip, out uint address))
            return string.Empty;

        uint mask = 0xFFFFFF00u;
        uint network = address & mask;
        return $"{network >> 24}.{(network >> 16) & 0xFF}.{(network >> 8) & 0xFF}.{network & 0xFF}/24";
    }

    private static bool OverlapsAny(string cidr, List<string> used)
    {
        if (!TryParseCidr(cidr, out uint network, out int prefix))
            return true;

        for (int i = 0; i < used.Count; i++)
        {
            if (TryParseCidr(used[i], out uint other, out int otherPrefix) &&
                Overlaps(network, prefix, other, otherPrefix))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Overlaps(uint a, int aPrefix, uint b, int bPrefix)
    {
        int prefix = Math.Min(aPrefix, bPrefix);
        uint mask = prefix == 0 ? 0u : 0xFFFFFFFFu << (32 - prefix);
        return (a & mask) == (b & mask);
    }

    private static bool TryParseCidr(string cidr, out uint network, out int prefix)
    {
        network = 0;
        prefix = 0;
        int slash = cidr.IndexOf('/');
        if (slash < 0)
            return false;

        return TryParseIp(cidr.Substring(0, slash), out network) &&
            int.TryParse(cidr.Substring(slash + 1), out prefix) &&
            prefix >= 0 && prefix <= 32;
    }

    private static bool IsInSubnet(string ip, string cidr)
    {
        if (!TryParseCidr(cidr, out uint network, out int prefix) ||
            !TryParseIp(ip, out uint address))
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
