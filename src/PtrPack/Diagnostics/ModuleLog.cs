using System;
using MelonLoader;

namespace PtrPack.Diagnostics;

internal sealed class ModuleLog
{
    private readonly MelonLogger.Instance _logger;
    private readonly string _tag;

    internal ModuleLog(MelonLogger.Instance logger, string tag)
    {
        _logger = logger;
        _tag = tag;
    }

    internal void Msg(string message)
        => _logger.Msg($"[{_tag}] {message}");

    internal void Warning(string message)
        => _logger.Warning($"[{_tag}] {message}");

    internal void Error(string message, Exception? exception = null)
        => _logger.Error(exception == null
            ? $"[{_tag}] {message}"
            : $"[{_tag}] {message}{Environment.NewLine}{exception}");
}
