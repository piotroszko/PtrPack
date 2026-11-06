using System;
using HarmonyLib;
using Il2Cpp;

namespace PtrPack.Modules;

[HarmonyPatch(typeof(Server), nameof(Server.TickTimer))]
internal static class ServerTickTimerPatch
{
    private const long HoldFlag = 1L << 32;

    [HarmonyPrefix]
    private static void Prefix(Server __instance, out long __state)
    {
        int original = __instance.eolTime;
        __state = original;

        try
        {
            if (!EolControl.ShouldHold(__instance.Pointer))
                return;
        }
        catch (Exception)
        {
            return;
        }

        __state = original + HoldFlag;
        if (original < int.MaxValue)
            __instance.eolTime = Math.Max(original, 1) + 1;
    }

    [HarmonyPostfix]
    private static void Postfix(Server __instance, long __state)
    {
        if (__state > int.MaxValue)
            __instance.eolTime = unchecked((int)(__state - HoldFlag));
    }
}

[HarmonyPatch(typeof(NetworkSwitch), nameof(NetworkSwitch.TickTimer))]
internal static class NetworkSwitchTickTimerPatch
{
    private const long HoldFlag = 1L << 32;

    [HarmonyPrefix]
    private static void Prefix(NetworkSwitch __instance, out long __state)
    {
        int original = __instance.eolTime;
        __state = original;

        try
        {
            if (!EolControl.ShouldHold(__instance.Pointer))
                return;
        }
        catch (Exception)
        {
            return;
        }

        __state = original + HoldFlag;
        if (original < int.MaxValue)
            __instance.eolTime = Math.Max(original, 1) + 1;
    }

    [HarmonyPostfix]
    private static void Postfix(NetworkSwitch __instance, long __state)
    {
        if (__state > int.MaxValue)
            __instance.eolTime = unchecked((int)(__state - HoldFlag));
    }
}
