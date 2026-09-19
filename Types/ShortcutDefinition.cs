using Avalonia.Input;
using System.Collections.Generic;

namespace MajdataEdit_Neo.Types;

public sealed record ShortcutDefinition(
    string FunctionKey,
    string GestureText,
    KeyGesture Gesture,
    string? PluginIconKey = null);

public static class ShortcutDefinitions
{
    public static IReadOnlyList<ShortcutDefinition> All { get; } =
    [
        new("Gui_Save", "Ctrl + S", new(Key.S, KeyModifiers.Control)),
        new("Shortcut_Undo", "Ctrl + Z", new(Key.Z, KeyModifiers.Control)),
        new("Shortcut_Redo", "Ctrl + Y", new(Key.Y, KeyModifiers.Control)),
        new("Shortcut_Redo", "Ctrl + Shift + Z", new(Key.Z, KeyModifiers.Control | KeyModifiers.Shift)),
        new("Shortcut_PlayStop", "Ctrl + Shift + C", new(Key.C, KeyModifiers.Control | KeyModifiers.Shift)),
        new("Shortcut_PlayPause", "Ctrl + Shift + X", new(Key.X, KeyModifiers.Control | KeyModifiers.Shift)),
        new("Shortcut_PlayIncludeOp", "Ctrl + Shift + A", new(Key.A, KeyModifiers.Control | KeyModifiers.Shift)),
        new("Shortcut_IncreasePlaybackSpeed", "Ctrl + P", new(Key.P, KeyModifiers.Control)),
        new("Shortcut_DecreasePlaybackSpeed", "Ctrl + O", new(Key.O, KeyModifiers.Control)),
        new("Gui_MirrorHorizontally", "Ctrl + J", new(Key.J, KeyModifiers.Control), "mirror_h"),
        new("Gui_MirrorVertically", "Ctrl + K", new(Key.K, KeyModifiers.Control), "mirror_v"),
        new("Gui_Mirror180", "Ctrl + L", new(Key.L, KeyModifiers.Control), "mirror_180"),
        new("Gui_Rotate45", "Ctrl + ;", new(Key.OemSemicolon, KeyModifiers.Control), "rotate_r"),
        new("Gui_RotateNeg45", "Ctrl + '", new(Key.OemQuotes, KeyModifiers.Control), "rotate_l")
    ];
}
