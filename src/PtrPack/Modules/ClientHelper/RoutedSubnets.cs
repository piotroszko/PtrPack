using System;
using System.Collections.Generic;
using Il2Cpp;
using PtrLib.Networking;
using UnityEngine;

namespace PtrPack.Modules.ClientHelper;

internal sealed class RoutedIpOption
{
    internal RoutedIpOption(
        string routerId,
        string subnet,
        int vlan,
        string gateway,
        string lowestFreeIp,
        int usedIps,
        int totalIps,
        int targetVlan,
        string targetIp)
    {
        RouterId = routerId;
        Subnet = subnet;
        Vlan = vlan;
        Gateway = gateway;
        LowestFreeIp = lowestFreeIp;
        UsedIps = usedIps;
        TotalIps = totalIps;
        TargetVlan = targetVlan;
        TargetIp = targetIp;
    }

    internal string RouterId { get; }
    internal string Subnet { get; }
    internal int Vlan { get; }
    internal string Gateway { get; }
    internal string LowestFreeIp { get; }
    internal int UsedIps { get; }
    internal int TotalIps { get; }
    internal int TargetVlan { get; }
    internal string TargetIp { get; }

    internal bool HasFreeIp => !string.IsNullOrEmpty(LowestFreeIp);

    internal string Detail =>
        $"via {RouterId}  VLAN {Vlan}  {Subnet}\n" +
        $"GW {Gateway}  low {LowestFreeIp}  -> client VLAN {TargetVlan} {TargetIp}";
}

internal static class RoutedSubnets
{
    internal static void FillClients(
        List<ClientApp> clients,
        HashSet<string> usedIps,
        Server? server)
    {
        var routers = Resources.FindObjectsOfTypeAll<Router>();
        var map = NetworkMap.instance;
        for (int i = 0; i < routers.Length; i++)
        {
            var router = routers[i];
            if (router == null || !router.gameObject.scene.IsValid())
                continue;

            var routes = router.routes;
            var owned = router.ownedSubnets;
            if (routes == null || owned == null)
                continue;

            string routerId = string.IsNullOrWhiteSpace(router.switchId)
                ? router.name
                : router.switchId;

            if (server != null && map != null)
            {
                try
                {
                    if (!map.IsPhysicallyReachable(routerId, server.ServerID))
                        continue;
                }
                catch (Exception)
                {
                    // Keep the option if the path check cannot run.
                }
            }

            for (int clientIndex = 0; clientIndex < clients.Count; clientIndex++)
            {
                var client = clients[clientIndex];
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int routeIndex = 0; routeIndex < routes.Count; routeIndex++)
            {
                var route = routes[routeIndex];
                if (route == null || route.targetVlanId != client.Vlan)
                    continue;
                if (!RouteTargetsClient(route.targetIp, client))
                    continue;

                var source = FindOwned(owned, route.sourceVlanId);
                if (source == null || string.IsNullOrEmpty(source.subnetCidr))
                    continue;

                string key = $"{routerId}:{source.subnetCidr}:{source.vlanId}";
                if (!seen.Add(key))
                    continue;

                string gateway = map?.GetGatewayForSubnet(source.subnetCidr) ?? string.Empty;
                if (string.IsNullOrEmpty(gateway))
                    gateway = route.sourceIp ?? string.Empty;

                string free = FindLowestFree(source.subnetCidr, gateway, usedIps);
                int hostCount = Ipv4Subnet.HostCount(source.subnetCidr);
                int used = CountUsedInSubnet(source.subnetCidr, usedIps);
                client.RoutedOptions.Add(new RoutedIpOption(
                    routerId,
                    source.subnetCidr,
                    source.vlanId,
                    gateway,
                    free,
                    used,
                    hostCount,
                    route.targetVlanId,
                    route.targetIp ?? string.Empty));
                }
            }
        }
    }

    private static Router.OwnedSubnet? FindOwned(
        Il2CppSystem.Collections.Generic.List<Router.OwnedSubnet> owned,
        int vlan)
    {
        for (int i = 0; i < owned.Count; i++)
        {
            var subnet = owned[i];
            if (subnet != null && subnet.vlanId == vlan && !string.IsNullOrEmpty(subnet.subnetCidr))
                return subnet;
        }

        return null;
    }

    private static bool RouteTargetsClient(string targetIp, ClientApp client)
    {
        if (string.IsNullOrEmpty(targetIp))
            return true;
        if (!string.IsNullOrEmpty(client.Gateway) &&
            string.Equals(targetIp, client.Gateway, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Ipv4Subnet.Contains(targetIp, client.Subnet);
    }

    private static string FindLowestFree(
        string cidr,
        string gateway,
        HashSet<string> usedIps)
    {
        if (!Ipv4Subnet.TryParseCidr(cidr, out uint network, out int prefix) || prefix > 30)
            return string.Empty;

        uint broadcast = network | (prefix == 0 ? uint.MaxValue : 0xFFFFFFFFu >> prefix);
        uint first = network + 1;
        uint last = broadcast - 1;
        if (first > last)
            return string.Empty;

        int limit = 65536;
        for (uint value = first; value <= last && limit-- > 0; value++)
        {
            string ip = Ipv4Address.Format(value);
            if (string.Equals(ip, gateway, StringComparison.Ordinal))
                continue;
            if (usedIps.Contains(ip))
                continue;
            return ip;
        }

        return string.Empty;
    }

    private static int CountUsedInSubnet(string cidr, HashSet<string> usedIps)
    {
        if (!Ipv4Subnet.TryParseCidr(cidr, out uint network, out int prefix))
            return 0;

        uint broadcast = network | (prefix == 0 ? uint.MaxValue : 0xFFFFFFFFu >> prefix);
        int used = 0;
        foreach (string ip in usedIps)
        {
            if (Ipv4Address.TryParse(ip, out uint value) && value > network && value < broadcast)
                used++;
        }

        return used;
    }

}
