using System;
using System.Collections.Generic;

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
}
