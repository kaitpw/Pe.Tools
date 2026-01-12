#nullable enable
namespace Pe.Extensions.FamDocument.SetValue.CoercionStrategies;

/// <summary>
///     Coerced mapping strategy - performs reasonable type coercions between compatible storage types.
/// </summary>
/// <exception cref="T:System.ArgumentException">Invalid value type</exception>
/// <exception cref="T:Autodesk.Revit.Exceptions.ArgumentException">
///     Thrown when the input argument-"targetParam"-is an invalid family parameter.
///     --or-- When the storage type of family parameter is not ElementId
///     --or-- The input ElementId does not represent either a valid element in the document or InvalidElementId.
/// </exception>
/// <exception cref="T:Autodesk.Revit.Exceptions.ArgumentOutOfRangeException">
///     Thrown when the input argument-"targetParam"-is out of range.
///     --or-- Thrown when the input ElementId is not valid as a value for this FamilyParameter.
/// </exception>
/// <exception cref="T:Autodesk.Revit.Exceptions.InvalidOperationException">
///     Thrown when the family parameter is determined by formula,
///     or the current family type is invalid.
/// </exception>
public class CoerceSimple : ICoercionStrategy {
    public bool CanMap(CoercionContext context) {
        // Can always map if storage types match
        if (context.SourceStorageType == context.TargetStorageType)
            return true;

        // Check if coercion is possible
        return (context.SourceValue, context.TargetStorageType) switch {
            (bool, StorageType.Integer) => true,
            (int, StorageType.Double) => true,
            (string str, StorageType.Double) => CanParseAsDouble(str, context),
            (string str, StorageType.Integer) => CanParseAsInteger(str, context),
            (string str, StorageType.ElementId) => CanParseAsElementId(str),
            _ => false
        };
    }

    public Result<FamilyParameter> Map(CoercionContext context) {
        var fm = context.FamilyManager;
        var target = context.TargetParam;

        switch (context.SourceValue) {
        case bool boolValue when context.TargetStorageType == StorageType.Integer:
            fm.Set(target, boolValue ? 1 : 0);
            return target;
        case double doubleValue:
            fm.Set(target, doubleValue);
            return target;
        case int intValue:
            if (context.TargetStorageType == StorageType.Double)
                fm.Set(target, (double)intValue);
            else
                fm.Set(target, intValue);
            return target;
        case string stringValue:
            if (context.TargetStorageType == StorageType.Double) {
                var dataType = context.TargetDataType;
                // Try unit-formatted parsing first for measurable specs (e.g., "10'", "120V", "35 SF")
                if (UnitUtils.IsMeasurableSpec(dataType)) {
                    var units = context.FamilyDocument.GetUnits();
                    if (UnitFormatUtils.TryParse(units, dataType, stringValue, out var parsed)) {
                        fm.Set(target, parsed);
                        return target;
                    }
                }

                // Fallback to plain number parsing
                fm.Set(target, double.Parse(stringValue));
            } else if (context.TargetStorageType == StorageType.Integer)
                fm.Set(target, ParseAsInteger(stringValue, context));
            else if (context.TargetStorageType == StorageType.ElementId) {
                // Parse ElementId from format: "ElementName [ID:12345]" or "[ID:12345]"
                if (TryParseElementId(stringValue, out var idValue))
                    fm.Set(target, new ElementId(idValue));
                else
                    return new ArgumentException(
                        $"Cannot parse ElementId from string: '{stringValue}'. Expected format: 'ElementName [ID:12345]' or '[ID:12345]'");
            } else
                fm.Set(target, stringValue);

            return target;
        case ElementId elementIdValue:
            fm.Set(target, elementIdValue);
            return target;
        default:
            return new ArgumentException($"Invalid type of value to set ({context.SourceValue.GetType().Name})");
        }
    }

    private static bool CanParseAsInteger(string str, CoercionContext context) {
        // Handle "Yes"/"No" strings for Yes/No parameters
        if (context.TargetDataType == SpecTypeId.Boolean.YesNo) {
            return str.Equals("Yes", StringComparison.OrdinalIgnoreCase)
                   || str.Equals("No", StringComparison.OrdinalIgnoreCase);
        }

        return int.TryParse(str, out _);
    }

    private static int ParseAsInteger(string str, CoercionContext context) {
        // Handle "Yes"/"No" strings for Yes/No parameters
        if (context.TargetDataType == SpecTypeId.Boolean.YesNo)
            return str.Equals("Yes", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        return int.Parse(str);
    }

    private static bool CanParseAsDouble(string str, CoercionContext context) {
        // Try plain number parsing first
        if (double.TryParse(str, out _))
            return true;

        // For measurable specs, try unit-formatted parsing
        var dataType = context.TargetDataType;
        if (UnitUtils.IsMeasurableSpec(dataType)) {
            var units = context.FamilyDocument.GetUnits();
            return UnitFormatUtils.TryParse(units, dataType, str, out _);
        }

        return false;
    }

    /// <summary>
    ///     Checks if a string can be parsed as an ElementId.
    ///     Supports formats: "[ID:12345]" or "ElementName [ID:12345]"
    /// </summary>
    private static bool CanParseAsElementId(string str) => TryParseElementId(str, out _);

    /// <summary>
    ///     Parses ElementId from string formats: "[ID:12345]" or "ElementName [ID:12345]"
    ///     Returns the integer ID value.
    /// </summary>
    private static bool TryParseElementId(string str, out int idValue) {
        idValue = -1;
        if (string.IsNullOrWhiteSpace(str)) return false;

        // Look for pattern "[ID:12345]"
        var idStart = str.IndexOf("[ID:", StringComparison.Ordinal);
        if (idStart == -1) return false;

        var idEnd = str.IndexOf("]", idStart, StringComparison.Ordinal);
        if (idEnd == -1) return false;

        var idString = str.Substring(idStart + 4, idEnd - idStart - 4);
        return int.TryParse(idString, out idValue);
    }
}