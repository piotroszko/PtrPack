using System;
using System.Collections.Generic;
using MelonLoader;
using PtrPack.Diagnostics;
using PtrPack.Game;
using PtrPack.Settings;
using PtrPack.Ui;

namespace PtrPack.Mod;

internal sealed class ModuleHost
{
    private readonly MelonLogger.Instance _logger;
    private readonly WindowManager _windows;
    private readonly ModSettings _settings = new ModSettings();
    private readonly PauseMenuSettings _pauseMenuSettings;
    private readonly List<PtrModule> _modules = new List<PtrModule>();
    private readonly HashSet<PtrModule> _faulted = new HashSet<PtrModule>();
    private bool _gameplayActive;

    internal ModuleHost(MelonLogger.Instance logger, WindowManager windows)
    {
        _logger = logger;
        _windows = windows;
        _pauseMenuSettings = new PauseMenuSettings(_settings, new ModuleLog(_logger, "Settings"));
    }

    internal void Register(PtrModule module)
    {
        if (module == null)
            throw new ArgumentNullException(nameof(module));

        foreach (var existing in _modules)
        {
            if (string.Equals(existing.Id, module.Id, StringComparison.Ordinal))
                throw new InvalidOperationException($"Duplicate module id '{module.Id}'.");
        }

        var context = new ModuleContext(
            new ModuleLog(_logger, module.Id),
            new ModuleConfig(module),
            _windows,
            _settings);

        module.Attach(context);
        _modules.Add(module);
    }

    internal void Initialize()
    {
        _logger.Msg($"[{ModInfo.Name}] {_modules.Count} module(s) registered.");

        foreach (var module in _modules)
        {
            if (!module.Context.Config.IsEnabled)
            {
                module.Context.Log.Msg("Disabled by config.");
                continue;
            }

            Invoke(module, nameof(PtrModule.OnRegisterSettings), () => module.OnRegisterSettings(_settings));
        }

        _logger.Msg($"[{ModInfo.Name}] {_settings.Sections.Count} settings section(s) registered.");

        foreach (var module in _modules)
        {
            if (!module.Context.Config.IsEnabled) continue;
            Invoke(module, nameof(PtrModule.OnInitialize), module.OnInitialize);
        }
    }

    internal void OnUpdate()
    {
        _pauseMenuSettings.OnUpdate();

        foreach (var module in _modules)
        {
            if (!module.Context.Config.IsEnabled) continue;
            Invoke(module, nameof(PtrModule.OnUpdate), module.OnUpdate);
        }
    }

    internal void OnSceneInitialized(string sceneName)
    {
        if (!string.Equals(sceneName, SceneNames.Gameplay, StringComparison.Ordinal))
            return;

        if (_gameplayActive) return;

        _gameplayActive = true;
        _pauseMenuSettings.OnGameplayEnter();
        foreach (var module in _modules)
            module.Context.InGameplay = true;

        _logger.Msg($"[{ModInfo.Name}] Gameplay scene '{sceneName}' entered.");
        foreach (var module in _modules)
        {
            if (!module.Context.Config.IsEnabled) continue;
            Invoke(module, nameof(PtrModule.OnGameplayEnter), module.OnGameplayEnter);
        }
    }

    internal void OnSceneUnloaded(string sceneName)
    {
        if (!string.Equals(sceneName, SceneNames.Gameplay, StringComparison.Ordinal))
            return;

        ExitGameplay(sceneName);
    }

    private void ExitGameplay(string sceneName)
    {
        if (!_gameplayActive) return;

        _gameplayActive = false;
        _pauseMenuSettings.OnGameplayExit();
        foreach (var module in _modules)
            module.Context.InGameplay = false;

        _logger.Msg($"[{ModInfo.Name}] Left gameplay scene ('{sceneName}').");
        foreach (var module in _modules)
        {
            if (!module.Context.Config.IsEnabled) continue;
            Invoke(module, nameof(PtrModule.OnGameplayExit), module.OnGameplayExit);
        }
    }

    internal void DrawGui()
    {
        foreach (var module in _modules)
        {
            if (!module.Context.Config.IsEnabled) continue;
            Invoke(module, nameof(PtrModule.OnGui), module.OnGui);
        }

        _windows.DrawAll();
    }

    internal void Shutdown()
    {
        _pauseMenuSettings.OnGameplayExit();

        foreach (var module in _modules)
        {
            if (!module.Context.Config.IsEnabled) continue;
            Invoke(module, nameof(PtrModule.OnShutdown), module.OnShutdown);
        }

        _windows.CloseAll();
    }

    private void Invoke(PtrModule module, string phase, Action action)
    {
        if (_faulted.Contains(module)) return;

        try
        {
            action();
        }
        catch (Exception exception)
        {
            _faulted.Add(module);
            module.Context.Log.Error($"{phase} failed; module disabled for this session.", exception);
        }
    }
}
