using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Extensions;
using Pe.Extensions.UiApplication;
using Pe.Global.Services.Storage;
using Pe.Library.Revit.Ui;

using Pe.Ui.Core;
using Pe.Ui.Core.Services;
using Serilog.Events;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;

namespace Pe.App.Commands.Palette;

[Transaction(TransactionMode.Manual)]
public class CmdPltSchedules : IExternalCommand {
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elementSet) {
        try {
            var uiapp = commandData.Application;
            var doc = uiapp.ActiveUIDocument.Document;

            var items = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .Where(s => !s.Name.Contains("<Revision Schedule>"))
                .OrderBy(s => s.Name)
                .Select(s => new SchedulePaletteItem(s));

            var actions = new List<PaletteAction<SchedulePaletteItem>> {
                new() { Name = "Open", Execute = async item => uiapp.OpenAndActivateView(item.Schedule) }
            };

            var window = PaletteFactory.Create("Schedule Palette", items, actions,
                new PaletteOptions<SchedulePaletteItem> {
                    Storage = new Storage(nameof(CmdPltSchedules)),
                    PersistenceKey = item => item.Schedule.Id.ToString(),
                    SearchConfig = SearchConfig.PrimaryAndSecondary(),
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
///     Adapter that wraps Revit ViewSchedule to implement ISelectableItem
/// </summary>
public class SchedulePaletteItem(ViewSchedule schedule) : IPaletteListItem {
    public ViewSchedule Schedule { get; } = schedule;
    public string TextPrimary => this.Schedule.Name;

    public string TextSecondary {
        get {
            var sheets = this.GetSheetInfo();
            if (sheets.Count == 0) return string.Empty;
            var nums = sheets.Select(s => s.num).Where(n => !string.IsNullOrEmpty(n));
            return $"Sheeted on ({sheets.Count}): {string.Join(", ", nums)}";
        }
    }

    public string TextPill { get; } = schedule.FindParameter("Discipline")?.AsValueString();

    public Func<string> GetTextInfo => () => {
        var sheets = this.GetSheetInfo();
        var sheetText = sheets.Count == 0
            ? "None"
            : string.Join("\n  ", sheets.Select(s => $"{s.num} - {s.name}"));
        return $"Id: {this.Schedule.Id}" +
               $"\nDiscipline: {this.TextPill}" +
               $"\nSheeted on:\n  {sheetText}";
    };

    public BitmapImage Icon => null;
    public Color? ItemColor => null;

    private List<(string num, string name)> GetSheetInfo() {
        var sheetInfo = new List<(string num, string name)>();
        foreach (var inst in this.Schedule.GetScheduleInstances(-1)) {
            var doc = this.Schedule.Document;
            var ownerViewId = doc.GetElement(inst).OwnerViewId;
            var ownerView = doc.GetElement(ownerViewId);
            if (ownerView is ViewSheet view) {
                var num = view.FindParameter(BuiltInParameter.SHEET_NUMBER)?.AsValueString() ?? string.Empty;
                var name = view.FindParameter(BuiltInParameter.SHEET_NAME)?.AsValueString() ?? string.Empty;
                sheetInfo.Add((num, name));
            }
        }

        return sheetInfo;
    }
}