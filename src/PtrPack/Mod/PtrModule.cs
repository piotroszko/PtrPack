using PtrPack.Settings;
namespace PtrPack.Mod;



internal abstract class PtrModule
{
    internal abstract string Id { get; }
    internal abstract string DisplayName { get; }
    internal virtual bool EnabledByDefault => true;
    internal ModuleContext Context { get; private set; } = null!;

    internal void Attach(ModuleContext context) => Context = context;

    internal virtual void OnRegisterSettings(ModSettings settings) { }
    internal virtual void OnInitialize() { }
    internal virtual void OnUpdate() { }
    internal virtual void OnGameplayEnter() { }
    internal virtual void OnGameplayExit() { }
    internal virtual void OnGui() { }
    internal virtual void OnShutdown() { }
}
