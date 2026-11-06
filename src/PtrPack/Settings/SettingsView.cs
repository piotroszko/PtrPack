using System;
using Il2Cpp;
using Il2CppTMPro;
using PtrPack.Ui;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PtrPack.Settings;

internal static class SettingsView
{
    private const string SectionTemplateName = "TXT (1)";
    private const string ToggleTemplateName = "AutoSaveOnOff";
    private const string SliderTemplateName = "Evaluation FPS budget";

    internal static bool Build(
        GameObject panel,
        ModSettings settings,
        Action<string> warning,
        ButtonExtended? buttonTemplate = null)
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
        var inputSource = UiFactory.FindSceneComponent<TMP_InputField>();

        var staging = new GameObject("PtrPackSettingsTemplates");
        staging.transform.SetParent(panel.transform, false);
        staging.SetActive(false);
        var dropdownSource = content.GetComponentInChildren<TMP_Dropdown>(true)
            ?? UiFactory.FindSceneComponent<TMP_Dropdown>();

        var sectionTemplate = CloneTemplate(sectionSource.gameObject, staging.transform);
        var toggleTemplate = toggleSource != null
            ? CloneTemplate(toggleSource.gameObject, staging.transform)
            : null;
        var sliderTemplate = sliderSource != null
            ? CloneTemplate(sliderSource.gameObject, staging.transform)
            : null;
        var durationRowTemplate = toggleTemplate;
        var durationLabelTemplate = sectionTemplate.GetComponentInChildren<TextMeshProUGUI>(true);
        var durationInputTemplate = inputSource != null
            ? CloneTemplate(inputSource.gameObject, staging.transform)
            : null;
        var durationDropdownTemplate = dropdownSource != null
            ? CloneTemplate(dropdownSource.gameObject, staging.transform)
            : null;
        var durationButtonSource = FindButton(content)
            ?? buttonTemplate?.gameObject
            ?? UiFactory.FindSceneComponent<ButtonExtended>()?.gameObject;
        var durationButtonTemplate = durationButtonSource != null
            ? CloneTemplate(durationButtonSource, staging.transform)
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
                    case DurationActionSetting action:
                        AddDurationAction(
                            content,
                            durationRowTemplate,
                            durationLabelTemplate,
                            durationInputTemplate,
                            durationDropdownTemplate,
                            durationButtonTemplate,
                            action,
                            warning);
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

        ClearEvent(toggle.onValueChanged);
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

