using System.Windows.Controls;

namespace Pe.Ui.Core;

/// <summary>
///     Base class for all UserControls.
///     Automatically loads WpfUiResources in the constructor to ensure they are available
///     before InitializeComponent() runs in derived classes.
/// </summary>
public class RevitHostedUserControl : UserControl {
    public RevitHostedUserControl() => ThemeManager.LoadWpfUiResources(this);
}