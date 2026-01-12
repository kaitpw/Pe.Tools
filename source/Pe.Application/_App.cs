using Pe.Application.Commands;
using Pe.Application.Commands.FamilyFoundry;
using AddinPaletteSuite.Cmds;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI.Events;
using Nice3point.Revit.Extensions;

namespace Pe.Application;

internal class App : IExternalApplication {
    // Cache resolved assemblies to prevent loading the same assembly multiple times
    // which would create duplicate types in different load contexts and break WPF BAML lookup
    private static readonly Dictionary<string, Assembly> _resolvedAssemblies = new();

    /// <summary>
    ///     RevitTaskService for executing code in Revit API context from async/WPF contexts.
    /// </summary>
    private static RevitTaskService _revitTaskService;

    public Result OnStartup(UIControlledApplication app) {
        // CRITICAL: Pre-cache PE_Tools assembly BEFORE wiring up AssemblyResolve.
        // Revit loads PE_Tools directly (not through AssemblyResolve), so if WPF later
        // tries to resolve "PE_Tools" via Assembly.Load() for a pack URI, our handler
        // must return the SAME instance. Pre-caching ensures consistency.
        var peToolsAssembly = typeof(App).Assembly;
        var peToolsName = peToolsAssembly.GetName().Name;
        _resolvedAssemblies[peToolsName] = peToolsAssembly;
        Debug.WriteLine($"Pre-cached main assembly: {peToolsAssembly.FullName}");

        // Set up assembly resolver for Wpf.Ui and other dependencies
        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

        // CRITICAL: Force WPF to resolve pack URIs NOW while assembly state is clean.
        // This warms up WPF's internal caches with the correct assembly references
        // before any long-running session can cause cache invalidation issues.
        WarmUpWpfPackUriResolution();

        // Subscribe to ViewActivated event for MRU tracking
        app.ViewActivated += OnViewActivated;

        // Subscribe to DocumentClosing to clean up MRU buffer
        app.ControlledApplication.DocumentClosing += OnDocumentClosing;

        // Initialize RevitTaskService for async/deferred execution in Revit API context
        _revitTaskService = new RevitTaskService(app);
        _revitTaskService.Initialize();
        RevitTaskAccessor.RunAsync = async action => await _revitTaskService.Run(async () => await action());

        // 1. Create ribbon tab
        const string tabName = "PE TOOLS";
        try {
            app.CreateRibbonTab(tabName);
        } catch (Exception) {
            new Ballogger()
                .Add(Log.INFO, null, $"{tabName} already exists in the current Revit instance.")
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
            manageStackButton.AddPushButton<CmdUpdate>("Update"),
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

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication app) {
        AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
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

    /// <summary>
    ///     Forces WPF to resolve pack URIs for PE_Tools resources immediately at startup.
    ///     This warms up WPF's internal assembly caches while the CLR's assembly state is clean,
    ///     preventing issues where WPF's pack URI resolution fails after long sessions due to
    ///     cache invalidation or stale state.
    /// </summary>
    private static void WarmUpWpfPackUriResolution() {
        try {
            // Access ThemeManager.WpfUiResources which creates a ResourceDictionary
            // with a pack URI to PE_Tools. This forces WPF to resolve the pack URI
            // and cache the assembly reference NOW.
            var resources = ThemeManager.WpfUiResources;
            Debug.WriteLine($"WPF pack URI warmup complete. Resources loaded: {resources.Count} keys");
        } catch (Exception ex) {
            // Log but don't fail startup - the palette commands will still work
            // (or fail with a clear error) when first invoked
            Debug.WriteLine($"WPF pack URI warmup failed (non-fatal): {ex.Message}");
        }
    }

    private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args) {
        Debug.WriteLine($"Assembly Resolution Requested: {args.Name}");

        var assemblyName = new AssemblyName(args.Name);
        var simpleName = assemblyName.Name;

        // Return cached assembly if we've already resolved this one
        if (_resolvedAssemblies.TryGetValue(simpleName, out var cached))
            return cached;

        // Check if already loaded in the current AppDomain first
        // This prevents creating duplicate assembly instances in different load contexts
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var loaded in loadedAssemblies) {
            if (loaded.GetName().Name == simpleName) {
                _resolvedAssemblies[simpleName] = loaded;
                Debug.WriteLine($"Using already-loaded assembly: {loaded.FullName}");
                return loaded;
            }
        }

        // Get the directory where this add-in's DLL is located
        var addinPath = typeof(App).Assembly.Location;
        var addinDirectory = Path.GetDirectoryName(addinPath);
        if (addinDirectory is null) return null;

        var assemblyPath = Path.Combine(addinDirectory, $"{simpleName}.dll");

        if (!File.Exists(assemblyPath)) {
            Debug.WriteLine($"Assembly not found in add-in directory: {assemblyPath}");
            return null;
        }

        // Use LoadFrom for first-time loads. The key insight is that LoadFrom itself
        // isn't the problem - the problem was calling LoadFrom MULTIPLE TIMES for the
        // same assembly, creating duplicate instances in different load contexts.
        // With the caching and already-loaded checks above, this only runs once per assembly.
        // 
        // NOTE: Do NOT use Assembly.Load(bytes) here - that creates an anonymous/neither
        // context where types are completely isolated and will never match XAML-compiled
        // type references, breaking WPF styles with "TargetType does not match" errors.
        Debug.WriteLine($"Loading assembly via LoadFrom (first time): {assemblyPath}");
        var resolved = Assembly.LoadFrom(assemblyPath);

        _resolvedAssemblies[simpleName] = resolved;
        return resolved;
    }
}

