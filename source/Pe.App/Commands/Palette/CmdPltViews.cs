using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Extensions;
using Pe.Extensions.UiApplication;
using Pe.Global.Services.Storage;
using Pe.Library.Revit.Ui;
using Pe.Ui.Core;
using Serilog.Events;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;

namespace Pe.App.Commands.Palette;

[Transaction(TransactionMode.Manual)]
public class CmdPltViews : IExternalCommand {
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elementSet) {
        try {
            var uiapp = commandData.Application;
            var doc = uiapp.ActiveUIDocument.Document;

            var items = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate
                            && v.ViewType != ViewType.Legend
                            && v.ViewType != ViewType.DrawingSheet
                            && v.ViewType != ViewType.DraftingView
                            && v.ViewType != ViewType.SystemBrowser
                            && v.ViewType != ViewType.ProjectBrowser
                            && v is not ViewSchedule)
                .OrderBy(v => v.Name)
                .Select(v => new ViewPaletteItem(v));

            var actions = new List<PaletteAction<ViewPaletteItem>> {
                new() {
                    Name = "Open View",
                    Execute = async item => uiapp.OpenAndActivateView(item.View),
                    CanExecute = item => item != null && item.View.CanBePrinted
                }
            };

            var window = PaletteFactory.Create("View Palette", items, actions,
                new PaletteOptions<ViewPaletteItem> {
                    Storage = new Storage(nameof(CmdPltViews)),
                    PersistenceKey = item => item.View.Id.ToString()
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
///     Adapter that wraps Revit View to implement ISelectableItem
/// </summary>
public class ViewPaletteItem(View view) : IPaletteListItem {
    private readonly string _discipline = view.HasViewDiscipline()
        ? view.Discipline.ToString()
        : string.Empty;

    // Cache expensive sheet lookup - called during item creation for TextSecondary
    private readonly Lazy<string> _sheetInfo = new(() => GetSheetInfoStatic(view));

    public View View { get; } = view;
    public string TextPrimary => this.View.Name;

    public string TextSecondary {
        get {
            var sheetInfo = this._sheetInfo.Value;
            return string.IsNullOrEmpty(sheetInfo) ? "Not Sheeted" : $"Sheeted on: {sheetInfo}";
        }
    }

    public string TextPill => this.View.FindParameter("View Use")?.AsString() ?? string.Empty;

    public Func<string> GetTextInfo => () =>
        $"Assoc. Lvl:{this.View.FindParameter(BuiltInParameter.PLAN_VIEW_LEVEL)?.AsValueString()}" +
        $"\nDetail Lvl: {this.View.DetailLevel}" +
        $"\nDiscipline: {this._discipline}" +
        $"\nType: {this.View.ViewType}" +
        $"\nId: {this.View.Id}";

    public BitmapImage Icon => null;
    public Color? ItemColor => null;

    private static string GetSheetInfoStatic(View view) {
        var doc = view.Document;

        // Find which sheet this view is on by searching through all sheets
        var sheets = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>();

        foreach (var sheet in sheets) {
            var viewportIds = sheet.GetAllViewports();
            foreach (var viewportId in viewportIds) {
                var viewport = doc.GetElement(viewportId) as Viewport;
                if (viewport?.ViewId == view.Id) {
                    var sheetNumber = sheet.SheetNumber;
                    var sheetName = sheet.Name;
                    return $"{sheetNumber} - {sheetName}";
                }
            }
        }

        return string.Empty;
    }
}