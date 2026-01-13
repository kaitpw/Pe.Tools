using Pe.Ui.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;

namespace Pe.Tools.Commands.FamilyFoundry.FamilyFoundryUi;

/// <summary>
///     A horizontal row of action buttons with keyboard navigation.
///     Supports Tab/Shift+Tab and Left/Right arrow navigation.
///     Enter/Space activates the focused button.
/// </summary>
public class ActionButtonRow : RevitHostedUserControl {
    private readonly List<Button> _buttons = [];
    private readonly StackPanel _panel;
    private int _focusedIndex;

    public ActionButtonRow() {
        this.Focusable = true;

        this._panel = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };

        var border = new BorderSpec()
            .Padding(UiSz.none)
            .CreateAround(this._panel);

        this.Content = border;
        this.PreviewKeyDown += this.OnPreviewKeyDown;
        this.Loaded += this.OnLoaded;
    }

    /// <summary>
    ///     Event raised when an action button is clicked.
    /// </summary>
    public event EventHandler<ActionButtonClickedEventArgs> ActionClicked;

    /// <summary>
    ///     Adds an action button to the row.
    /// </summary>
    public ActionButtonRow AddButton(string name, ButtonAction action, SymbolRegular? icon = null) {
        var button = new Button {
            Content = name,
            Margin = new Thickness(4, 0, 4, 0),
            Padding = new Thickness(12, 6, 12, 6),
            Tag = action,
            Focusable = true
        };

        if (icon.HasValue) button.Icon = new SymbolIcon { Symbol = icon.Value };

        button.Click += (_, _) => this.OnButtonClick(action);
        button.GotFocus += (_, _) => this.UpdateFocusedIndex(button);

        this._buttons.Add(button);
        _ = this._panel.Children.Add(button);

        return this;
    }

    /// <summary>
    ///     Sets whether buttons are enabled.
    /// </summary>
    public void SetEnabled(bool enabled) {
        foreach (var button in this._buttons)
            button.IsEnabled = enabled;
    }

    /// <summary>
    ///     Sets whether a specific button is enabled by action type.
    /// </summary>
    public void SetButtonEnabled(ButtonAction action, bool enabled) {
        var button = this._buttons.FirstOrDefault(b => b.Tag is ButtonAction a && a == action);
        if (button != null)
            button.IsEnabled = enabled;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) {
        // Focus the first button on load
        if (this._buttons.Count > 0) {
            this._focusedIndex = 0;
            _ = this._buttons[0].Focus();
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e) {
        if (this._buttons.Count == 0) return;

        switch (e.Key) {
        case Key.Tab when (Keyboard.Modifiers & ModifierKeys.Shift) != 0:
        case Key.Left:
            this.MoveFocus(-1);
            e.Handled = true;
            break;

        case Key.Tab when (Keyboard.Modifiers & ModifierKeys.Shift) == 0:
        case Key.Right:
            this.MoveFocus(1);
            e.Handled = true;
            break;

        case Key.Enter:
        case Key.Space:
            if (this._focusedIndex >= 0 && this._focusedIndex < this._buttons.Count) {
                var action = (ButtonAction)this._buttons[this._focusedIndex].Tag;
                this.OnButtonClick(action);
            }

            e.Handled = true;
            break;
        }
    }

    private void MoveFocus(int direction) {
        if (this._buttons.Count == 0) return;

        this._focusedIndex = (this._focusedIndex + direction + this._buttons.Count) % this._buttons.Count;
        _ = this._buttons[this._focusedIndex].Focus();
    }

    private void UpdateFocusedIndex(Button button) {
        var index = this._buttons.IndexOf(button);
        if (index >= 0) this._focusedIndex = index;
    }

    private void OnButtonClick(ButtonAction action) =>
        this.ActionClicked?.Invoke(this, new ActionButtonClickedEventArgs(action));
}

/// <summary>
///     Enum for action button types.
/// </summary>
public enum ButtonAction {
    RegenerateSchema,
    ProcessFamilies,
    Cancel
}

/// <summary>
///     Event args for action button clicks.
/// </summary>
public class ActionButtonClickedEventArgs : EventArgs {
    public ActionButtonClickedEventArgs(ButtonAction action) => this.Action = action;
    public ButtonAction Action { get; }
}