using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Pe.FamilyFoundry;
using Pe.FamilyFoundry.Operations;
using Pe.FamilyFoundry.Snapshots;
using Pe.Global.Services.Storage;
using Pe.Library.Revit.Lib;
using Pe.Library.Revit.Ui;
using Serilog.Events;
using System.Diagnostics;

namespace Pe.Tools.Commands.FamilyFoundry;
// support add, delete, remap, sort, rename

[Transaction(TransactionMode.Manual)]
public class CmdFFManagerSnapshot : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSetf
    ) {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        try {
            var storage = new Storage("FF Manager");
            var outputFolderPath = storage.OutputDir().DirectoryPath;

            // force this to never be single transaction
            var executionOptions = new ExecutionOptions { SingleTransaction = false, OptimizeTypeOperations = true };

            // Request both parameter and refplane snapshots
            var collectorQueue = new CollectorQueue()
                .Add(new ParamSectionCollector())
                .Add(new RefPlaneSectionCollector());

            using var processor = new OperationProcessor(doc, executionOptions);

            var queue = new OperationQueue()
                .Add(new LogRefPlaneAndDims(outputFolderPath));

            var metadataString = queue.GetExecutableMetadataString();
            Debug.WriteLine(metadataString);

            var logs = processor
                .SelectFamilies(() => doc.IsFamilyDocument ? null : Pickers.GetSelectedFamilies(uiDoc))
                .ProcessQueue(queue, collectorQueue, outputFolderPath);

            new ProcessingResultBuilder(storage)
                .WithOperationMetadata(queue)
                .WriteSingleFamilyOutput(logs.contexts[0], true);

            var balloon = new Ballogger();
            foreach (var ctx in logs.contexts)
                _ = balloon.Add(LogEventLevel.Information, new StackFrame(),
                    $"Processed {ctx.FamilyName} in {ctx.TotalMs}ms");
            balloon.Show();
            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(LogEventLevel.Error, new StackFrame(), ex, true).Show();
            return Result.Cancelled;
        }
    }
}