using Il2Cpp;
using PtrPack.Mod;
using PtrPack.Ui;
using Il2CppTMPro;
using UnityEngine;

namespace PtrPack.Modules;

internal sealed class PauseMenuTabModule : PtrModule
{
    private const string TabName = "PtrPackTab";
    private const string PanelName = "PtrPackPanel";

    private PauseMenu? _pause;
    private PauseMenu_TabButton? _tab;
    private GameObject? _panel;
    private bool _needsDefault;
    private bool _warned;

    internal override string Id => "PauseMenuTab";
    internal override string DisplayName => "Pause Menu Tab";

    internal override void OnGameplayEnter()
    {
        _pause = null;
        _tab = null;
        _panel = null;
        _needsDefault = false;
        _warned = false;
    }

    internal override void OnUpdate()
    {
        if (!Context.InGameplay) return;

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
                Context.Log.Warning("PauseMenu was not found; tab not installed.");
            }

            return;
        }

        _pause = pause;
        if (pause.mainPauseMenuTabGroup == null || pause.systemMenuTab == null)
        {
            if (!_warned)
            {
                _warned = true;
                Context.Log.Warning("Pause menu tab references are not ready.");
            }

            return;
        }

        if (pause.mainPauseMenuTabGroup.transform.Find(TabName) != null)
            return;

        Install(pause);
    }

    private void Install(PauseMenu pause)
    {
        var group = pause.mainPauseMenuTabGroup;
        var templateTab = pause.systemMenuTab;
        var templateText = templateTab.GetComponentInChildren<TextMeshProUGUI>(true);
        if (group == null || templateTab == null || templateText == null)
        {
            Context.Log.Warning("Pause menu tab templates are unavailable.");
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
            Context.Log.Warning("Pause menu has no panel list; tab not installed.");
            return;
        }

        var panel = UiFactory.CreatePanel(
            panels[0].transform.parent,
            panels[0],
            PanelName,
            new Color(0.02f, 0.04f, 0.06f, 0.98f));

        UiFactory.CreateText(
            panel.transform,
            templateText,
            "PtrPack\n\nPause menu tab test view.\nAdded by a PtrPack module.",
            28f,
            TextAlignmentOptions.TopLeft,
            new Vector2(60f, 50f),
            new Vector2(-60f, -50f));

        panel.SetActive(false);
        panels.Add(panel);
        _tab = tab;
        _needsDefault = true;

        tab.gameObject.SetActive(true);
        _panel = panel;
        Context.Log.Msg("Installed 'PtrPack' tab in the pause menu.");

    }
}
