using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PtrPack.Ui;

internal static class UiFactory
{
    internal static T? FindSceneComponent<T>() where T : Component
    {
        var all = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < all.Length; i++)
        {
            var candidate = all[i];
            if (candidate == null) continue;
            if (candidate.gameObject.scene.IsValid()) return candidate;
        }

        return null;
    }

    internal static Transform? FindChild(Transform root, string name)
    {
        if (root.name == name) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChild(root.GetChild(i), name);
            if (found != null) return found;
        }

        return null;
    }
    internal static GameObject CreatePanel(Transform parent, GameObject template, string name, Color color)
    {
        var panel = Object.Instantiate(template, parent);
        panel.name = name;

        for (int i = panel.transform.childCount - 1; i >= 0; i--)
            Object.Destroy(panel.transform.GetChild(i).gameObject);

        var rect = panel.GetComponent<RectTransform>();
        Stretch(rect, Vector2.zero, Vector2.zero);

        var image = panel.GetComponent<Image>();
        if (image == null)
            image = panel.AddComponent<Image>();

        image.color = color;
        return panel;
    }

    internal static TextMeshProUGUI CreateText(
        Transform parent,
        TextMeshProUGUI template,
        string text,
        float fontSize,
        TextAlignmentOptions alignment,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        var clone = Object.Instantiate(template, parent);
        var localised = clone.GetComponent<LocalisedText>();
        if (localised != null)
            localised.enabled = false;

        clone.name = "Text";
        clone.text = text;
        clone.fontSize = fontSize;
        clone.alignment = alignment;
        clone.color = Color.white;

        Stretch(clone.rectTransform, offsetMin, offsetMax);
        return clone;
    }

    internal static ButtonExtended CreateButton(ButtonExtended template, Transform parent, string name)
    {
        var clone = Object.Instantiate(template, parent);
        clone.name = name;
        clone.onClick.RemoveAllListeners();
        return clone;
    }

    internal static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    internal static void SetTopRight(RectTransform rect, Vector2 size, Vector2 offset)
    {
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.sizeDelta = size;
        rect.anchoredPosition = offset;
    }

    internal static void SetTopLeft(RectTransform rect, Vector2 size, Vector2 offset)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = offset;
    }

    internal static bool SetButtonLabel(ButtonExtended button, string text)
    {
        bool any = false;

        var localised = button.GetComponentsInChildren<LocalisedText>(true);
        for (int i = 0; i < localised.Length; i++)
        {
            if (localised[i] == null) continue;
            localised[i].enabled = false;
            Object.Destroy(localised[i]);
        }

        var labels = button.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] == null) continue;
            any = true;
            labels[i].text = text;
        }
        return any;
    }
}
