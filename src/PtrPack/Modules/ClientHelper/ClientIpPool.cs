using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using PtrLib.Networking;
using UnityEngine;

namespace PtrPack.Modules.ClientHelper;

internal static class ClientIpPool
{
    private static HashSet<string>? _usedIpsCache;
    private static float _usedIpsCacheAt = -1000f;

    internal static HashSet<string> CollectUsedIps()
    {
        float now = Time.unscaledTime;
        if (_usedIpsCache != null && now - _usedIpsCacheAt < 0.5f)
            return _usedIpsCache;

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

        _usedIpsCache = used;
        _usedIpsCacheAt = now;
        return used;
    }

    internal static void ResetCaches()
    {
        _usedIpsCache = null;
        _usedIpsCacheAt = -1000f;
    }

    internal static int CountUsed(
        CustomerBase customerBase,
        Il2CppStringArray? usableIps,
        HashSet<string> usedIps)
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

    internal static string FindLowestFree(
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

        candidates.Sort(Ipv4Address.Compare);
        for (int i = 0; i < candidates.Count; i++)
        {
            if (!usedIps.Contains(candidates[i]))
                return candidates[i];
        }

        return string.Empty;
    }
}
