namespace livemap.common.util;

public abstract class Lang {
    public static string Get(string key, params object[]? args) {
        return Vintagestory.API.Config.Lang.Get(key.Contains(':') ? key : $"livemap:{key}", args);
    }
}
