using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Pe.Extensions.UiApplication;
using Pe.Global.Services.Storage;
using Pe.Library.Revit.Ui;
using Pe.Ui.Core;
using Pe.Ui.Core.Services;
using Serilog.Events;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;

namespace AddinPaletteSuite.Cmds;

[Transaction(TransactionMode.Manual)]
public class CmdPltAllViews : IExternalCommand {
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elementSet) {
        try {
            var uiapp = commandData.Application;
            var doc = uiapp.ActiveUIDocument.Document;

            var items = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .OrderBy(v => v.Name)
                .Select(v => new AllViewPaletteItem(v));

            var actions = new List<PaletteAction<AllViewPaletteItem>> {
                new() { Name = "Open View", Execute = async item => uiapp.OpenAndActivateView(item.View) }
            };

            var window = PaletteFactory.Create("All Views Palette", items, actions,
                new PaletteOptions<AllViewPaletteItem> {
                    Storage = new Storage(nameof(CmdPltAllViews)),
                    PersistenceKey = item => item.View.Id.ToString(),
                    SearchConfig = SearchConfig.Default(),
                    FilterKeySelector = item => item.View.ViewType.ToString()
                });
            window.Show();

            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(LogEventLevel.Error, new StackFrame(), ex, true).Show();
            return Result.Failed;
        }
    }
}

/// <summary>
///     Adapter that wraps Revit View to implement ISelectableItem for all views (no filtering)
/// </summary>
public class AllViewPaletteItem(View view) : IPaletteListItem {
    public View View { get; } = view;
    public string TextPrimary => this.View.Name;
    public string TextSecondary => string.Empty;
    public string TextPill => this.View.ViewType.ToString();
    public Func<string> GetTextInfo => () => $"View Type: {this.View.ViewType}\nId: {this.View.Id}";
    public BitmapImage Icon => null;
    public Color? ItemColor => null;
}