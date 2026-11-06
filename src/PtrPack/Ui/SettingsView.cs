using System;
using Il2Cpp;
using Il2CppTMPro;
using PtrPack.Settings;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PtrPack.Ui;

internal static class SettingsView
{
    private const string SectionTemplateName = "TXT (1)";
    private const string ToggleTemplateName = "AutoSaveOnOff";
    private const string SliderTemplateName = "Evaluation FPS budget";

    internal static bool Build(GameObject panel, ModSettings settings, Action<string> warning)
    {
        var scroll = panel.GetComponentInChildren<ScrollRect>(true);
        if (scroll == null || scroll.content == null)
        {
            warning("Settings view has no ScrollRect content.");
            return false;
        }

        var content = scroll.content;
        var sectionSource = FindDirect(content, SectionTemplateName);
        if (sectionSource == null)
        {
            warning($"Settings section template '{SectionTemplateName}' was not found.");
            return false;
        }

        var toggleSource = FindDirect(content, ToggleTemplateName);
        var sliderSource = FindDirect(content, SliderTemplateName);

        var staging = new GameObject("PtrPackSettingsTemplates");
        staging.transform.SetParent(panel.transform, false);
        staging.SetActive(false);

        var sectionTemplate = CloneTemplate(sectionSource.gameObject, staging.transform);
        var toggleTemplate = toggleSource != null
            ? CloneTemplate(toggleSource.gameObject, staging.transform)
            : null;
        var sliderTemplate = sliderSource != null
            ? CloneTemplate(sliderSource.gameObject, staging.transform)
            : null;

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var child = content.GetChild(i).gameObject;
            child.SetActive(false);
            UnityEngine.Object.Destroy(child);
        }

        EnsureLayout(content);

        if (settings.Sections.Count == 0)
        {
            AddHeader(content, sectionTemplate, "No module settings registered.");
            return true;
        }

        for (int i = 0; i < settings.Sections.Count; i++)
        {
            var section = settings.Sections[i];
            AddHeader(content, sectionTemplate, section.Title);

            for (int j = 0; j < section.Settings.Count; j++)
            {
                var setting = section.Settings[j];
                switch (setting)
                {
                    case ToggleSetting toggle when toggleTemplate != null:
                        AddToggle(content, toggleTemplate, toggle);
                        break;
                    case IntSliderSetting slider when sliderTemplate != null:
                        AddSlider(content, sliderTemplate, slider);
                        break;
                    default:
                        warning($"No UI template for setting '{setting.Key}'.");
                        break;
                }
            }
        }

        return true;
    }

    private static void AddHeader(Transform content, GameObject template, string text)
    {
        var row = UnityEngine.Object.Instantiate(template, content);
        row.name = "Section";
        row.SetActive(true);
        DisableLocalisation(row);

        var label = row.GetComponent<TextMeshProUGUI>();
        if (label != null)
        {
            label.text = text;
            label.fontSize = 24f;
        }

        SetHeight(row, 40f);
    }

    private static void AddToggle(Transform content, GameObject template, ToggleSetting setting)
    {
        var row = UnityEngine.Object.Instantiate(template, content);
        row.name = "Setting_" + setting.Key;
        row.SetActive(true);
        DisableLocalisation(row);
        SetHeight(row, 50f);

        var label = FindText(row.transform, "Text_AutoSaveOnOff") ?? FindTextAt(row.transform, 0);
        if (label != null)
            label.text = setting.Label;

        var toggle = row.GetComponentInChildren<Toggle>(true);
        if (toggle == null)
        {
            UnityEngine.Object.Destroy(row);
            return;
        }

        toggle.onValueChanged.RemoveAllListeners();
        toggle.SetIsOnWithoutNotify(setting.Entry.Value);
        toggle.onValueChanged.AddListener(
            (UnityAction<bool>)(Action<bool>)(value => setting.Apply(value)));
    }

    private static void AddSlider(Transform content, GameObject template, IntSliderSetting setting)
    {
        var row = UnityEngine.Object.Instantiate(template, content);
        row.name = "Setting_" + setting.Key;
        row.SetActive(true);
        DisableLocalisation(row);
        SetHeight(row, 40f);

        var label = FindText(row.transform, "EvalFPSTXT") ?? FindTextAt(row.transform, 0);
        if (label != null)
            label.text = setting.Label;

        var valueLabel = FindText(row.transform, "EvalFPSTXTValue") ?? FindTextAt(row.transform, 1);

        var slider = row.GetComponentInChildren<Slider>(true);
        if (slider == null)
        {
            UnityEngine.Object.Destroy(row);
            return;
        }

        slider.minValue = setting.Min;
        slider.maxValue = setting.Max;
        slider.wholeNumbers = true;
        slider.SetValueWithoutNotify(Mathf.Clamp(setting.Entry.Value, setting.Min, setting.Max));

        if (valueLabel != null)
            valueLabel.text = setting.FormatValue(setting.Entry.Value);

        slider.onValueChanged.RemoveAllListeners();
        slider.onValueChanged.AddListener(
            (UnityAction<float>)(Action<float>)(raw =>
            {
                int value = Mathf.RoundToInt(raw);
                setting.Apply(value);
                if (valueLabel != null)
                    valueLabel.text = setting.FormatValue(value);
            }));
    }

    private static void EnsureLayout(RectTransform content)
    {
        var layout = content.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        var fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }
    }

    private static GameObject CloneTemplate(GameObject source, Transform parent)
    {
        var clone = UnityEngine.Object.Instantiate(source, parent);
        clone.name = source.name + " Template";
        clone.SetActive(false);
        return clone;
    }

    private static void SetHeight(GameObject row, float height)
    {
        var rect = row.GetComponent<RectTransform>();
        if (rect != null)
        {
            var size = rect.sizeDelta;
            size.y = height;
            rect.sizeDelta = size;
        }

        var element = row.GetComponent<LayoutElement>();
        if (element == null)
            element = row.AddComponent<LayoutElement>();

        element.minHeight = height;
        element.preferredHeight = height;
    }

    private static void DisableLocalisation(GameObject root)
    {
        var localised = root.GetComponentsInChildren<LocalisedText>(true);
        for (int i = 0; i < localised.Length; i++)
            localised[i].enabled = false;
    }

    private static Transform? FindDirect(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (string.Equals(child.name, name, StringComparison.Ordinal))
                return child;
        }

        return null;
    }

    private static TextMeshProUGUI? FindText(Transform root, string name)
    {
        var child = FindDirect(root, name);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    private static TextMeshProUGUI? FindTextAt(Transform root, int index)
    {
        var texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        return index >= 0 && index < texts.Length ? texts[index] : null;
    }
}
