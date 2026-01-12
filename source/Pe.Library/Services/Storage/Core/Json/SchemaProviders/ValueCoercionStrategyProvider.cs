using Pe.Library.Services.Storage.Core.Json.SchemaProcessors;
using PeExtensions.FamDocument.SetValue;

namespace Pe.Library.Services.Storage.Core.Json.SchemaProviders;

/// <summary>
///     Provides available ValueCoercionStrategy names for schema generation.
/// </summary>
public class ValueCoercionStrategyProvider : IOptionsProvider {
    public IEnumerable<string> GetExamples() => ValueCoercionStrategyRegistry.GetAllNames();
}