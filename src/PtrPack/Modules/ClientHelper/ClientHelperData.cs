using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using PtrLib.Ui;
using UnityEngine;

namespace PtrPack.Modules.ClientHelper;

internal sealed class ClientApp
{
    internal ClientApp(
        int customerBaseId,
        string customerName,
        int appId,
        string subnet,
        int vlan,
        string gateway,
        string lowestFreeIp,
        int usedIps,
        int totalIps,
        int requiredServerType)
    {
        CustomerBaseId = customerBaseId;
        CustomerName = customerName;
        AppId = appId;
        Subnet = subnet;
        Vlan = vlan;
        Gateway = gateway;
        LowestFreeIp = lowestFreeIp;
        UsedIps = usedIps;
        TotalIps = totalIps;
        RequiredServerType = requiredServerType;
    }

    internal int CustomerBaseId { get; }
    internal string CustomerName { get; }
    internal int AppId { get; }
    internal string Subnet { get; }
    internal int Vlan { get; }
    internal string Gateway { get; }
    internal string LowestFreeIp { get; }
    internal int UsedIps { get; }
    internal int TotalIps { get; }
    internal int RequiredServerType { get; }

    internal bool HasFreeIp => !string.IsNullOrEmpty(LowestFreeIp);

    internal string Title => $"{CustomerName}  •  App {AppId}";
}

internal static class ClientHelperData
{
    internal static List<ClientApp> Snapshot(Server? server = null)
    {
        var result = new List<ClientApp>();
        var map = NetworkMap.instance;
        if (map == null)
            return result;

        var setIp = UiFactory.FindSceneComponent<SetIP>();
        var usedIps = CollectUsedIps();
        int serverType = server != null ? server.serverType : -1;

        var customerBases = new List<CustomerBase>();
        var customerBaseMap = map.customerBases;
        if (customerBaseMap != null)
        {
            var keys = new Il2CppStructArray<int>(customerBaseMap.Count);
            customerBaseMap.Keys.CopyTo(keys, 0);
            for (int i = 0; i < keys.Length; i++)
            {
                if (customerBaseMap.TryGetValue(keys[i], out CustomerBase customerBase) &&
                    customerBase != null)
                {
                    customerBases.Add(customerBase);
                }
            }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var customerBase in customerBases)
        {
            if (customerBase == null)
                continue;

            var item = customerBase.customerItem;
            if (item == null)
                continue;

            string customerName = string.IsNullOrWhiteSpace(item.customerName)
                ? $"Customer {customerBase.customerID}"
                : item.customerName;

            var appIds = new List<int>();
            if (item.appTypes != null)
            {
                for (int i = 0; i < item.appTypes.Length; i++)
                    appIds.Add(item.appTypes[i]);
            }

            if (appIds.Count == 0)
            {
                for (int appId = 0; appId < 8; appId++)
                {
                    if (customerBase.subnetsPerApp != null &&
                        customerBase.subnetsPerApp.ContainsKey(appId))
                    {
                        appIds.Add(appId);
                    }
                }
            }

            foreach (int appId in appIds)
            {
                string subnet = Get(customerBase.subnetsPerApp, appId, string.Empty);
                if (string.IsNullOrEmpty(subnet))
                    continue;

                string key = $"{customerName}:{appId}:{subnet}";
                if (!seen.Add(key))
                    continue;

                int requiredType = Get(customerBase.appIdToServerType, appId, -1);
                if (server != null)
                {
                    int assignedApp = server.appID;
                    bool eligible = assignedApp == appId ||
                        (assignedApp < 0 && requiredType >= 0 && server.serverType == requiredType);
                    if (!eligible)
                        continue;
                }

                int vlan = Get(customerBase.vlanIdsPerApp, appId, 0);
                string gateway = map.GetGatewayForSubnet(subnet) ?? string.Empty;

                Il2CppStringArray? usableIps = null;
                customerBase.usableIpsPerApp?.TryGetValue(appId, out usableIps);

                int used = CountUsed(customerBase, usableIps, usedIps);
                int total = usableIps == null ? 0 : usableIps.Length;

                string lowest = FindLowestFree(
                    usableIps,
                    subnet,
                    gateway,
                    customerBase,
                    setIp,
                    usedIps);

                result.Add(new ClientApp(
                    customerBase.customerBaseID,
                    customerName,
                    appId,
                    subnet,
                    vlan,
                    gateway,
                    lowest,
                    used,
                    total,
                    requiredType));
            }
        }

        result.Sort((left, right) =>
        {
            int name = string.Compare(left.CustomerName, right.CustomerName, StringComparison.OrdinalIgnoreCase);
            return name != 0 ? name : left.AppId.CompareTo(right.AppId);
        });

        return result;
    }

