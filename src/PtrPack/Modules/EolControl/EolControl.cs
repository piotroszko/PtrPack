using System;
using System.Collections.Generic;
using Il2Cpp;

namespace PtrPack.Modules;

internal static class EolControl
{
    private static readonly Dictionary<IntPtr, int> Ticks = new Dictionary<IntPtr, int>();

    internal static bool Disabled { get; private set; }
    internal static int Multiplier { get; private set; } = 1;

    internal static void Configure(bool disabled, int multiplier)
    {
        Disabled = disabled;
        Multiplier = Math.Clamp(multiplier, 1, 50);
        Ticks.Clear();
    }

    internal static void SetDisabled(bool disabled)
    {
        Disabled = disabled;
        Ticks.Clear();
    }

    internal static void SetMultiplier(int multiplier)
    {
        Multiplier = Math.Clamp(multiplier, 1, 50);
        Ticks.Clear();
    }

    internal static void ResetTicks() => Ticks.Clear();

    internal static bool ShouldHold(IntPtr device)
    {
        if (Disabled) return true;
        if (Multiplier <= 1) return false;

        int count = Ticks.TryGetValue(device, out int current) ? current + 1 : 1;
        if (count >= Multiplier)
        {
            Ticks[device] = 0;
            return false;
        }

        Ticks[device] = count;
        return true;
    }

    internal static int AddTimeToAll(int seconds)
    {
        if (seconds <= 0)
            return 0;

        var manager = DeviceTimerManager.instance;
        if (manager == null)
            return 0;

        var devices = manager.activeDevices;
        if (devices == null)
            return 0;

        int count = 0;
        for (int i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            if (device == null)
                continue;

            var server = device.TryCast<Server>();
            if (server != null)
            {
                if (server.isBroken)
                    continue;

                server.eolTime = AddClamped(server.eolTime, seconds);
                server.timeToBrake = AddClamped(server.timeToBrake, seconds);
                count++;
                continue;
            }

            var networkSwitch = device.TryCast<NetworkSwitch>();
            if (networkSwitch == null || networkSwitch.isBroken)
                continue;

            networkSwitch.eolTime = AddClamped(networkSwitch.eolTime, seconds);
            networkSwitch.timeToBrake = AddClamped(networkSwitch.timeToBrake, seconds);
            count++;
        }

        return count;
    }

    private static int AddClamped(int value, int delta)
        => value > int.MaxValue - delta ? int.MaxValue : value + delta;
}
