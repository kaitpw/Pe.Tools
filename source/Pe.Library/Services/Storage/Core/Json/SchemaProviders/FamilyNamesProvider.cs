using Pe.Library.Services.Documents;
using Pe.Library.Services.Storage.Core.Json.SchemaProcessors;

namespace Pe.Library.Services.Storage.Core.Json.SchemaProviders;

/// <summary>
///     Provides family names from the active Revit document for JSON schema examples.
///     Used to enable LSP autocomplete for family name properties.
///     Returns empty list if no document is available (schema generation context).
/// </summary>
public class FamilyNamesProvider : IOptionsProvider {
    public IEnumerable<string> GetExamples() {
        try {
            var doc = DocumentManager.GetActiveDocument();
            if (doc == null) return [];
            var families = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Select(f => f.Name)
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct()
                .OrderBy(name => name);

            return families;
        } catch {
            // No document available or error - no examples, no crash
            return [];
        }
    }
}