public static class ButtonDataHydrator {
    private static readonly Dictionary<string, ButtonDataRecord> ButtonDataRecords = new() {
        {
            nameof(CmdUpdate), new ButtonDataRecord {
                SmallImage = "monitor-down16.png",
                LargeImage = "monitor-down32.png",
                ToolTip =
                    "Update the PE Tools addin suite to the latest release. You will need to restart Revit. TODO; fix this"
            }
        }, {
            nameof(CmdCacheParametersService),
            new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip = "Cache the parameters service data for use in the Family Foundry command."
            }
        }, {
            nameof(CmdApsAuthPKCE), new ButtonDataRecord {
                SmallImage = "id-card16.png",
                LargeImage = "id-card32.png",
                ToolTip =
                    "Get an access token from Autodesk Platform Services. This is primarily for testing purposes, but running it will not hurt anything."
            }
        }, {
            nameof(CmdApsAuthNormal), new ButtonDataRecord {
                SmallImage = "id-card16.png",
                LargeImage = "id-card32.png",
                ToolTip =
                    "Get an access token from Autodesk Platform Services. This is primarily for testing purposes, but running it will not hurt anything."
            }
        }, {
            nameof(CmdMep2040),
            new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip = "Analyze MEP sustainability metrics (pipe length, refrigerant volume, mech equipment count)."
            }
        }, {
            nameof(CmdPltCommands), new ButtonDataRecord {
                SmallImage = "square-terminal16.png",
                LargeImage = "square-terminal32.png",
                ToolTip =
                    "Search and execute Revit commands quickly without looking through Revit's tabs, ribbons, and panels. Not all commands are guaranteed to run."
            }
        }, {
            nameof(CmdPltViews),
            new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip = "Search and open views in the current document."
            }
        }, {
            nameof(CmdPltAllViews),
            new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip = "Search and open all views in the current document (no filtering)."
            }
        }, {
            nameof(CmdPltMruViews),
            new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip = "Open recently visited views in MRU (Most Recently Used) order."
            }
        }, {
            nameof(CmdPltSchedules),
            new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip = "Search and open schedules in the current document."
            }
        }, {
            nameof(CmdPltSheets),
            new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip = "Search and open sheets in the current document."
            }
        }, {
            nameof(CmdPltFamilies),
            new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip = "Search families in the document. Click to edit family, Ctrl+Click to select all instances."
            }
        }, {
            nameof(CmdPltFamilyElements), new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip =
                    "Browse all family elements (parameters, connectors, dimensions, reference planes, nested families). Highlights selected elements. Only works in family documents."
            }
        }, {
            nameof(CmdTapMaker), new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip =
                    "Add a (default) 6\" tap to a clicked point on a duct face. Works in all views and on both round/rectangular ducts.",
                LongDescription =
                    """
                    Add a (default) 6" tap to a clicked point on a duct face. Works in all views and on both round/rectangular ducts.
                    Automatic click-point adjustments will prevent overlaps (with other taps) and overhangs (over face edges).
                    Automatic size adjustments will size down a duct until it fits on a duct face.

                    In the event an easy location or size adjustment is not found, no tap will be placed.
                    """
            }
        }, {
            nameof(CmdCreateSchedule),
            new ButtonDataRecord {
                SmallImage = "Red_16.png", LargeImage = "Red_32.png", ToolTip = "Create a new schedule from a profile."
            }
        }, {
            nameof(CmdSerializeSchedule),
            new ButtonDataRecord {
                SmallImage = "Red_16.png", LargeImage = "Red_32.png", ToolTip = "Serialize a schedule to a JSON file."
            }
        }, {
            nameof(CmdFFMigrator),
            new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip = "Process families in a variety of ways from the Family Foundry."
            }
        }, {
            nameof(CmdFFManager),
            new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip = "Manage families in a variety of ways from the Family Foundry."
            }
        }, {
            nameof(CmdFFManagerSnapshot), new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip =
                    "Running this will output a JSON file with a config the represents the reference planes, dimensions, and family parameters of the currently open family"
            }
        }, {
            nameof(CmdFFMakeATVariants), new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip =
                    "Test command that processes a family 3 times with incrementing TEST_PROCESS_NUMBER parameter."
            }
        }, {
            nameof(CmdFFParamAggregator),
            new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip = "Aggregate parameter metadata across families in a category and output to CSV."
            }
        }

        // {
        //     nameof(CmdTestSettingsEditor),
        //     new ButtonDataRecord {
        //         SmallImage = "Red_16.png",
        //         LargeImage = "Red_32.png",
        //         ToolTip = "Test the generic settings editor POC with Family Foundry settings."
        //     }
        // }
    };

    public static void AddButtonData(List<PushButton> buttons) {
        foreach (var button in buttons) {
            Debug.WriteLine("button.ClassName: " + button.ClassName);
            var key = button.ClassName.Split('.').Last();
            if (ButtonDataRecords.TryGetValue(key, out var btnData)) {
                _ = button.SetImage(btnData.SmallImage)
                    .SetLargeImage(btnData.LargeImage)
                    .SetToolTip(btnData.ToolTip);
                if (!string.IsNullOrEmpty(btnData.LongDescription))
                    _ = button.SetLongDescription(btnData.LongDescription);
            } else
                throw new Exception($"{key} was not found in ButtonDataRecords.");
        }
    }

    public record ButtonDataRecord {
        private readonly string _largeImage;
        private readonly string _smallImage;
        public string Shortcuts { get; init; }

        public required string SmallImage {
            get => ValidateUri(this._smallImage);
            init => this._smallImage = value;
        }

        public required string LargeImage {
            get => ValidateUri(this._largeImage);
            init => this._largeImage = value;
        }

        public required string ToolTip { get; init; }
        public string LongDescription { get; init; }
        public string ContextualHelp { get; init; }

        private static string ValidateUri(string fileName) =>
            new Uri($"pack://application:,,,/PE_Tools;component/Resources/{fileName}", UriKind.Absolute).ToString();
    }
}