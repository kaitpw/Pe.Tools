namespace Pe.Library.Revit.Ui;

internal class UiHelpers {
    internal static RibbonPanel CreateRibbonPanel(
        UIControlledApplication app,
        string tabName,
        string panelName
    ) {
        var curPanel = GetRibbonPanelByName(app, tabName, panelName) ?? app.CreateRibbonPanel(tabName, panelName);
        return curPanel;
    }

    internal static RibbonPanel GetRibbonPanelByName(
        UIControlledApplication app,
        string tabName,
        string panelName
    ) {
        foreach (var tmpPanel in app.GetRibbonPanels(tabName)) {
            if (tmpPanel.Name == panelName)
                return tmpPanel;
        }

        return null;
    }
}

internal class CommandAvailability : IExternalCommandAvailability {
    public bool IsCommandAvailable(
        UIApplication applicationData,
        CategorySet selectedCategories
    ) {
        var result = false;
        var activeDoc = applicationData.ActiveUIDocument;
        if (activeDoc != null && activeDoc.Document != null) result = true;

        return result;
    }
}