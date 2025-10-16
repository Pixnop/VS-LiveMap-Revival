using Vintagestory.API.Common;

namespace livemap.common.util;

public abstract class Logger {
    private static ILogger Log() {
        return LiveMapModSystem.Logger!;
    }

    private static string Prefix(string message) {
        return $"[LiveMap] {message}";
    }

    public static void Chat(string message) => Log().Chat(Prefix(message));
    public static void Chat(string format, params object[] args) => Log().Chat(Prefix(format), args);

    public static void Event(string message) => Log().Event(Prefix(message));
    public static void Event(string format, params object[] args) => Log().Event(Prefix(format), args);

    public static void StoryEvent(string message) => Log().StoryEvent(Prefix(message));
    public static void StoryEvent(string format, params object[] args) => Log().StoryEvent(Prefix(format), args);

    public static void Build(string message) => Log().Build(Prefix(message));
    public static void Build(string format, params object[] args) => Log().Build(Prefix(format), args);

    public static void VerboseDebug(string message) => Log().VerboseDebug(Prefix(message));
    public static void VerboseDebug(string format, params object[] args) => Log().VerboseDebug(Prefix(format), args);

    public static void Debug(string message) => Log().Debug(Prefix(message));
    public static void Debug(string format, params object[] args) => Log().Debug(Prefix(format), args);

    public static void Notification(string message) => Log().Notification(Prefix(message));
    public static void Notification(string format, params object[] args) => Log().Notification(Prefix(format), args);

    public static void Warning(string message) => Log().Warning(Prefix(message));
    public static void Warning(string format, params object[] args) => Log().Warning(Prefix(format), args);
    public static void Warning(Exception e) => Log().Warning(e);

    public static void Error(string message) => Log().Error(Prefix(message));
    public static void Error(string format, params object[] args) => Log().Error(Prefix(format), args);
    public static void Error(Exception e) => Log().Error(e);

    public static void Fatal(string message) => Log().Fatal(Prefix(message));
    public static void Fatal(string format, params object[] args) => Log().Fatal(Prefix(format), args);
    public static void Fatal(Exception e) => Log().Fatal(e);

    public static void Audit(string message) => Log().Audit(Prefix(message));
    public static void Audit(string format, params object[] args) => Log().Audit(Prefix(format), args);
}
