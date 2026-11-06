using PtrPack.Diagnostics;
using PtrPack.Ui;

namespace PtrPack.Mod;

internal sealed class ModuleContext
{
    internal ModuleContext(ModuleLog log, ModuleConfig config, WindowManager windows)
    {
        Log = log;
        Config = config;
        Windows = windows;
    }

    internal ModuleLog Log { get; }
    internal ModuleConfig Config { get; }
    internal WindowManager Windows { get; }
    internal bool InGameplay { get; set; }
}
