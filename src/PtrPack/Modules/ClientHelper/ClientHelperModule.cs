using System;
using System.Collections.Generic;
using Il2Cpp;
using PtrLib.Mod;
using PtrLib.Settings;
using PtrLib.Ui;
using UnityEngine;

namespace PtrPack.Modules.ClientHelper;

internal sealed partial class ClientHelperModule : PtrModule
{
    private enum ViewMode
    {
        None,
        Server,
        Router,
        Switch,
    }

    private NativeWindow? _window;
    private SetIP? _setIp;
    private RouterConfiguration? _routerConfig;
    private NetworkSwitchConfiguration? _switchConfig;
    private ToggleSetting? _showOnServer;
    private ToggleSetting? _showOnRouter;
    private ToggleSetting? _showOnSwitch;

    private float _nextReferenceScan;
    private float _nextRefresh;
    private ViewMode _lastMode;
    private ViewMode _placedMode;
    private bool _templateWarningLogged;
    private bool _windowLogged;
    private bool _referencesLogged;
    private ViewMode _renderedMode;
    private string _renderedSignature = string.Empty;

    public override string Id => "ClientHelper";
    public override string DisplayName => "Client Helper";
    public override bool EnabledByDefault => true;

    public override void OnRegisterSettings(ModSettings settings)
    {
        var section = settings.AddSection(Id, DisplayName, Context.Config.Category);
        _showOnServer = section.AddToggle(
            "ShowOnServerView",
            "Show helper on the server IP screen",
            true);
        _showOnRouter = section.AddToggle(
            "ShowOnRouterView",
            "Show route planner on the router screen",
            true);
        _showOnSwitch = section.AddToggle(
            "ShowOnSwitchView",
            "Show port audit on the switch screen",
            true);

        section.AddAction(
            "ResetServerWindow",
            "Reset server helper position",
            "Reset",
            () => ResetWindowPosition(ViewMode.Server));
        section.AddAction(
            "CenterServerWindow",
            "Center server helper",
            "Center",
            () => CenterWindow(ViewMode.Server));
        section.AddAction(
            "ResetRouterWindow",
            "Reset router helper position",
            "Reset",
            () => ResetWindowPosition(ViewMode.Router));
        section.AddAction(
            "CenterRouterWindow",
            "Center router helper",
            "Center",
            () => CenterWindow(ViewMode.Router));
        section.AddAction(
            "ResetSwitchWindow",
            "Reset switch helper position",
            "Reset",
            () => ResetWindowPosition(ViewMode.Switch));
        section.AddAction(
            "CenterSwitchWindow",
            "Center switch helper",
            "Center",
            () => CenterWindow(ViewMode.Switch));
    }

    private void ResetWindowPosition(ViewMode mode)
    {
        string key = PositionKey(mode);
        bool active = _window != null &&
                      string.Equals(_window.PositionKey, key, StringComparison.Ordinal);
        if (active)
            CloseWindow();

        NativeWindow.ClearSavedPosition(key);

        if (active)
        {
            _placedMode = ViewMode.None;
            _lastMode = ViewMode.None;
            _renderedMode = ViewMode.None;
            _renderedSignature = string.Empty;
        }
    }

    private void CenterWindow(ViewMode mode)
    {
        string key = PositionKey(mode);
        if (_window != null &&
            string.Equals(_window.PositionKey, key, StringComparison.Ordinal))
        {
            _window.CenterInParent();
            _placedMode = mode;
            return;
        }

        NativeWindow.ClearSavedPosition(key);
        NativeWindow.RequestCenter(key);
    }

    public override void OnGameplayEnter()
    {
        _window = null;
        _setIp = null;
        _routerConfig = null;
        _switchConfig = null;
        _nextReferenceScan = 0f;
        _nextRefresh = 0f;
        _lastMode = ViewMode.None;
        _placedMode = ViewMode.None;
        _templateWarningLogged = false;
        _windowLogged = false;
        _referencesLogged = false;
        _renderedMode = ViewMode.None;
        _renderedSignature = string.Empty;
        RouterAudit.Reset();
        ClientHelperData.ResetCaches();
    }

