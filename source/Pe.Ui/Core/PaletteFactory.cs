using PeServices.Storage;
using PeUi.Components;
using PeUi.Core.Services;
using PeUi.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace PeUi.Core;

/// <summary>
///     Factory for creating palette windows using composition instead of inheritance.
///     Handles the boilerplate of wiring up SearchFilterService, PaletteViewModel, Palette, and EphemeralWindow.
/// </summary>
/// <example>
///     Basic usage with persistence and filtering:
///     <code>
///     var window = PaletteFactory.Create("Schedule Palette", items, actions,
///         new PaletteOptions&lt;SchedulePaletteItem&gt; {
///             Storage = new Storage(nameof(CmdPltSchedules)),
///             PersistenceKey = item => item.Schedule.Id.ToString(),
///             SearchConfig = SearchConfig.PrimaryAndSecondary(),
///             FilterKeySelector = item => item.TextPill
///         });
///     window.Show();
///     </code>
/// </example>
/// <example>
///     Minimal usage (search enabled, no persistence):
///     <code>
///     var window = PaletteFactory.Create("My Palette", items, actions);
///     window.Show();
///     </code>
/// </example>
public static class PaletteFactory {
    /// <summary>
    ///     Creates an EphemeralWindow containing a fully configured palette.
    /// </summary>
    /// <typeparam name="TItem">The palette item type (must implement IPaletteListItem)</typeparam>
    /// <param name="title">Window title displayed in the floating pill</param>
    /// <param name="items">Items to display in the palette list</param>
    /// <param name="actions">Actions available for items (first action with no modifiers is the default)</param>
    /// <param name="options">Optional configuration. If null, uses defaults (search enabled, no persistence)</param>
    /// <returns>An EphemeralWindow ready to show</returns>
    public static EphemeralWindow Create<TItem>(
        string title,
        IEnumerable<TItem> items,
        List<PaletteAction<TItem>> actions,
        PaletteOptions<TItem> options = null
    ) where TItem : class, IPaletteListItem {
        options ??= new PaletteOptions<TItem>();

        // Create search service - with or without persistence based on configuration
        var searchService = options.Storage != null && options.PersistenceKey != null
            ? new SearchFilterService<TItem>(options.Storage, options.PersistenceKey, options.SearchConfig)
            : new SearchFilterService<TItem>(options.SearchConfig);

        // Create view model with optional debounce delay
        var viewModel = new PaletteViewModel<TItem>(
            items,
            searchService,
            options.FilterKeySelector,
            options.SelectionDebounceMs
        );
        options.ViewModelMutator?.Invoke(viewModel);

        // Create palette - hide search box if search is disabled
        var isSearchDisabled = options.SearchConfig == null;
        var palette = new Palette(isSearchDisabled);

        // Create Ctrl-release callback if provided
        // Pass viewModel reference so callback can read current SelectedItem when Ctrl is released
        Action onCtrlReleased = null;
        if (options.OnCtrlReleased != null) {
            var vmRef = viewModel; // Capture viewModel reference
            onCtrlReleased = options.OnCtrlReleased(vmRef);
        }

        // Wire up selection changed callback if provided (immediate, for highlighting)
        if (options.OnSelectionChanged != null) {
            viewModel.PropertyChanged += (_, e) => {
                if (e.PropertyName == nameof(viewModel.SelectedItem))
                    options.OnSelectionChanged(viewModel.SelectedItem);
            };
        }

        // Wire up debounced selection changed callback if provided (delayed, for expensive operations)
        if (options.OnSelectionChangedDebounced != null) {
            viewModel.SelectionChangedDebounced += (_, _) =>
                options.OnSelectionChangedDebounced(viewModel.SelectedItem);
        }

        palette.Initialize(viewModel, actions, options.CustomKeyBindings, onCtrlReleased, options.Sidebar,
            options.KeepOpenAfterAction);

        var window = new EphemeralWindow(palette, title);

        // Wire up parent window reference so palette can coordinate window sizing
        palette.SetParentWindow(window);

        return window;
    }
}

/// <summary>
///     Defines a sidebar for the palette.
/// </summary>
public class PaletteSidebar {
    /// <summary>
    ///     The UserControl to display in the sidebar.
    /// </summary>
    public UIElement Content { get; init; }

    /// <summary>
    ///     Initial state of the sidebar (collapsed or expanded).
    /// </summary>
    public SidebarState InitialState { get; init; } = SidebarState.Collapsed;

    /// <summary>
    ///     Width of the sidebar when expanded.
    /// </summary>
    public GridLength Width { get; init; } = new(400);

    /// <summary>
    ///     Keys that will collapse the sidebar and return focus to the main palette.
    /// </summary>
    public List<Key> ExitKeys { get; init; } = [Key.Escape];
}

/// <summary>
///     Sidebar state enumeration.
/// </summary>
public enum SidebarState {
    Collapsed,
    Expanded
}

/// <summary>
///     Configuration options for <see cref="PaletteFactory.Create{TItem}" />.
///     All properties are optional - use only what you need.
/// </summary>
/// <typeparam name="TItem">The palette item type</typeparam>
public class PaletteOptions<TItem> where TItem : class, IPaletteListItem {
    /// <summary>
    ///     Storage instance for persisting usage data. Required for persistence to work.
    ///     Default: null (no persistence)
    /// </summary>
    /// <example>
    ///     <code>Storage = new Storage(nameof(MyCmdClass))</code>
    /// </example>
    public Storage Storage { get; init; }

