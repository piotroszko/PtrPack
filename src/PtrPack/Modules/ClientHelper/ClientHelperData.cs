using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using PtrLib.Interop;
using PtrLib.Networking;
using PtrLib.Ui;
using UnityEngine;

namespace PtrPack.Modules.ClientHelper;

internal static class ClientHelperData
{
    internal static List<ClientApp> Snapshot(
        Server? server = null,
        string? targetDeviceId = null,
        bool includeRouted = false)
    {
        var result = new List<ClientApp>();
        var map = NetworkMap.instance;
        if (map == null)
            return result;

        var setIp = UiFactory.FindSceneComponent<SetIP>();
        var usedIps = ClientIpPool.CollectUsedIps();

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
            if (customerBase.subnetsPerApp != null && customerBase.subnetsPerApp.Count > 0)
            {
                var appKeys = new Il2CppStructArray<int>(customerBase.subnetsPerApp.Count);
                customerBase.subnetsPerApp.Keys.CopyTo(appKeys, 0);
                for (int i = 0; i < appKeys.Length; i++)
                    appIds.Add(appKeys[i]);
            }
            else if (item.appTypes != null)
            {
                for (int i = 0; i < item.appTypes.Length; i++)
                    appIds.Add(item.appTypes[i]);
            }

            bool reachable = ClientReachability.IsCustomerReachable(map, server, targetDeviceId, customerBase.customerID);
            if (!reachable)
                continue;

            foreach (int appId in appIds)
            {
                string subnet = customerBase.subnetsPerApp.ValueOrDefault(appId, string.Empty);
                if (string.IsNullOrEmpty(subnet))
                    continue;

                string key = $"{customerName}:{appId}:{subnet}";
                if (!seen.Add(key))
                    continue;

                int requiredType = customerBase.appIdToServerType.ValueOrDefault(appId, -1);
                if (server != null && server.appID >= 0 && server.appID != appId)
                    continue;

                int vlan = customerBase.vlanIdsPerApp.ValueOrDefault(appId, 0);
                string gateway = map.GetGatewayForSubnet(subnet) ?? string.Empty;

                Il2CppStringArray? usableIps = null;
                customerBase.usableIpsPerApp?.TryGetValue(appId, out usableIps);

                int used = ClientIpPool.CountUsed(customerBase, usableIps, usedIps);
                int total = usableIps == null ? 0 : usableIps.Length;

                string lowest = ClientIpPool.FindLowestFree(
                    usableIps,
                    subnet,
                    gateway,
                    customerBase,
                    setIp,
                    usedIps);

                var client = new ClientApp(
                    customerBase.customerBaseID,
                    customerBase.customerID,
                    customerName,
                    appId,
                    subnet,
                    vlan,
                    gateway,
                    lowest,
                    used,
                    total,
                    requiredType,
                    item.logo);
                result.Add(client);
            }
        }

        result.Sort((left, right) =>
        {
            int name = string.Compare(left.CustomerName, right.CustomerName, StringComparison.OrdinalIgnoreCase);
            return name != 0 ? name : left.AppId.CompareTo(right.AppId);
        });

        if (includeRouted)
            RoutedSubnets.FillClients(result, usedIps, server);

        return result;
    }

    internal static List<ClientApp> SnapshotNetwork(string? targetDeviceId = null)
    {
        var result = new List<ClientApp>();
        var map = NetworkMap.instance;
        if (map == null)
            return result;

        HashSet<int>? reachableCustomers = ClientReachability.GetReachableCustomers(map, targetDeviceId);
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
            var item = customerBase.customerItem;
            if (item == null)
                continue;

            string customerName = string.IsNullOrWhiteSpace(item.customerName)
                ? $"Customer {customerBase.customerID}"
                : item.customerName;

            if (reachableCustomers != null &&
                !reachableCustomers.Contains(customerBase.customerID))
            {
                continue;
            }

            var appIds = new List<int>();
            if (customerBase.subnetsPerApp != null && customerBase.subnetsPerApp.Count > 0)
            {
                var appKeys = new Il2CppStructArray<int>(customerBase.subnetsPerApp.Count);
                customerBase.subnetsPerApp.Keys.CopyTo(appKeys, 0);
                for (int i = 0; i < appKeys.Length; i++)
                    appIds.Add(appKeys[i]);
            }
            else if (item.appTypes != null)
            {
                for (int i = 0; i < item.appTypes.Length; i++)
                    appIds.Add(item.appTypes[i]);
            }

            foreach (int appId in appIds)
            {
                string subnet = customerBase.subnetsPerApp.ValueOrDefault(appId, string.Empty);
                if (string.IsNullOrEmpty(subnet))
                    continue;

                string key = $"{customerName}:{appId}:{subnet}";
                if (!seen.Add(key))
                    continue;

                int vlan = customerBase.vlanIdsPerApp.ValueOrDefault(appId, 0);
                string gateway = map.GetGatewayForSubnet(subnet) ?? string.Empty;
                int requiredType = customerBase.appIdToServerType.ValueOrDefault(appId, -1);
                result.Add(new ClientApp(
                    customerBase.customerBaseID,
                    customerBase.customerID,
                    customerName,
                    appId,
                    subnet,
                    vlan,
                    gateway,
                    string.Empty,
                    0,
                    0,
                    requiredType,
                    item.logo));
            }
        }

        result.Sort((left, right) =>
        {
            int name = string.Compare(left.CustomerName, right.CustomerName, StringComparison.OrdinalIgnoreCase);
            return name != 0 ? name : left.AppId.CompareTo(right.AppId);
        });
        return result;
    }
    internal static void ResetCaches() => ClientIpPool.ResetCaches();
}
