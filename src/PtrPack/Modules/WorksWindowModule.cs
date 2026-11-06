using Il2Cpp;
using PtrPack.Mod;
using PtrPack.Ui;

namespace PtrPack.Modules;

internal sealed class WorksWindowModule : PtrModule
{
    private const string WindowTitle = "PtrPack";
    private const string WindowBody = "Works!";

    private PtrWindow? _window;

    internal override string Id => "WorksWindow";
    internal override string DisplayName => "Works Window";

    internal override void OnGameplayEnter()
    {
        Context.Log.Msg("Gameplay entered; waiting for player.");
    }

    internal override void OnUpdate()
    {
        if (!Context.InGameplay || _window != null) return;

        var manager = PlayerManager.instance;
        if (manager == null || manager.playerClass == null) return;

        _window = Context.Windows.Show(WindowTitle, WindowBody, 360f, 140f);
        Context.Log.Msg("Player ready; 'Works!' window shown.");
    }

    internal override void OnGameplayExit()
    {
        if (_window == null) return;

        Context.Windows.Close(_window);
        _window = null;
        Context.Log.Msg("Window closed on leaving gameplay.");
    }

    internal override void OnShutdown()
    {
        if (_window == null) return;

        Context.Windows.Close(_window);
        _window = null;
    }
}
