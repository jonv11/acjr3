namespace Acjr3.App;

public static class RuntimeConfigLoader
{
    internal static bool TryLoadValidatedConfig(bool requireAuth, IAppLogger logger, out Acjr3Config? config, out string error)
    {
        var source = new EnvironmentVariableSource();
        var loader = new ConfigLoader(source);
        if (!loader.TryLoad(out config, out error))
        {
            return false;
        }

        if (requireAuth && !ConfigValidator.TryValidateAuth(config!, out error))
        {
            return false;
        }

        logger.Verbose($"Loaded config site={config!.SiteUrl} auth={config.AuthMode}");
        return true;
    }
}
