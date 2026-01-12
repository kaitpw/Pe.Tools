using Pe.Ui.ViewModels;

namespace Pe.Ui.Views;

public sealed partial class Pe.UiView
{
public Pe.UiView(Pe.UiViewModel viewModel)
{
    DataContext = viewModel;
    InitializeComponent();
}
}