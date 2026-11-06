using System;
using System.Collections.Generic;
using MelonLoader;

namespace PtrPack.Settings;

internal sealed class SettingsSection
{
    private readonly MelonPreferences_Category _category;
    private readonly Action _onChanged;
    private readonly List<SettingDefinition> _settings = new List<SettingDefinition>();

    internal SettingsSection(string id, string title, MelonPreferences_Category category, Action onChanged)
    {
        Id = id;
        Title = title;
        _category = category;
        _onChanged = onChanged;
    }

    internal string Id { get; }
    internal string Title { get; }
    internal IReadOnlyList<SettingDefinition> Settings => _settings;

    internal ToggleSetting AddToggle(
        string key,
        string label,
        bool defaultValue,
        Action<bool>? changed = null)
    {
        EnsureUnique(key);
        var entry = _category.CreateEntry(key, defaultValue, label);
        var setting = new ToggleSetting(
            key,
            label,
            entry,
            value =>
            {
                _onChanged();
                changed?.Invoke(value);
            });
        _settings.Add(setting);
        return setting;
    }

    internal IntSliderSetting AddSlider(
        string key,
        string label,
        int min,
        int max,
        int defaultValue,
        string format,
        Action<int>? changed = null)
    {
        if (min > max)
            throw new ArgumentOutOfRangeException(nameof(min), "Slider minimum must not exceed maximum.");
        if (defaultValue < min || defaultValue > max)
            throw new ArgumentOutOfRangeException(nameof(defaultValue), "Slider default must be inside the range.");

        EnsureUnique(key);
        var entry = _category.CreateEntry(key, defaultValue, label);
        var clamped = Math.Clamp(entry.Value, min, max);
        if (clamped != entry.Value)
        {
            entry.Value = clamped;
            _onChanged();
        }

        var setting = new IntSliderSetting(
            key,
            label,
            entry,
            min,
            max,
            format,
            value =>
            {
                _onChanged();
                changed?.Invoke(value);
            });
        _settings.Add(setting);
        return setting;
    }

    internal DurationActionSetting AddDurationAction(
        string key,
        string label,
        string buttonText,
        int min,
        int max,
        int defaultValue,
        string[] unitLabels,
        int[] unitSeconds,
        int defaultUnitIndex,
        Action<int> apply)
    {
        EnsureUnique(key);
        var setting = new DurationActionSetting(
            key,
            label,
            buttonText,
            min,
            max,
            defaultValue,
            unitLabels,
            unitSeconds,
            defaultUnitIndex,
            apply);
        _settings.Add(setting);
        return setting;
    }

    private void EnsureUnique(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Setting key must not be empty.", nameof(key));

        foreach (var existing in _settings)
        {
            if (string.Equals(existing.Key, key, StringComparison.Ordinal))
                throw new InvalidOperationException($"Duplicate setting key '{key}' in section '{Id}'.");
        }
    }
}
