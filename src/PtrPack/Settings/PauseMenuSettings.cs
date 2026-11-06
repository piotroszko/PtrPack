using System;
using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using PtrPack.Diagnostics;
using PtrPack.Ui;
using UnityEngine;

using Object = UnityEngine.Object;

namespace PtrPack.Settings;

internal sealed class PauseMenuSettings
{
    private const string TabName = "PtrPackTab";
    private const string PanelName = "PtrPackPanel";
    private const float SaveDelaySeconds = 0.5f;

    private readonly ModSettings _settings;
    private readonly ModuleLog _log;

    private PauseMenu? _pause;
    private PauseMenu_TabButton? _tab;
    private GameObject? _panel;
    private bool _active;
    private bool _needsDefault;
    private bool _warned;
    private bool _menuOpen;
    private bool _saveScheduled;
    private float _saveAt;

    internal PauseMenuSettings(ModSettings settings, ModuleLog log)
    {
        _settings = settings;
        _log = log;
    }

    internal void OnGameplayEnter()
    {
        _active = true;
        Reset();
    }

    internal void OnGameplayExit()
    {
        _active = false;
        SaveIfDirty();
        Reset();
    }

    internal void OnUpdate()
    {
        if (!_active) return;

        UpdateMenuState();
        if (_saveScheduled && Time.unscaledTime >= _saveAt)
        {
            _saveScheduled = false;
            SaveIfDirty();
        }

        if (_needsDefault && _pause != null && _pause.mainPauseMenuTabGroup != null)
        {
            var group = _pause.mainPauseMenuTabGroup;
            if (group.tabButtons != null && _tab != null && group.tabButtons.Contains(_tab))
            {
                if (group.selectedTab == _tab && _pause.systemMenuTab != null)
                    group.OnTabSelected(_pause.systemMenuTab);

                _needsDefault = false;
            }
        }

        if (_pause != null && _panel != null && _pause.mainPauseMenuTabGroup != null) return;

        var pause = UiFactory.FindSceneComponent<PauseMenu>();
        if (pause == null)
        {
            if (!_warned)
            {
                _warned = true;
                _log.Warning("PauseMenu was not found; settings tab not installed.");
            }

            return;
        }

        _pause = pause;
        if (pause.mainPauseMenuTabGroup == null || pause.systemMenuTab == null)
        {
            if (!_warned)
            {
                _warned = true;
                _log.Warning("Pause menu tab references are not ready.");
            }

            return;
        }

        if (pause.mainPauseMenuTabGroup.transform.Find(TabName) != null)
            return;

        Install(pause);

        UpdateMenuState();
    }

    private void Install(PauseMenu pause)
    {
        var group = pause.mainPauseMenuTabGroup;
        var templateTab = pause.systemMenuTab;
        var templateText = templateTab?.GetComponentInChildren<TextMeshProUGUI>(true);
        if (group == null || templateTab == null || templateText == null)
        {
            _log.Warning("Pause menu tab templates are unavailable.");
            return;
        }

        var tab = Object.Instantiate(templateTab, templateTab.transform.parent);
        tab.name = TabName;
        tab.gameObject.SetActive(false);
        tab.tabGroup = group;

        var label = tab.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            var localised = label.GetComponent<LocalisedText>();
            if (localised != null)
                localised.enabled = false;

            label.text = "PtrPack";
        }

        var panels = group.objectsToSwap;
        if (panels == null || panels.Count == 0)
        {
            Object.Destroy(tab.gameObject);
            _log.Warning("Pause menu has no panel list; settings tab not installed.");
            return;
        }

        var panelHost = panels[0].transform.parent;
        var settingsTemplate = UiFactory.FindChild(panelHost, "Gameplay")?.gameObject;
        if (settingsTemplate == null)
        {
            Object.Destroy(tab.gameObject);
            _log.Warning("Pause menu settings panel template 'Gameplay' was not found.");
            return;
        }

        var panel = Object.Instantiate(settingsTemplate, panelHost);
        panel.name = PanelName;
        UiFactory.Stretch(panel.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        if (!SettingsView.Build(panel, _settings, _log.Warning))
        {
            Object.Destroy(panel);
            Object.Destroy(tab.gameObject);
            _log.Warning("Pause menu settings view could not be built.");
            return;
        }

        panel.SetActive(false);
        panels.Add(panel);
        _tab = tab;
        _needsDefault = true;

        tab.gameObject.SetActive(true);
        _panel = panel;
        _log.Msg($"Installed 'PtrPack' settings tab ({_settings.Sections.Count} section(s)).");
    }

    private void UpdateMenuState()
    {
        if (_pause == null) return;

        var ui = _pause.pauseMenuUI;
        if (ui == null) return;

        bool open = ui.activeInHierarchy
                    && (_pause.pauseMenuCanvas == null || _pause.pauseMenuCanvas.enabled);
        if (open == _menuOpen) return;

        _menuOpen = open;
        if (open)
        {
            _saveScheduled = false;
            return;
        }

        _saveScheduled = true;
        _saveAt = Time.unscaledTime + SaveDelaySeconds;
    }

    private void SaveIfDirty()
    {
        if (!_settings.IsDirty) return;

        try
        {
            MelonPreferences.Save();
            _settings.ClearDirty();
            _log.Msg("Saved settings.");
        }
        catch (Exception exception)
        {
            _log.Error("Settings could not be saved.", exception);
        }
    }

    private void Reset()
    {
        _pause = null;
        _tab = null;
        _panel = null;
        _needsDefault = false;
        _warned = false;
        _menuOpen = false;
        _saveScheduled = false;
        _saveAt = 0f;
    }
}
