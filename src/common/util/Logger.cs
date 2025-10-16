using Vintagestory.API.Common;

namespace livemap.common.util;

public abstract class Logger {
    private static ILogger Log() {
        return LiveMapModSystem.Logger;
    }

    public static void Chat(string message) => Log().Chat(message);
    public static void Chat(string format, params object[] args) => Log().Chat(format, args);

    public static void Event(string message) => Log().Event(message);
    public static void Event(string format, params object[] args) => Log().Event(format, args);

    public static void StoryEvent(string message) => Log().StoryEvent(message);
    public static void StoryEvent(string format, params object[] args) => Log().StoryEvent(format, args);

    public static void Build(string message) => Log().Build(message);
    public static void Build(string format, params object[] args) => Log().Build(format, args);

    public static void VerboseDebug(string message) => Log().VerboseDebug(message);
    public static void VerboseDebug(string format, params object[] args) => Log().VerboseDebug(format, args);

    public static void Debug(string message) => Log().Debug(message);
    public static void Debug(string format, params object[] args) => Log().Debug(format, args);

    public static void Notification(string message) => Log().Notification(message);
    public static void Notification(string format, params object[] args) => Log().Notification(format, args);

    public static void Warning(string message) => Log().Warning(message);
    public static void Warning(string format, params object[] args) => Log().Warning(format, args);
    public static void Warning(Exception e) => Log().Warning(e);

    public static void Error(string message) => Log().Error(message);
    public static void Error(string format, params object[] args) => Log().Error(format, args);
    public static void Error(Exception e) => Log().Error(e);

    public static void Fatal(string message) => Log().Fatal(message);
    public static void Fatal(string format, params object[] args) => Log().Fatal(format, args);
    public static void Fatal(Exception e) => Log().Fatal(e);

    public static void Audit(string message) => Log().Audit(message);
    public static void Audit(string format, params object[] args) => Log().Audit(format, args);
}
