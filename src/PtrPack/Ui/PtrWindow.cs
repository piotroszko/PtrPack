using UnityEngine;
using System;

namespace PtrPack.Ui;

internal sealed class PtrWindow
{
    private readonly int _id;
    private readonly string _title;
    private readonly string _body;
    private Rect _rect;
    private GUIStyle? _bodyStyle;

    internal PtrWindow(int id, string title, string body, float width, float height)
    {
        _id = id;
        _title = title;
        _body = body;
        _rect = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.3f,
            width,
            height);
    }

    internal bool Visible { get; set; } = true;

    internal void Draw()
    {
        if (!Visible) return;
        _rect = GUI.Window(_id, _rect, (Action<int>)DrawContents, _title);
    }

    private void DrawContents(int id)
    {
        _bodyStyle ??= new GUIStyle
        {
            fontSize = 20,
            font = GUI.skin.label.font,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
        };

        GUI.Label(
            new Rect(12f, 24f, _rect.width - 24f, _rect.height - 36f),
            _body,
            _bodyStyle);
        GUI.DragWindow();
    }
}