    private static HashSet<string> CollectUsedIps()
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var servers = UnityEngine.Object.FindObjectsOfType<Server>();
        for (int i = 0; i < servers.Length; i++)
        {
            var server = servers[i];
            if (server == null)
                continue;

            string ip = server.IP;
            if (!string.IsNullOrWhiteSpace(ip) && ip != "0.0.0.0")
                used.Add(ip);
        }

        return used;
    }

    private static int CountUsed(CustomerBase customerBase, Il2CppStringArray? usableIps, HashSet<string> usedIps)
    {
        if (usableIps == null)
            return 0;

        int used = 0;
        for (int i = 0; i < usableIps.Length; i++)
        {
            string ip = usableIps[i];
            if (!string.IsNullOrWhiteSpace(ip) && usedIps.Contains(ip))
                used++;
        }

        return used;
    }

    private static string FindLowestFree(
        Il2CppStringArray? usableIps,
        string subnet,
        string gateway,
        CustomerBase customerBase,
        SetIP? setIp,
        HashSet<string> usedIps)
    {
        var candidates = new List<string>();
        if (usableIps != null)
        {
            for (int i = 0; i < usableIps.Length; i++)
            {
                string ip = usableIps[i];
                if (string.IsNullOrWhiteSpace(ip)) continue;
                if (!string.IsNullOrEmpty(gateway) && string.Equals(ip, gateway, StringComparison.Ordinal)) continue;
                candidates.Add(ip);
            }
        }

        if (candidates.Count == 0 && setIp != null && !string.IsNullOrEmpty(subnet))
        {
            var generated = setIp.GetUsableIPsFromSubnet(subnet);
            if (generated != null)
            {
                for (int i = 0; i < generated.Length; i++)
                {
                    string ip = generated[i];
                    if (string.IsNullOrWhiteSpace(ip)) continue;
                    if (!string.IsNullOrEmpty(gateway) && string.Equals(ip, gateway, StringComparison.Ordinal)) continue;
                    if (!customerBase.IsIPPresent(ip)) continue;
                    candidates.Add(ip);
                }
            }
        }

        candidates.Sort(CompareIp);
        for (int i = 0; i < candidates.Count; i++)
        {
            if (!usedIps.Contains(candidates[i]))
                return candidates[i];
        }

        return string.Empty;
    }

    private static int CompareIp(string left, string right)
    {
        if (TryParseIp(left, out uint a) && TryParseIp(right, out uint b))
            return a.CompareTo(b);
        return string.Compare(left, right, StringComparison.Ordinal);
    }

    private static bool TryParseIp(string text, out uint value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string[] parts = text.Split('.');
        if (parts.Length != 4)
            return false;

        uint result = 0;
        for (int i = 0; i < 4; i++)
        {
            if (!byte.TryParse(parts[i], out byte octet))
                return false;
            result = (result << 8) | octet;
        }

        value = result;
        return true;
    }

    private static T Get<T>(
        Il2CppSystem.Collections.Generic.Dictionary<int, T>? dictionary,
        int key,
        T fallback)
    {
        if (dictionary == null)
            return fallback;

        return dictionary.TryGetValue(key, out T value) ? value : fallback;
    }
}
