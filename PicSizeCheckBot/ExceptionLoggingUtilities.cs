using Microsoft.Extensions.Logging;
using System;

namespace TehGM.WolfBots
{
    /// <summary>Utilities for logging the exception with scope preserved using 'when' keyword.</summary>
    public static class ExceptionLoggingUtilities
    {
        public static bool LogAsCritical(this Exception exception, ILogger log, string message, params object[] args)
        {
            log?.LogCritical(exception, message, args);
            return true;
        }
        public static bool LogAsError(this Exception exception, ILogger log, string message, params object[] args)
        {
            log?.LogError(exception, message, args);
            return true;
        }
        public static bool LogAsWarning(this Exception exception, ILogger log, string message, params object[] args)
        {
            log?.LogWarning(exception, message, args);
            return true;
        }
        public static bool LogAsInformation(this Exception exception, ILogger log, string message, params object[] args)
        {
            log?.LogInformation(exception, message, args);
            return true;
        }
        public static bool LogAsDebug(this Exception exception, ILogger log, string message, params object[] args)
        {
            log?.LogDebug(exception, message, args);
            return true;
        }
        public static bool LogAsTrace(this Exception exception, ILogger log, string message, params object[] args)
        {
            log?.LogTrace(exception, message, args);
            return true;
        }
    }
}
