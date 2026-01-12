using Pe.Library.Services.Documents;
using Pe.Library.Services.Storage.Core.Json.SchemaProcessors;

namespace Pe.Library.Services.Storage.Core.Json.SchemaProviders;

/// <summary>
///     Provides category names from the active Revit document for JSON schema examples.
///     Used to enable LSP autocomplete for category name properties.
///     Returns empty list if no document is available (schema generation context).
/// </summary>
public class CategoryNamesProvider : IOptionsProvider {
    public IEnumerable<string> GetExamples() {
        try {
            var doc = DocumentManager.GetActiveDocument();
            // var categories = doc.Settings.Categories;
            if (doc == null || doc.IsFamilyDocument) return [];

            var categories = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .OfClass(typeof(Family))
                .OfType<Family>()
                .Where(f => f.FamilyCategory.CategoryType == CategoryType.Model)
                .Select(f => f.FamilyCategory.Name)
                .Distinct();

            return categories;
        } catch {
            // No document available or error - no examples, no crash
            return [];
        }
    }
}