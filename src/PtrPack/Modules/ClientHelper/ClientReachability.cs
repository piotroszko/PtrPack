using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace PtrPack.Modules.ClientHelper;

internal static class ClientReachability
{
    internal static HashSet<int>? GetReachableCustomers(NetworkMap map, string? targetDeviceId)
    {
        if (string.IsNullOrEmpty(targetDeviceId))
            return null;

        try
        {
            var paths = map.FindAllReachablePathsFrom(targetDeviceId);
            if (paths == null)
                return null;

            var customers = new HashSet<int>();
            var keys = new Il2CppStringArray(paths.Count);
            paths.Keys.CopyTo(keys, 0);
            for (int i = 0; i < keys.Length; i++)
            {
                var device = map.GetDevice(keys[i]);
                if (device != null && device.CustomerID >= 0)
                    customers.Add(device.CustomerID);
            }

            return customers;
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static bool IsCustomerReachable(
        NetworkMap map,
        Server? server,
        string? targetDeviceId,
        int customerId)
    {
        string? target = server?.ServerID;
        if (string.IsNullOrEmpty(target))
            target = targetDeviceId;

        if (string.IsNullOrEmpty(target))
            return true;

        try
        {
            var paths = map.FindAllShortestPathsFromCustomer(customerId, target);
            int count = paths == null ? 0 : paths.Count;
            if (count > 0)
                return true;
        }
        catch (Exception exception)
        {
            MelonLoader.MelonLogger.Warning(
                $"Client helper: path check failed for customer #{customerId}: {exception.Message}");
        }

        try
        {
            var devices = map.devices;
            if (devices == null)
                return true;

            bool sawCustomerDevice = false;
            var keys = new Il2CppStringArray(devices.Count);
            devices.Keys.CopyTo(keys, 0);
            for (int i = 0; i < keys.Length; i++)
            {
                var device = devices[keys[i]];
                if (device == null || device.CustomerID != customerId)
                    continue;

                sawCustomerDevice = true;
                if (map.IsPhysicallyReachable(target, device.Name))
                    return true;
            }

            if (!sawCustomerDevice)
                return true;
        }
        catch (Exception exception)
        {
            MelonLoader.MelonLogger.Warning(
                $"Client helper: physical check failed for customer #{customerId}: {exception.Message}");
        }

        return false;
    }
}
