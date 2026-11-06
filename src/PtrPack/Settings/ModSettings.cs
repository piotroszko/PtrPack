using System;
using System.Collections.Generic;
using MelonLoader;

namespace PtrPack.Settings;

internal sealed class ModSettings
{
    private readonly List<SettingsSection> _sections = new List<SettingsSection>();

    internal IReadOnlyList<SettingsSection> Sections => _sections;

    internal SettingsSection AddSection(string id, string title, MelonPreferences_Category category)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Section id must not be empty.", nameof(id));
        if (category == null)
            throw new ArgumentNullException(nameof(category));

        foreach (var existing in _sections)
        {
            if (string.Equals(existing.Id, id, StringComparison.Ordinal))
                throw new InvalidOperationException($"Duplicate settings section id '{id}'.");
        }

        var section = new SettingsSection(id, title, category);
        _sections.Add(section);
        return section;
    }
}
