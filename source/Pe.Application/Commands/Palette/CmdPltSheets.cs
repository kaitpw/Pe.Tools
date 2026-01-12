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

namespace AddinPaletteSuite.Cmds;

[Transaction(TransactionMode.Manual)]
public class CmdPltSheets : IExternalCommand {
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elementSet) {
        try {
            var uiapp = commandData.Application;
            var doc = uiapp.ActiveUIDocument.Document;

            var items = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .OrderBy(s => s.SheetNumber)
                .Select(s => new SheetPaletteItem(s));

            var actions = new List<PaletteAction<SheetPaletteItem>> {
                new() {
                    Name = "Open Sheet",
                    Execute = async item => uiapp.OpenAndActivateView(item.Sheet),
                    CanExecute = item => item != null && item.Sheet.CanBePrinted
                }
            };

            var window = PaletteFactory.Create("Sheet Palette", items, actions,
                new PaletteOptions<SheetPaletteItem> {
                    Storage = new Storage(nameof(CmdPltSheets)),
                    PersistenceKey = item => item.Sheet.Id.ToString(),
                    FilterKeySelector = item => item.TextPill
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
///     Adapter that wraps Revit ViewSheet to implement ISelectableItem
/// </summary>
public class SheetPaletteItem(ViewSheet sheet) : IPaletteListItem {
    public ViewSheet Sheet { get; } = sheet;
    public string TextPrimary => $"{this.Sheet.SheetNumber} - {this.Sheet.Name}";

    public string TextSecondary {
        get {
            var views = this.GetViewInfo();
            return views.Count == 0 ? string.Empty : $"{views.Count} views";
        }
    }

    public string TextPill {
        get {
            try {
                var sheetNum = this.Sheet.FindParameter(BuiltInParameter.SHEET_NUMBER)?.AsString();
                if (string.IsNullOrEmpty(sheetNum) || sheetNum == "-") return string.Empty;

                var firstDigitIndex = sheetNum.TakeWhile(c => !char.IsDigit(c)).Count();
                if (firstDigitIndex == 0) return string.Empty;
                return sheetNum[..firstDigitIndex];
            } catch {
                return string.Empty;
            }
        }
    }

    public Func<string> GetTextInfo => () => {
        var views = this.GetViewInfo();
        var viewText = views.Count == 0
            ? "None"
            : string.Join("\n  ", views.Select(v => $"{v.type} - {v.name}"));
        return $"Id: {this.Sheet.Id}" +
               $"\nPlaced Views:\n\t{viewText}";
    };

    public BitmapImage Icon => null;
    public Color? ItemColor => null;

    public List<(string type, string name)> GetViewInfo() {
        var viewInfo = new List<(string type, string name)>();
        foreach (var viewId in this.Sheet.GetAllPlacedViews()) {
            if (this.Sheet.Document.GetElement(viewId) is View view)
                viewInfo.Add((view.ViewType.ToString(), view.Name));
        }

        return viewInfo;
    }
}