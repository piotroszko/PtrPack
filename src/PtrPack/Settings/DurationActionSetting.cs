using System;

namespace PtrPack.Settings;

internal sealed class DurationActionSetting : SettingDefinition
{
    internal DurationActionSetting(
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
        : base(key, label)
    {
        if (unitLabels == null) throw new ArgumentNullException(nameof(unitLabels));
        if (unitSeconds == null) throw new ArgumentNullException(nameof(unitSeconds));
        if (unitLabels.Length == 0 || unitLabels.Length != unitSeconds.Length)
            throw new ArgumentException("Duration units must be non-empty and pair labels with seconds.");
        for (int i = 0; i < unitSeconds.Length; i++)
        {
            if (unitSeconds[i] <= 0)
                throw new ArgumentOutOfRangeException(nameof(unitSeconds), "Duration units must be positive.");
        }
        if (min < 1 || max < min)
            throw new ArgumentOutOfRangeException(nameof(min), "Duration range is invalid.");
        if (defaultValue < min || defaultValue > max)
            throw new ArgumentOutOfRangeException(nameof(defaultValue), "Duration default is outside the range.");
        if (defaultUnitIndex < 0 || defaultUnitIndex >= unitLabels.Length)
            throw new ArgumentOutOfRangeException(nameof(defaultUnitIndex), "Default unit index is outside the unit list.");

        ButtonText = buttonText;
        Min = min;
        Max = max;
        DefaultValue = defaultValue;
        UnitLabels = unitLabels;
        UnitSeconds = unitSeconds;
        DefaultUnitIndex = defaultUnitIndex;
        Apply = apply ?? throw new ArgumentNullException(nameof(apply));
    }

    internal string ButtonText { get; }
    internal int Min { get; }
    internal int Max { get; }
    internal int DefaultValue { get; }
    internal string[] UnitLabels { get; }
    internal int[] UnitSeconds { get; }
    internal int DefaultUnitIndex { get; }
    internal Action<int> Apply { get; }
}
