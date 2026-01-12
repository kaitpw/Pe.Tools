#nullable enable
using Pe.Extensions.FamilyDocument.SetValue.CoercionStrategies;
using BCS = Pe.Extensions.FamilyDocument.SetValue.BuiltInCoercionStrategy;

namespace Pe.Extensions.FamilyDocument.SetValue;

public enum BuiltInCoercionStrategy {
    Strict,
    CoerceByStorageType,
    CoerceMeasurableToNumber
}

/// <summary>
///     Registry for ParamCoercionStrategy implementations.
///     Allows runtime registration of custom coercion strategies and dynamic discovery of available strategies.
/// </summary>
public static class ParamCoercionStrategyRegistry {
    private static readonly Dictionary<string, Func<ICoercionStrategy>> _factories = new();

    static ParamCoercionStrategyRegistry() {
        Register(BuiltInCoercionStrategy.Strict.ToString(), () => new Strict());
        Register(BuiltInCoercionStrategy.CoerceByStorageType.ToString(), () => new CoerceByStorageType());

        // CoerceMeasurableToNumber with fallback to CoerceByStorageType
        // Tries unit conversion first, falls back to raw value copy if no mapping exists
        Register(BuiltInCoercionStrategy.CoerceMeasurableToNumber.ToString(), () => new CompositeStrategy(
            new CoerceMeasurableToNumber(),
            new CoerceByStorageType()
        ));

        Register("CoerceElectrical", () => new CoerceElectrical());
    }

    /// <summary>
    ///     Register a new coercion strategy factory.
    /// </summary>
    /// <param name="name">Strategy name (should match enum value for C# usage)</param>
    /// <param name="factory">Factory function to create strategy instances</param>
    public static void Register(string name, Func<ICoercionStrategy> factory) => _factories[name] = factory;

    /// <summary>
    ///     Get a coercion strategy instance by name.
    /// </summary>
    /// <param name="name">Strategy name</param>
    /// <returns>New strategy instance</returns>
    /// <exception cref="KeyNotFoundException">Thrown if strategy name is not registered</exception>
    public static ICoercionStrategy Get(string name) {
        if (!_factories.TryGetValue(name, out var factory)) {
            throw new KeyNotFoundException(
                $"Coercion strategy '{name}' not found. Available strategies: {string.Join(", ", GetAllNames())}");
        }

        return factory();
    }

    /// <summary>
    ///     Get all registered strategy names.
    /// </summary>
    /// <returns>Collection of strategy names</returns>
    public static IEnumerable<string> GetAllNames() => _factories.Keys;
}

/// <summary>
///     Registry for ValueCoercionStrategy implementations.
///     Allows runtime registration of custom coercion strategies and dynamic discovery of available strategies.
/// </summary>
public static class ValueCoercionStrategyRegistry {
    private static readonly Dictionary<string, Func<ICoercionStrategy>> _factories = new();

    static ValueCoercionStrategyRegistry() {
        Register(BuiltInCoercionStrategy.Strict.ToString(), () => new Strict());
        Register(BuiltInCoercionStrategy.CoerceByStorageType.ToString(), () => new CoerceByStorageType());

        // CoerceMeasurableToNumber with fallback to CoerceByStorageType
        // Tries unit conversion first, falls back to raw value copy if no mapping exists
        Register(BuiltInCoercionStrategy.CoerceMeasurableToNumber.ToString(), () => new CompositeStrategy(
            new CoerceMeasurableToNumber(),
            new CoerceByStorageType()
        ));
    }

    /// <summary>
    ///     Register a new coercion strategy factory.
    /// </summary>
    /// <param name="name">Strategy name (should match enum value for C# usage)</param>
    /// <param name="factory">Factory function to create strategy instances</param>
    public static void Register(string name, Func<ICoercionStrategy> factory) => _factories[name] = factory;

    /// <summary>
    ///     Get a coercion strategy instance by name.
    /// </summary>
    /// <param name="name">Strategy name</param>
    /// <returns>New strategy instance</returns>
    /// <exception cref="KeyNotFoundException">Thrown if strategy name is not registered</exception>
    public static ICoercionStrategy Get(string name) {
        if (!_factories.TryGetValue(name, out var factory)) {
            throw new KeyNotFoundException(
                $"Coercion strategy '{name}' not found. Available strategies: {string.Join(", ", GetAllNames())}");
        }

        return factory();
    }

    /// <summary>
    ///     Get all registered strategy names.
    /// </summary>
    /// <returns>Collection of strategy names</returns>
    public static IEnumerable<string> GetAllNames() => _factories.Keys;
}