using System;
using MelonLoader;
using PtrPack.Mod;
using PtrPack.Settings;

namespace PtrPack.Modules;

internal sealed class EolControlModule : PtrModule
{
    private const string DisableKey = "DisableEol";
    private const string MultiplierKey = "EolLifetimeMultiplier";

    private HarmonyLib.Harmony? _harmony;
    private ToggleSetting? _disabled;
    private IntSliderSetting? _multiplier;

    internal override string Id => "EolControl";
    internal override string DisplayName => "EOL";

    internal override void OnRegisterSettings(ModSettings settings)
    {
        var section = settings.AddSection(Id, DisplayName, Context.Config.Category);
        _disabled = section.AddToggle(DisableKey, "Disable EOL", false, EolControl.SetDisabled);
        _multiplier = section.AddSlider(
            MultiplierKey,
            "EOL lifetime multiplier",
            1,
            50,
            1,
            "{0}x",
            EolControl.SetMultiplier);
        section.AddDurationAction(
            "AddTime",
            "Add lifetime to all devices",
            "Add",
            1,
            9999,
            1,
            new[] { "Seconds", "Minutes", "Hours" },
            new[] { 1, 60, 3600 },
            2,
            seconds =>
            {
                int count = EolControl.AddTimeToAll(seconds);
                Context.Log.Msg($"Added {seconds}s to {count} device(s).");
            });
    }

    internal override void OnInitialize()
    {
        if (_disabled == null || _multiplier == null)
            throw new InvalidOperationException("EOL settings were not registered.");

        EolControl.Configure(_disabled.Value, _multiplier.Value);

        try
        {
            _harmony = new HarmonyLib.Harmony("PtrPack.EolControl");
            _harmony.PatchAll(typeof(ServerTickTimerPatch));
            _harmony.PatchAll(typeof(NetworkSwitchTickTimerPatch));
        }
        catch (Exception exception)
        {
            Context.Log.Error("EOL patches could not be installed; EOL settings are inactive.", exception);
            _harmony?.UnpatchSelf();
            _harmony = null;
            return;
        }

        try
        {
            _harmony.PatchAll(typeof(ServerEolUiPatch));
            _harmony.PatchAll(typeof(NetworkSwitchEolUiPatch));
        }
        catch (Exception exception)
        {
            Context.Log.Warning($"EOL UI patches could not be installed: {exception}");
        }

        Context.Log.Msg(
            $"EOL control active (disabled={_disabled.Value}, multiplier={_multiplier.Value}x).");
    }

    internal override void OnGameplayEnter() => EolControl.ResetTicks();
    internal override void OnGameplayExit() => EolControl.ResetTicks();

    internal override void OnShutdown()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
        EolControl.Configure(false, 1);
    }
}
