using System;
using Il2Cpp;
using PtrPack.Mod;
using PtrPack.Ui;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PtrPack.Modules;

internal sealed class ComputerAppModule : PtrModule
{
    private const string IconName = "Icon PtrPack bcg";
    private const string PanelName = "PtrPackApp";

    private ComputerShop? _shop;
    private GameObject? _panel;
    private bool _warned;

    internal override string Id => "ComputerApp";
    internal override string DisplayName => "Computer App";

    internal override void OnGameplayEnter()
    {
        _shop = null;
        _panel = null;
        _warned = false;
    }

    internal override void OnUpdate()
    {
        if (!Context.InGameplay) return;

        if (_panel != null)
        {
            if (_panel.activeSelf && _shop != null && _shop.mainScreen != null && _shop.mainScreen.activeSelf)
                _panel.SetActive(false);
            return;
        }

        if (_shop == null)
            _shop = UiFactory.FindSceneComponent<ComputerShop>();

        if (_shop == null || _shop.mainScreen == null)
        {
            if (!_warned)
            {
                _warned = true;
                Context.Log.Warning("ComputerShop was not found; app not installed.");
            }

            return;
        }

        Install(_shop);
    }

    private void Install(ComputerShop shop)
    {
        var main = shop.mainScreen;
        var grid = main.transform.Find("Button Grid");
        if (grid == null)
        {
            Context.Log.Warning("Computer desktop grid was not found.");
            return;
        }

        if (grid.Find(IconName) != null)
            return;

        var templateIcon = grid.Find("Icon Shop bcg");
        if (templateIcon == null)
        {
            if (grid.childCount == 0)
                return;

            templateIcon = grid.GetChild(0);
        }

        var templateText = templateIcon.GetComponentInChildren<TextMeshProUGUI>(true);
        if (templateText == null)
        {
            Context.Log.Warning("Computer icon text template was not found.");
            return;
        }

        var icon = UnityEngine.Object.Instantiate(templateIcon.gameObject, grid);
        icon.name = IconName;

        var label = icon.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
            label.text = "PtrPack";

        var button = icon.GetComponentInChildren<ButtonExtended>(true);
        if (button == null)
        {
            UnityEngine.Object.Destroy(icon);
            Context.Log.Warning("Computer icon button was not found.");
            return;
        }

        var panel = UiFactory.CreatePanel(
            main.transform.parent,
            main,
            PanelName,
            new Color(0.02f, 0.04f, 0.06f, 0.98f));

        UiFactory.Stretch(
            panel.GetComponent<RectTransform>(),
            new Vector2(10f, 10f),
            new Vector2(-10f, -10f));

        UiFactory.CreateText(
            panel.transform,
            templateText,
            "PtrPack\n\nPC app view test.\nOpened from the desktop icon.",
            32f,
            TextAlignmentOptions.TopLeft,
            new Vector2(60f, 60f),
            new Vector2(-60f, -60f));

        panel.SetActive(false);

        var returnTemplate = shop.networkMapScreen != null
            ? shop.networkMapScreen.transform.Find("Button Return")
            : main.transform.Find("Button Return");

        var returnButton = returnTemplate != null
            ? returnTemplate.GetComponent<ButtonExtended>()
            : null;

        if (returnButton != null)
        {
            var back = UiFactory.CreateButton(returnButton, panel.transform, "Button Return");
            UiFactory.SetTopRight(
                back.GetComponent<RectTransform>(),
                new Vector2(100f, 40f),
                new Vector2(-20f, -20f));

            back.onClick.AddListener((UnityAction)(Action)(() => Close(shop, panel)));
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener((UnityAction)(Action)(() => Open(shop, panel)));

        _panel = panel;
        Context.Log.Msg("Installed 'PtrPack' icon and app view on the computer desktop.");
    }

    private void Open(ComputerShop shop, GameObject panel)
    {
        if (shop.mainScreen == null)
            return;

        shop.ButtonReturnMainScreen();
        shop.mainScreen.SetActive(false);
        panel.SetActive(true);
        Context.Log.Msg("Opened PtrPack computer app.");
    }

    private void Close(ComputerShop shop, GameObject panel)
    {
        panel.SetActive(false);
        if (shop.mainScreen != null)
            shop.mainScreen.SetActive(true);

        Context.Log.Msg("Closed PtrPack computer app.");
    }
}
