using AddinPaletteSuite.Cmds;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Nice3point.Revit.Toolkit.External;
using Pe.Application.Commands;
using Pe.Application.Commands.FamilyFoundry;
using Pe.Global.Services.Document;
using Pe.Library.Revit.Ui;
using Pe.Ui.Core;
using ricaun.Revit.UI.Tasks;
using Serilog;
using Serilog.Events;

namespace Pe.Application;

/// <summary>
///     Application entry point
/// </summary>
[UsedImplicitly]
public class Application : ExternalApplication
{
    /// <summary>
    ///     RevitTaskService for executing code in Revit API context from async/WPF contexts.
    /// </summary>
    private static RevitTaskService _revitTaskService;
    public override void OnStartup()
    {
        
        // Subscribe to ViewActivated event for MRU tracking
        this.Application.ViewActivated += OnViewActivated;

        // Subscribe to DocumentClosing to clean up MRU buffer
        this.Application.ControlledApplication.DocumentClosing += OnDocumentClosing;

        // Initialize RevitTaskService for async/deferred execution in Revit API context
        _revitTaskService = new RevitTaskService(this.Application);
        _revitTaskService.Initialize();
        RevitTaskAccessor.RunAsync = async action => await _revitTaskService.Run(async () => await action());

        CreateLogger();
        this.CreateRibbon();
    }
    
    public new Result OnShutdown(UIControlledApplication app) {
        // AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
        app.ViewActivated -= OnViewActivated;
        app.ControlledApplication.DocumentClosing -= OnDocumentClosing;
        _revitTaskService?.Dispose();
        return Result.Succeeded;
    }

    private static void OnViewActivated(object sender, ViewActivatedEventArgs e) {
        if (e?.CurrentActiveView == null) return;
        if (sender is not UIApplication) return;

        // Record view activation for MRU tracking
        DocumentManager.Instance.RecordViewActivation(e.CurrentActiveView.Document, e.CurrentActiveView.Id);
    }

    private static void OnDocumentClosing(object sender, DocumentClosingEventArgs e) {
        if (e?.Document == null) return;
        DocumentManager.Instance.OnDocumentClosed(e.Document);
    }

    public override void OnShutdown()
    {
        Log.CloseAndFlush();
    }

    private void CreateRibbon()
    {
        var panel = this.Application.CreatePanel("Commands", "Pe.Application");
        var app = this.Application;

        
        // 1. Create ribbon tab
        const string tabName = "PE TOOLS";
        try {
            app.CreateRibbonTab(tabName);
        } catch (Exception) {
            new Ballogger()
                .Add(LogEventLevel.Information, null, $"{tabName} already exists in the current Revit instance.")
                .Show();
        }

        // 2. Create ribbon panel
        const string ribbonPanelName1 = "Manage";
        const string ribbonPanelName2 = "Tools";
        const string ribbonPanelName3 = "Migration";
        const string ribbonPanelName4 = "Dev";
        var panelManage = UiHelpers.CreateRibbonPanel(app, tabName, ribbonPanelName1);
        var panelTools = UiHelpers.CreateRibbonPanel(app, tabName, ribbonPanelName2);
        var panelMigration = UiHelpers.CreateRibbonPanel(app, tabName, ribbonPanelName3);
        var panelDev = UiHelpers.CreateRibbonPanel(app, tabName, ribbonPanelName4);

        var manageStackButton = panelManage.AddPullDownButton("General");
        // var ffManagerStackButton = panelMigration.AddSplitButton("Manager");

#if !REVIT2023 && !REVIT2024 // APS Auth not supported in Revit 2023/2024
        ButtonDataHydrator.AddButtonData([
            manageStackButton.AddPushButton<CmdApsAuthPKCE>("OAuth PKCE"),
            manageStackButton.AddPushButton<CmdApsAuthNormal>("OAuth Normal")
        ]);
#endif

        ButtonDataHydrator.AddButtonData([
            panelMigration.AddPushButton<CmdCreateSchedule>("Create Schedule"),
            panelMigration.AddPushButton<CmdSerializeSchedule>("Serialize Schedule"),
            panelMigration.AddPushButton<CmdFFManager>("FF Manager"),
            panelMigration.AddPushButton<CmdFFManagerSnapshot>("FF Manager Snapshot"),
            panelMigration.AddPushButton<CmdFFMigrator>("FF Migrator"),
            panelMigration.AddPushButton<CmdFFMakeATVariants>("Make AT Variants"),
            panelMigration.AddPushButton<CmdFFParamAggregator>("FF Param Aggregator"),
            manageStackButton.AddPushButton<CmdCacheParametersService>("Cache Params Svc"),
            // manageStackButton.AddPushButton<CmdTestSettingsEditor>("Test Settings Editor"),

            panelTools.AddPushButton<CmdMep2040>("MEP 2040"),
            panelTools.AddPushButton<CmdPltCommands>("Command Palette"),
            panelTools.AddPushButton<CmdPltViews>("View Palette"),
            panelTools.AddPushButton<CmdPltAllViews>("All Views Palette"),
            panelTools.AddPushButton<CmdPltMruViews>("MRU Views"),
            panelTools.AddPushButton<CmdPltSchedules>("Schedule Palette"),
            panelTools.AddPushButton<CmdPltSheets>("Sheet Palette"),
            panelTools.AddPushButton<CmdPltFamilies>("Family Palette"),
            panelTools.AddPushButton<CmdPltFamilyElements>("Family Palette"),
            panelTools.AddPushButton<CmdTapMaker>("Tap Maker")
        ]);

    }

    private static void CreateLogger()
    {
        const string outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Debug(LogEventLevel.Debug, outputTemplate)
            .MinimumLevel.Debug()
            .CreateLogger();

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var exception = (Exception)args.ExceptionObject;
            Log.Fatal(exception, "Domain unhandled exception");
        };
    }
}