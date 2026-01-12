using Pe.Library.Services.Storage.Core.Json.SchemaProcessors;
using Pe.Library.Revit.Lib;

namespace Pe.Library.Services.Storage.Core.Json.SchemaProviders;

/// <summary>
///     Provides schedulable parameter names for categories that have schedule profiles.
///     Used to enable LSP autocomplete for schedule field parameter name properties.
///     Uses an in-memory cache populated by CmdCreateSchedule.
/// </summary>
public class SchedulableParameterNamesProvider : IOptionsProvider {
    private static readonly object _lock = new();
    private static HashSet<string> _cachedParameters = new(StringComparer.Ordinal);
    private static DateTime _cacheGeneratedAt = DateTime.MinValue;

    public IEnumerable<string> GetExamples() {
        Debug.WriteLine("[SchedulableParameterNamesProvider] GetExamples() called");

        lock (_lock) {
            if (_cachedParameters.Count > 0) {
                Debug.WriteLine(
                    $"[SchedulableParameterNamesProvider] Returning {_cachedParameters.Count} cached parameters (generated: {_cacheGeneratedAt})");
                return _cachedParameters.OrderBy(name => name).ToList();
            }

            Debug.WriteLine("[SchedulableParameterNamesProvider] No cache available - returning empty");
            return [];
        }
    }

    /// <summary>
    ///     Updates the in-memory cache with schedulable parameters for the given categories.
    ///     Must be called from within a valid Revit API context (e.g., from a command, not from palette callbacks).
    /// </summary>
    /// <param name="doc">The Revit document</param>
    /// <param name="categories">List of category names to query parameters for</param>
    public static void UpdateCache(Document doc, IEnumerable<string> categories) {
        Debug.WriteLine("[SchedulableParameterNamesProvider] UpdateCache() called");

        if (doc == null || doc.IsFamilyDocument) {
            Debug.WriteLine("[SchedulableParameterNamesProvider] Invalid document for cache update");
            return;
        }

        var categoryList = categories.ToList();
        Debug.WriteLine(
            $"[SchedulableParameterNamesProvider] Updating cache for {categoryList.Count} categories: {string.Join(", ", categoryList)}");

        var parameterNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var categoryName in categoryList) {
            try {
                Debug.WriteLine($"[SchedulableParameterNamesProvider] Querying: {categoryName}");
                var categoryParams = ScheduleHelper.GetSchedulableParameterNames(doc, categoryName);
                Debug.WriteLine(
                    $"[SchedulableParameterNamesProvider] Found {categoryParams.Count} parameters for {categoryName}");
                foreach (var param in categoryParams)
                    _ = parameterNames.Add(param);
            } catch (Exception ex) {
                Debug.WriteLine($"[SchedulableParameterNamesProvider] Error querying {categoryName}: {ex.Message}");
            }
        }

        lock (_lock) {
            _cachedParameters = parameterNames;
            _cacheGeneratedAt = DateTime.Now;
            Debug.WriteLine(
                $"[SchedulableParameterNamesProvider] Cache updated with {_cachedParameters.Count} parameters at {_cacheGeneratedAt}");
        }
    }
}