        ClearEvent(slider.onValueChanged);
        slider.onValueChanged.AddListener(
            (UnityAction<float>)(Action<float>)(raw =>
            {
                int value = Mathf.RoundToInt(raw);
                setting.Apply(value);
                if (valueLabel != null)
                    valueLabel.text = setting.FormatValue(value);
            }));
    }

    private static void AddDurationAction(
        Transform content,
        GameObject? rowTemplate,
        TextMeshProUGUI? labelTemplate,
        GameObject? inputTemplate,
        GameObject? dropdownTemplate,
        GameObject? buttonTemplate,
        DurationActionSetting setting,
        Action<string> warning)
    {
        if (rowTemplate == null
            || labelTemplate == null
            || inputTemplate == null
            || dropdownTemplate == null
            || buttonTemplate == null)
        {
            warning($"No UI template for action setting '{setting.Key}'.");
            return;
        }

        var row = UnityEngine.Object.Instantiate(rowTemplate, content);
        row.name = "Setting_" + setting.Key;
        row.SetActive(true);
        SetHeight(row, 50f);

        for (int i = row.transform.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(row.transform.GetChild(i).gameObject);

        var layout = row.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
        {
            var existing = row.GetComponent<LayoutGroup>();
            if (existing != null)
                existing.enabled = false;

            layout = row.AddComponent<HorizontalLayoutGroup>();
        }

        var padding = new RectOffset();
        padding.left = 16;
        padding.right = 16;
        layout.padding = padding;
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var label = UnityEngine.Object.Instantiate(labelTemplate, row.transform);
        label.name = "Label";
        label.text = setting.Label;
        label.fontSize = 18f;
        label.alignment = TextAlignmentOptions.Left;
        label.gameObject.SetActive(true);
        SetLayoutSize(label.gameObject, 120f, 1f, 30f);

        var input = UnityEngine.Object.Instantiate(inputTemplate, row.transform)
            .GetComponent<TMP_InputField>();
        if (input == null)
        {
            UnityEngine.Object.Destroy(row);
            warning($"Input template for action setting '{setting.Key}' is invalid.");
            return;
        }

        input.name = "Amount";
        input.gameObject.SetActive(true);
        input.interactable = true;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.characterLimit = 6;
        ClearEvent(input.onValueChanged);
        ClearEvent(input.onEndEdit);
        ClearEvent(input.onSubmit);
        ClearEvent(input.onSelect);
        ClearEvent(input.onDeselect);
        ClearEvent(input.onTextSelection);
        ClearEvent(input.onEndTextSelection);
        input.SetTextWithoutNotify(setting.DefaultValue.ToString());
        SetLayoutSize(input.gameObject, 80f, 0f, 32f);

        var dropdown = UnityEngine.Object.Instantiate(dropdownTemplate, row.transform)
            .GetComponent<TMP_Dropdown>();
        if (dropdown == null)
        {
            UnityEngine.Object.Destroy(row);
            warning($"Dropdown template for action setting '{setting.Key}' is invalid.");
            return;
        }

        dropdown.name = "Unit";
        dropdown.gameObject.SetActive(true);
        dropdown.interactable = true;
        dropdown.ClearOptions();

        var options = new Il2CppSystem.Collections.Generic.List<string>();
        for (int i = 0; i < setting.UnitLabels.Length; i++)
            options.Add(setting.UnitLabels[i]);

        dropdown.AddOptions(options);
        dropdown.SetValueWithoutNotify(setting.DefaultUnitIndex);
        dropdown.RefreshShownValue();
        ClearEvent(dropdown.onValueChanged);
        SetLayoutSize(dropdown.gameObject, 140f, 0f, 32f);

        var button = UnityEngine.Object.Instantiate(buttonTemplate, row.transform);
        button.name = "Apply";
        button.SetActive(true);

        Action apply = () =>
        {
            if (!int.TryParse(input.text, out int amount))
                amount = setting.DefaultValue;

            amount = Math.Clamp(amount, setting.Min, setting.Max);
            input.SetTextWithoutNotify(amount.ToString());

            int unit = Math.Clamp(dropdown.value, 0, setting.UnitSeconds.Length - 1);
            long totalSeconds = (long)amount * setting.UnitSeconds[unit];
            setting.Apply(totalSeconds > int.MaxValue ? int.MaxValue : (int)totalSeconds);
        };

        if (!WireButton(button, apply))
        {
            UnityEngine.Object.Destroy(row);
            warning($"Button template for action setting '{setting.Key}' is invalid.");
            return;
        }

        var buttonLabel = FindTextAt(button.transform, 0);
        if (buttonLabel != null)
            buttonLabel.text = setting.ButtonText;

        SetLayoutSize(button, 90f, 0f, 32f);

        DisableLocalisation(row);
    }

    private static GameObject? FindButton(Transform root)
    {
        var extended = root.GetComponentInChildren<ButtonExtended>(true);
        if (extended != null)
            return extended.gameObject;

        var button = root.GetComponentInChildren<Button>(true);
        return button != null ? button.gameObject : null;
    }

    private static bool WireButton(GameObject target, Action action)
    {
        var extended = target.GetComponentInChildren<ButtonExtended>(true);
        if (extended != null)
        {
            extended.interactable = true;
            ClearEvent(extended.onClick);
            ClearEvent(extended.onSecondClick);
            extended.onClick.AddListener((UnityAction)action);
            return true;
        }

        var button = target.GetComponentInChildren<Button>(true);
        if (button == null)
            return false;

        button.interactable = true;
        ClearEvent(button.onClick);
        button.onClick.AddListener((UnityAction)action);
        return true;
    }

    private static void SetLayoutSize(
        GameObject target,
        float preferredWidth,
        float flexibleWidth,
        float height)
    {
        var element = target.GetComponent<LayoutElement>();
        if (element == null)
            element = target.AddComponent<LayoutElement>();

        element.minWidth = preferredWidth;
        element.preferredWidth = preferredWidth;
        element.flexibleWidth = flexibleWidth;
        element.minHeight = height;
        element.preferredHeight = height;
        element.flexibleHeight = 0f;
    }

    private static void ClearEvent(UnityEventBase unityEvent)
    {
        if (unityEvent == null) return;
        for (int i = unityEvent.GetPersistentEventCount() - 1; i >= 0; i--)
            unityEvent.SetPersistentListenerState(i, UnityEventCallState.Off);

        unityEvent.RemoveAllListeners();
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
