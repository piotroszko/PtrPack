using System;
using System.Collections.Generic;
using System.Text;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppHashSet = Il2CppSystem.Collections.Generic.HashSet<int>;

namespace PtrPack.Modules.ClientHelper;

internal sealed class SwitchPortAudit
{
    internal SwitchPortAudit(
        int index,
        string remote,
        int serverVlan,
        bool vlanBlocked,
        string deniedVlans)
    {
        Index = index;
        Remote = remote;
        ServerVlan = serverVlan;
        VlanBlocked = vlanBlocked;
        DeniedVlans = deniedVlans;
    }

    internal int Index { get; }
    internal string Remote { get; }
    internal int ServerVlan { get; }
    internal bool VlanBlocked { get; }
    internal string DeniedVlans { get; }
}

internal sealed class SwitchVlanGap
{
    internal SwitchVlanGap(int portIndex, ClientApp client, string detail)
    {
        PortIndex = portIndex;
        Client = client;
        Detail = detail;
    }

    internal int PortIndex { get; }
    internal ClientApp Client { get; }
    internal string Detail { get; }
}

internal sealed class SwitchAuditResult
{
    internal SwitchAuditResult(
        List<SwitchPortAudit> ports,
        List<SwitchVlanGap> gaps,
        int linkedPorts,
        int blockedPorts)
    {
        Ports = ports;
        Gaps = gaps;
        LinkedPorts = linkedPorts;
        BlockedPorts = blockedPorts;
    }

    internal List<SwitchPortAudit> Ports { get; }
    internal List<SwitchVlanGap> Gaps { get; }
    internal int LinkedPorts { get; }
    internal int BlockedPorts { get; }
}

internal static class SwitchAudit
{
    internal static SwitchAuditResult Build(NetworkSwitchConfiguration config)
    {
        var ports = new List<SwitchPortAudit>();
        var gaps = new List<SwitchVlanGap>();
        NetworkSwitch? networkSwitch = config.currentNetworkSwitch;
        if (networkSwitch == null)
            return new SwitchAuditResult(ports, gaps, 0, 0);

        Il2CppReferenceArray<CableLink>? cables = networkSwitch.cableLinkSwitchPorts;
        if (cables == null)
            cables = config.currentPorts;

        int portCount = cables == null ? networkSwitch.PortCount : cables.Length;
        int linked = 0;
        int blocked = 0;
        var clients = ClientHelperData.SnapshotNetwork(networkSwitch.switchId);

        for (int i = 0; i < portCount; i++)
        {
            CableLink? cable = cables != null && i < cables.Length ? cables[i] : null;
            string remote = Describe(cable);
            if (string.IsNullOrEmpty(remote))
                continue;

            linked++;
            int serverVlan = -1;
            if (cable?.parentServer != null)
            {
                try
                {
                    serverVlan = config.ResolveServerVlanId(cable.parentServer, out _);
                }
                catch (Exception)
                {
                    serverVlan = -1;
                }
            }

            Il2CppHashSet? denied = GetDenied(networkSwitch, i);
            bool vlanBlocked = denied != null && serverVlan >= 0 && denied.Contains(serverVlan);
            if (vlanBlocked)
                blocked++;

            ports.Add(new SwitchPortAudit(
                i,
                remote,
                serverVlan,
                vlanBlocked,
                DescribeSet(denied)));

            if (denied == null)
                continue;

            foreach (var client in clients)
            {
                if (client.Vlan >= 0 &&
                    IsKnownVlan(networkSwitch, client.Vlan) &&
                    denied.Contains(client.Vlan))
                {
                    gaps.Add(new SwitchVlanGap(
                        i,
                        client,
                        $"VLAN {client.Vlan} is denied on port {i + 1} ({remote})"));
                }
            }
        }

        return new SwitchAuditResult(ports, gaps, linked, blocked);
    }

    private static bool IsKnownVlan(NetworkSwitch networkSwitch, int vlan)
    {
        var known = networkSwitch.knownVlans;
        return known == null || known.Count == 0 || known.Contains(vlan);
    }

    private static string Describe(CableLink? cable)
    {
        if (cable == null)
            return string.Empty;

        if (cable.parentServer != null)
            return "Server " + cable.parentServer.ServerID;
        if (cable.parentSwitch != null)
            return "Switch " + cable.parentSwitch.switchId;
        if (cable.parentPatchPanel != null)
            return "Patch panel";
        if (cable.CustomerID >= 0)
            return $"Customer #{cable.CustomerID}";

        return cable.typeOfLink == CableLink.TypeOfLink.None
            ? string.Empty
            : cable.typeOfLink.ToString();
    }

    private static Il2CppHashSet? GetDenied(NetworkSwitch networkSwitch, int portIndex)
    {
        var denied = networkSwitch.disallowedVlansPerPort;
        if (denied == null)
            return null;

        if (denied.TryGetValue(portIndex, out Il2CppHashSet? set))
            return set;

        if (denied.TryGetValue(portIndex + 1, out set))
            return set;

        return null;
    }

    private static string DescribeSet(Il2CppHashSet? set)
    {
        if (set == null || set.Count == 0)
            return string.Empty;

        var text = new StringBuilder();
        foreach (int vlan in set)
        {
            if (text.Length > 0)
                text.Append(',');
            text.Append(vlan);
        }

        return text.ToString();
    }
}
