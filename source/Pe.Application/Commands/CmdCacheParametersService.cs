using PeServices.Aps;
using PeServices.Aps.Models;
using PeServices.Storage;

namespace Pe.Application.Commands;

[Transaction(TransactionMode.Manual)]
public class CmdCacheParametersService : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements) {
        var cacheFilename = "parameters-service-cache";
        var apsParamsCache = Storage.GlobalDir().StateJson<ParametersApi.Parameters>(cacheFilename);

        var svcAps = new Aps(new CacheParametersService());
        var _ = Task.Run(async () =>
            await svcAps.Parameters(new CacheParametersService()).GetParameters(
                apsParamsCache, false)
        ).Result;

        return Result.Succeeded;
    }
}

public class CacheParametersService : Aps.IOAuthTokenProvider, Aps.IParametersTokenProvider {
#if DEBUG
    public string GetClientId() => Storage.GlobalDir().SettingsJson().Read().ApsWebClientId1;
    public string GetClientSecret() => Storage.GlobalDir().SettingsJson().Read().ApsWebClientSecret1;
#else
    public string GetClientId() => Storage.GlobalDir().SettingsJson().Read().ApsDesktopClientId1;
    public string GetClientSecret() => null;
#endif
    public string GetAccountId() => Storage.GlobalDir().SettingsJson().Read().Bim360AccountId;
    public string GetGroupId() => Storage.GlobalDir().SettingsJson().Read().ParamServiceGroupId;
    public string GetCollectionId() => Storage.GlobalDir().SettingsJson().Read().ParamServiceCollectionId;
}