using System;
using MelonLoader;

namespace PtrPack.Settings;

internal abstract class SettingDefinition
{
    protected SettingDefinition(string key, string label)
    {
        Key = key;
        Label = label;
    }

    internal string Key { get; }
    internal string Label { get; }
}

internal sealed class ToggleSetting : SettingDefinition
{
    internal ToggleSetting(
        string key,
        string label,
        MelonPreferences_Entry<bool> entry,
        Action<bool>? changed)
        : base(key, label)
    {
        Entry = entry;
        Changed = changed;
    }

    internal MelonPreferences_Entry<bool> Entry { get; }
    internal Action<bool>? Changed { get; }

    internal bool Value => Entry.Value;

    internal void Apply(bool value)
    {
        if (Entry.Value == value) return;
        Entry.Value = value;
        Changed?.Invoke(value);
    }
}

internal sealed class IntSliderSetting : SettingDefinition
{
    internal IntSliderSetting(
        string key,
        string label,
        MelonPreferences_Entry<int> entry,
        int min,
        int max,
        string format,
        Action<int>? changed)
        : base(key, label)
    {
        Entry = entry;
        Min = min;
        Max = max;
        Format = string.IsNullOrEmpty(format) ? "{0}" : format;
        Changed = changed;
    }

    internal MelonPreferences_Entry<int> Entry { get; }
    internal int Min { get; }
    internal int Max { get; }
    internal int Value => Entry.Value;
    internal string Format { get; }
    internal Action<int>? Changed { get; }

    internal void Apply(int value)
    {
        value = Math.Clamp(value, Min, Max);
        if (Entry.Value == value) return;
        Entry.Value = value;
        Changed?.Invoke(value);
    }

    internal string FormatValue(int value) => string.Format(Format, value);
}