    /// <summary>
    ///     Function that returns a unique key for each item, used for persistence.
    ///     Required (along with Storage) for persistence to work.
    ///     Default: null (no persistence)
    /// </summary>
    /// <example>
    ///     <code>PersistenceKey = item => item.Element.Id.ToString()</code>
    /// </example>
    public Func<TItem, string> PersistenceKey { get; init; }

    /// <summary>
    ///     Search configuration controlling which fields to search and scoring weights.
    ///     Default: <see cref="Services.SearchConfig.Default()" /> (searches TextPrimary only).
    ///     Set to null to disable search entirely (hides the search box).
    /// </summary>
    /// <example>
    ///     <code>
    ///     // Search both name and description:
    ///     SearchConfig = SearchConfig.PrimaryAndSecondary()
    ///     
    ///     // Disable search entirely:
    ///     SearchConfig = null
    ///     </code>
    /// </example>
    public SearchConfig SearchConfig { get; init; } = SearchConfig.Default();

    /// <summary>
    ///     Function that extracts a filter category from each item, enabling dropdown filtering.
    ///     Default: null (filtering disabled)
    /// </summary>
    /// <example>
    ///     <code>
    ///     // Filter by view type:
    ///     FilterKeySelector = item => item.View.ViewType.ToString()
    ///     
    ///     // Filter by category:
    ///     FilterKeySelector = item => item.TextPill
    ///     </code>
    /// </example>
    public Func<TItem, string> FilterKeySelector { get; init; }

    /// <summary>
    ///     Custom keyboard bindings for navigation.
    ///     Default: null (uses only built-in arrow key navigation)
    /// </summary>
    /// <example>
    ///     <code>
    ///     var keys = new CustomKeyBindings();
    ///     keys.Add(Key.OemTilde, NavigationAction.MoveDown, ModifierKeys.Control);
    ///     CustomKeyBindings = keys;
    ///     </code>
    /// </example>
    public CustomKeyBindings CustomKeyBindings { get; init; }

    /// <summary>
    ///     Callback to mutate the view model after creation but before palette initialization.
    ///     Useful for setting initial selection state.
    ///     Default: null (no mutation)
    /// </summary>
    /// <example>
    ///     <code>
    ///     // Select second item for MRU-style palettes:
    ///     ViewModelMutator = vm => { if (vm.FilteredItems.Count > 1) vm.SelectedIndex = 1; }
    ///     </code>
    /// </example>
    public Action<PaletteViewModel<TItem>> ViewModelMutator { get; init; }

    /// <summary>
    ///     Factory function that receives the view model and returns an action to execute when Ctrl is released.
    ///     Used for "hold Ctrl to browse, release to select" behavior (like Alt+Tab).
    ///     The returned action should read the current SelectedItem when executed (not when created).
    ///     Default: null (no Ctrl-release behavior)
    /// </summary>
    /// <example>
    ///     <code>
    ///     OnCtrlReleased = vm => () => {
    ///         // Read current SelectedItem when Ctrl is released (not at window creation)
    ///         var selected = vm.SelectedItem;
    ///         if (selected?.View != null)
    ///             uiapp.ActiveUIDocument.ActiveView = selected.View;
    ///     }
    ///     </code>
    /// </example>
    public Func<PaletteViewModel<TItem>, Action> OnCtrlReleased { get; init; }

    /// <summary>
    ///     Callback invoked when the selected item changes in the palette.
    ///     Useful for highlighting or previewing the currently selected element.
    ///     Default: null (no selection change behavior)
    /// </summary>
    /// <example>
    ///     <code>
    ///     OnSelectionChanged = item => {
    ///         if (item?.ElementId != null)
    ///             highlighter.Highlight(item.ElementId);
    ///     }
    ///     </code>
    /// </example>
    public Action<TItem> OnSelectionChanged { get; init; }

    /// <summary>
    ///     Callback invoked when the selected item changes, after a debounce delay.
    ///     Useful for expensive operations like loading JSON, building previews, or IO.
    ///     The delay prevents triggering on every arrow key press.
    ///     Default: null (no debounced selection change behavior)
    /// </summary>
    /// <example>
    ///     <code>
    ///     OnSelectionChangedDebounced = item => {
    ///         if (item != null)
    ///             BuildExpensivePreview(item); // Only fires after user stops navigating
    ///     }
    ///     </code>
    /// </example>
    public Action<TItem> OnSelectionChangedDebounced { get; init; }

    /// <summary>
    ///     Debounce delay in milliseconds for OnSelectionChangedDebounced callback.
    ///     Default: 300ms
    /// </summary>
    public int SelectionDebounceMs { get; init; } = 300;

    /// <summary>
    ///     Sidebar definition for the palette.
    ///     Sidebars appear to the right of the main list and can be expanded/collapsed.
    ///     Default: null (no sidebar)
    /// </summary>
    /// <example>
    ///     <code>
    ///     Sidebar = new PaletteSidebar {
    ///         Content = previewPanel,
    ///         InitialState = SidebarState.Collapsed,
    ///         Width = new GridLength(400),
    ///         ExitKeys = [Key.Escape]
    ///     }
    ///     </code>
    /// </example>
    public PaletteSidebar Sidebar { get; init; }

    /// <summary>
    ///     When true, prevents the palette from closing after action execution.
    ///     Action executes immediately (not deferred) and the palette stays open.
    ///     Useful for multi-item workflows like placing multiple families.
    ///     Default: false (palette closes after action, execution is deferred)
    /// </summary>
    /// <example>
    ///     <code>
    ///     // For multi-placement workflows:
    ///     KeepOpenAfterAction = true
    ///     </code>
    /// </example>
    public bool KeepOpenAfterAction { get; init; } = false;
}