    public override void OnUpdate()
    {
        if (!Context.InGameplay)
            return;

        float now = Time.unscaledTime;

        if (now >= _nextReferenceScan)
        {
            _nextReferenceScan = now + 0.5f;
            EnsureReferences();
        }

        ViewMode mode = DetectMode();
        if (mode == ViewMode.None)
        {
            _lastMode = ViewMode.None;
            _placedMode = ViewMode.None;
            _window?.Hide();
            return;
        }

        if (_window == null)
        {
            try
            {
                _window = Context.Windows.ShowNative(
                    "PtrPack Client Helper",
                    "Client Helper",
                    new Vector2(660f, 620f),
                    Vector2.zero,
                    25,
                    false,
                    PositionKey(mode));
            }
            catch (Exception exception)
            {
                Context.Log.Warning($"Client helper window could not be created: {exception}");
                _templateWarningLogged = true;
            }

            if (_window == null && !_templateWarningLogged)
            {
                _templateWarningLogged = true;
                Context.Log.Warning("Native window template was not found; client helper is inactive.");
            }
        }

        if (_window == null)
            return;

        string positionKey = PositionKey(mode);
        if (!string.Equals(_window.PositionKey, positionKey, StringComparison.Ordinal))
        {
            _window.SetPositionKey(positionKey);
            _placedMode = _window.HasSavedPosition ? mode : ViewMode.None;
        }
        else if (_window.HasSavedPosition)
        {
            _placedMode = mode;
        }

        if (!_window.IsVisible)
        {
            Context.Log.Msg($"Client helper: showing window for {mode}.");
            _window.Show();
            Context.Log.Msg("Client helper: window shown.");
        }

        if (!_windowLogged)
        {
            _windowLogged = true;
            Context.Log.Msg($"Client helper window ready for {mode} view.");
        }

        var target = FindWindowRect(mode);
        if (target != null && mode != _placedMode && !_window.HasSavedPosition)
        {
            Context.Log.Msg("Client helper: placing window.");
            _window.PlaceNextTo(target, 32f, mode == ViewMode.Server);
            _placedMode = mode;
            Context.Log.Msg(
                $"Client helper: window at {_window.WindowRect.anchoredPosition} " +
                $"(target {target.rect.size} at {target.anchoredPosition}).");
        }

        if (mode != _lastMode || now >= _nextRefresh)
        {
            _lastMode = mode;
            _nextRefresh = now + 2f;
            Populate(mode);
        }
    }

    public override void OnGameplayExit() => CloseWindow();
    public override void OnShutdown() => CloseWindow();

    private void EnsureReferences()
    {
        if (_setIp == null)
            _setIp = FindSetIp();
        if (_routerConfig == null)
            _routerConfig = UiFactory.FindSceneComponent<RouterConfiguration>();
        if (_switchConfig == null)
            _switchConfig = UiFactory.FindSceneComponent<NetworkSwitchConfiguration>();

        if (!_referencesLogged &&
            (_setIp != null || _routerConfig != null || _switchConfig != null))
        {
            _referencesLogged = true;
            Context.Log.Msg(
                $"Client helper references: SetIP={_setIp != null}, " +
                $"Router={_routerConfig != null}, Switch={_switchConfig != null}.");
        }
    }

    private static SetIP? FindSetIp()
    {
        var all = Resources.FindObjectsOfTypeAll<SetIP>();
        SetIP? fallback = null;
        for (int i = 0; i < all.Length; i++)
        {
            var candidate = all[i];
            if (candidate == null || !candidate.gameObject.scene.IsValid())
                continue;

            if (fallback == null)
                fallback = candidate;

            if (candidate.server != null &&
                candidate.canvas != null &&
                candidate.canvas.activeInHierarchy)
            {
                return candidate;
            }
        }

        return fallback;
    }

    private ViewMode DetectMode()
    {
        if (_showOnServer?.Value != false &&
            _setIp != null &&
            (_setIp.isActive || IsVisible(_setIp.canvas)))
        {
            return ViewMode.Server;
        }

        if (_showOnRouter?.Value != false &&
            _routerConfig != null &&
            IsVisible(_routerConfig.gameObject))
        {
            return ViewMode.Router;
        }

        if (_showOnSwitch?.Value != false &&
            _switchConfig != null &&
            IsVisible(_switchConfig.gameObject))
        {
            return ViewMode.Switch;
        }

        return ViewMode.None;
    }

    private static bool IsVisible(GameObject? root)
    {
        if (root == null || !root.activeInHierarchy)
            return false;

        var canvas = root.GetComponent<Canvas>();
        return canvas == null || canvas.enabled;
    }

    private static string PositionKey(ViewMode mode) => $"ClientHelper.{mode}";

    private RectTransform? FindWindowRect(ViewMode mode)
    {
        Transform? root = null;
        if (mode == ViewMode.Server && _setIp?.canvas != null)
            root = _setIp.canvas.transform;
        else if (mode == ViewMode.Router)
            root = _routerConfig?.transform;
        else if (mode == ViewMode.Switch)
            root = _switchConfig?.transform;

        if (root == null)
            return null;

        var window = UiFactory.FindChild(root, "bcg");
        if (window != null)
        {
            var rect = window.GetComponent<RectTransform>();
            if (rect != null)
                return rect;
        }

        return root.GetComponent<RectTransform>();
    }


    private void CloseWindow()
    {
        if (_window == null)
            return;

        Context.Windows.Close(_window);
        _window = null;
    }
}
