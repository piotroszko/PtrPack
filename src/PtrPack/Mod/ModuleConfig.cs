using MelonLoader;

namespace PtrPack.Mod;

internal sealed class ModuleConfig
{
    internal MelonPreferences_Category Category { get; }
    internal MelonPreferences_Entry<bool> Enabled { get; }

    internal ModuleConfig(PtrModule module)
    {
        Category = MelonPreferences.CreateCategory(
            $"PtrPack_{module.Id}",
            $"{ModInfo.Name}: {module.DisplayName}");
        Enabled = Category.CreateEntry("Enabled", module.EnabledByDefault, "Enabled");
    }

    internal bool IsEnabled => Enabled.Value;
}
