using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppTMPro;

namespace PtrPack.Modules;

internal struct EolTickState
{
    internal int TimeToBrake;
    internal int EolTime;
    internal bool Held;
}

[HarmonyPatch(typeof(Server), nameof(Server.TickTimer))]
internal static class ServerTickTimerPatch
{
    [HarmonyPrefix]
    private static void Prefix(Server __instance, out EolTickState __state)
    {
        __state = default;

        try
        {
            if (!EolControl.ShouldHold(__instance.Pointer))
                return;
        }
        catch (Exception)
        {
            return;
        }

        __state.Held = true;
        __state.TimeToBrake = __instance.timeToBrake;
        __state.EolTime = __instance.eolTime;

        if (__instance.timeToBrake < int.MaxValue)
            __instance.timeToBrake++;
        if (__instance.eolTime < int.MaxValue)
            __instance.eolTime++;
    }

    [HarmonyPostfix]
    private static void Postfix(Server __instance, EolTickState __state)
    {
        if (!__state.Held)
            return;

        __instance.timeToBrake = __state.TimeToBrake;
        __instance.eolTime = __state.EolTime;
    }
}

[HarmonyPatch(typeof(NetworkSwitch), nameof(NetworkSwitch.TickTimer))]
internal static class NetworkSwitchTickTimerPatch
{
    [HarmonyPrefix]
    private static void Prefix(NetworkSwitch __instance, out EolTickState __state)
    {
        __state = default;

        try
        {
            if (!EolControl.ShouldHold(__instance.Pointer))
                return;
        }
        catch (Exception)
        {
            return;
        }

        __state.Held = true;
        __state.TimeToBrake = __instance.timeToBrake;
        __state.EolTime = __instance.eolTime;

        if (__instance.timeToBrake < int.MaxValue)
            __instance.timeToBrake++;
        if (__instance.eolTime < int.MaxValue)
            __instance.eolTime++;
    }

    [HarmonyPostfix]
    private static void Postfix(NetworkSwitch __instance, EolTickState __state)
    {
        if (!__state.Held)
            return;

        __instance.timeToBrake = __state.TimeToBrake;
        __instance.eolTime = __state.EolTime;
    }
}

[HarmonyPatch(typeof(Server), "UpdateServerScreenUI")]
internal static class ServerEolUiPatch
{

    [HarmonyPostfix]
    private static void Postfix(Server __instance)
    {
        var screen = __instance.txtServerScreen;
        if (screen == null) return;

        string text = screen.text;
        if (string.IsNullOrEmpty(text)) return;

        if (EolControl.Disabled)
        {
            string updated = EolDisplay.Replace(text);
            if (!string.Equals(updated, text, StringComparison.Ordinal))
                screen.text = updated;
            return;
        }

        if (text.IndexOf(EolDisplay.InfinityLine, StringComparison.Ordinal) >= 0)
            __instance.lastDisplayedEolMinute = -1;
    }
}

[HarmonyPatch(typeof(NetworkSwitch), "UpdateScreenUI")]
internal static class NetworkSwitchEolUiPatch
{

    [HarmonyPostfix]
    private static void Postfix(NetworkSwitch __instance)
    {
        var screen = __instance.txtScreen;
        if (screen == null) return;

        string text = screen.text;
        if (string.IsNullOrEmpty(text)) return;

        if (EolControl.Disabled)
        {
            string updated = EolDisplay.Replace(text);
            if (!string.Equals(updated, text, StringComparison.Ordinal))
                screen.text = updated;
            return;
        }

        if (text.IndexOf(EolDisplay.InfinityLine, StringComparison.Ordinal) >= 0)
            __instance.lastDisplayedEolMinute = -1;
    }
}

internal static class EolDisplay
{
    internal const string Marker = "EOL:";
    internal const string InfinityLine = "EOL: Infinity";

    internal static string Replace(string text)
    {
        int start = text.IndexOf(Marker, StringComparison.Ordinal);
        if (start < 0) return text;

        int end = text.IndexOf('\n', start);
        if (end < 0) end = text.Length;

        return text.Substring(0, start) + InfinityLine + text.Substring(end);
    }
}
