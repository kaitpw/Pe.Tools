using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Pe.FamilyFoundry.Snapshots;
using Pe.Global.Services.Storage.Core.Json.ContractResolvers;
using Pe.Global.Services.Storage.Core.Json.SchemaProviders;

namespace Pe.FamilyFoundry.Aggregators.Snapshots;

public static class SnapshotSerializer {
    private static readonly JsonSerializerSettings _settings = new() {
        Formatting = Formatting.Indented,
        ContractResolver = new RequiredAwareContractResolver(),
        Converters = [new StringEnumConverter()]
    };

    private static readonly string[] CsvHeaders =
        ["Name", "IsInstance", "IsProjectParameter", "PropertiesGroup", "DataType", "Formula"];

    // JSON

    public static List<ParamSnapshot> SortAndOrder(this List<ParamSnapshot> snapshots) {
        snapshots ??= [];
        return snapshots.Select(s => s with {
            ValuesPerType = new Dictionary<string, string>(
                s.ValuesPerType.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase),
                StringComparer.Ordinal
            )
        }).ToList();
    }

    // CSV (with type columns)
    public static string ToCsv(this List<ParamSnapshot> snapshots) {
        snapshots ??= [];

        var typeNames = snapshots
            .SelectMany(s => s.ValuesPerType.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lines = new List<string> { string.Join(",", CsvHeaders.Concat(typeNames).Select(EscapeCsvField)) };

        foreach (var s in snapshots.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)) {
            var fixedCols = new[] {
                s.Name, s.IsInstance.ToString(), s.IsProjectParameter.ToString(),
                PropertyGroupNamesProvider.GetLabelForForge(s.PropertiesGroup),
                SpecNamesProvider.GetLabelForForge(s.DataType), s.Formula ?? string.Empty
            };

            var valueCols = typeNames
                .Select(typeName =>
                    s.ValuesPerType.TryGetValue(typeName, out var v) ? v ?? string.Empty : string.Empty);

            lines.Add(string.Join(",", fixedCols.Concat(valueCols).Select(EscapeCsvField)));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string EscapeCsvField(string field) {
        field ??= string.Empty;

        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            return $"\"{field.Replace("\"", "\"\"")}\"";

        return field;
    }


    // RefPlaneSpec CSV
    public static string ToCsv(this List<RefPlaneSpec> specs) {
        specs ??= [];

        var lines = new List<string> {
            string.Join(",", new[] { "Name", "AnchorName", "Placement", "Parameter", "Strength" }
                .Select(EscapeCsvField))
        };

        foreach (var s in specs.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)) {
            var cols = new[] {
                s.Name ?? string.Empty, s.AnchorName ?? string.Empty, s.Placement.ToString(),
                s.Parameter ?? string.Empty, s.Strength.ToString()
            };
            lines.Add(string.Join(",", cols.Select(EscapeCsvField)));
        }

        return string.Join(Environment.NewLine, lines);
    }
}