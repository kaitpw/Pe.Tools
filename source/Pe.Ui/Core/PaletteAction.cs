using System.Windows;
using System.Windows.Input;

namespace PeUi.Core;

/// <summary>
///     Represents a single action that can be triggered in the palette.
/// </summary>
/// <remarks>
///     Actions come in two types:
///     <list type="bullet">
///         <item>
///             <term>Execute</term>
///             <description>Standard action - deferred to Revit API context after window closes.</description>
///         </item>
///         <item>
///             <term>NextPalette</term>
///             <description>Opens another palette in the sidebar.</description>
///         </item>
///     </list>
/// </remarks>
public record PaletteAction<TItem> where TItem : IPaletteListItem {
    /// <summary>Display name for the action (for debugging/logging)</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Keyboard modifiers required (Ctrl, Shift, Alt, etc.)</summary>
    public ModifierKeys Modifiers { get; init; } = ModifierKeys.None;

    /// <summary>Keyboard key that triggers this action</summary>
    public Key? Key { get; init; }

    /// <summary>
    ///     Standard action - deferred to Revit API context after window closes.
    ///     Use async lambdas: <c>Execute = async item => await DoWork(item)</c>
    ///     For sync work: <c>Execute = async item => DoSyncWork(item)</c>
    /// </summary>
    public Func<TItem, Task> Execute { get; init; }

    /// <summary>
    ///     Opens another palette in the sidebar.
    ///     Returns the UIElement (typically a Palette) to display.
    /// </summary>
    public Func<TItem, UIElement> NextPalette { get; init; }

    /// <summary>Optional predicate to check if action can execute</summary>
    public Func<TItem, bool> CanExecute { get; init; } = item => item != null;
}