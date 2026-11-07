using System;
using MelonLoader;
using PtrPack.Ui;
using PtrPack.Modules;

[assembly: MelonInfo(typeof(PtrPack.Mod.PtrPackMod), PtrPack.Mod.ModInfo.Name, PtrPack.Mod.ModInfo.Version, PtrPack.Mod.ModInfo.Author)]
[assembly: MelonGame("Waseku", "Data Center")]

namespace PtrPack.Mod;

public sealed class PtrPackMod : MelonMod
{
    private ModuleHost? _host;

    public override void OnInitializeMelon()
    {
        LoggerInstance.Msg($"{ModInfo.Name} {ModInfo.Version} initializing (IL2CPP).");

        try
        {
            _host = new ModuleHost(LoggerInstance, new WindowManager());
            foreach (var module in CreateModules())
                _host.Register(module);
            _host.Initialize();
        }
        catch (Exception exception)
        {
            LoggerInstance.Error($"PtrPack initialization failed: {exception}");
            _host = null;
        }
    }

    public override void OnUpdate() => _host?.OnUpdate();
    public override void OnGUI() => _host?.DrawGui();
    public override void OnSceneWasInitialized(int buildIndex, string sceneName) => _host?.OnSceneInitialized(sceneName);
    public override void OnSceneWasUnloaded(int buildIndex, string sceneName) => _host?.OnSceneUnloaded(sceneName);

    public override void OnDeinitializeMelon()
    {
        _host?.Shutdown();
        _host = null;
    }

    private static PtrModule[] CreateModules() => new PtrModule[]
    {
        new EolControlModule(),
    };
}
