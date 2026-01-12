using Pe.Library.Services.Storage.Core.Json.SchemaProcessors;
using PeExtensions.FamDocument.SetValue;

namespace Pe.Library.Services.Storage.Core.Json.SchemaProviders;

/// <summary>
///     Provides available ParamCoercionStrategy names for schema generation.
/// </summary>
public class ParamCoercionStrategyProvider : IOptionsProvider {
    public IEnumerable<string> GetExamples() => ParamCoercionStrategyRegistry.GetAllNames();
}