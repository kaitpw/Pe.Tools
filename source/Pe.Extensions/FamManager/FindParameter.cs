namespace Pe.Extensions.FamManager;

public static class FamilyManagerFindParameter {
    /// <summary>
    ///     Find a parameter by ForgeTypeId identifier
    /// </summary>
    /// <param name="familyManager">The family manager</param>
    /// <param name="parameter">Identifier of the built-in parameter</param>
    /// <exception cref="T:Autodesk.Revit.Exceptions.ArgumentException">
    ///     ForgeTypeId does not identify a built-in parameter.
    /// </exception>
    public static FamilyParameter FindParameter(this FamilyManager familyManager, ForgeTypeId parameter) =>
        familyManager.GetParameter(parameter);

    /// <summary>
    ///     Find a parameter by built-in parameter identifier. Returns null if the parameter is not found
    /// </summary>
    /// <param name="familyManager">The family manager</param>
    /// <param name="parameter">The built-in parameter ID</param>
    public static FamilyParameter FindParameter(this FamilyManager familyManager, BuiltInParameter parameter) =>
        familyManager.get_Parameter(parameter);

    /// <summary>
    ///     Find a parameter by definition. Returns null if the parameter is not found
    /// </summary>
    /// <param name="familyManager">The family manager</param>
    /// <param name="definition">The internal or external definition of the parameter</param>
    public static FamilyParameter FindParameter(this FamilyManager familyManager, Definition definition) =>
        familyManager.get_Parameter(definition);

    /// <summary>
    ///     Find a shared parameter by GUID. Returns null if the parameter is not found
    /// </summary>
    /// <param name="familyManager">The family manager</param>
    /// <param name="guid">The unique id associated with the shared parameter</param>
    public static FamilyParameter FindParameter(this FamilyManager familyManager, Guid guid) =>
        familyManager.get_Parameter(guid);

    /// <summary>
    ///     Find a parameter by name. Returns null if the parameter is not found
    /// </summary>
    /// <param name="familyManager">The family manager</param>
    /// <param name="name">The name of the parameter to be found</param>
    public static FamilyParameter? FindParameter(this FamilyManager familyManager, string name) =>
        familyManager.get_Parameter(name);
}