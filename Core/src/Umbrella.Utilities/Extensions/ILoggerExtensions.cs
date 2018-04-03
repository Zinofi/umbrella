﻿using System;
using System.Runtime.CompilerServices;
using System.Text;
using Umbrella.Utilities;

namespace Microsoft.Extensions.Logging
{
    public static class ILoggerExtensions
    {
        #region Public Static Methods
        public static void WriteDebug(this ILogger log, object state = null, string message = null, [CallerMemberName]string methodName = "", [CallerFilePath]string filePath = "", [CallerLineNumber]int lineNumber = 0)
            => LogDetails(log, LogLevel.Debug, null, state, message, methodName, filePath, lineNumber);

        public static void WriteTrace(this ILogger log, object state = null, string message = null, [CallerMemberName]string methodName = "", [CallerFilePath]string filePath = "", [CallerLineNumber]int lineNumber = 0)
            => LogDetails(log, LogLevel.Trace, null, state, message, methodName, filePath, lineNumber);

        public static void WriteInformation(this ILogger log, object state = null, string message = null, [CallerMemberName]string methodName = "", [CallerFilePath]string filePath = "", [CallerLineNumber]int lineNumber = 0)
            => LogDetails(log, LogLevel.Information, null, state, message, methodName, filePath, lineNumber);

        public static bool WriteWarning(this ILogger log, Exception exc = null, object state = null, string message = null, bool returnValue = false, [CallerMemberName]string methodName = "", [CallerFilePath]string filePath = "", [CallerLineNumber]int lineNumber = 0)
        {
            LogDetails(log, LogLevel.Error, exc, state, message, methodName, filePath, lineNumber);

            return returnValue;
        }

        public static bool WriteError(this ILogger log, Exception exc, object state = null, string message = null, bool returnValue = false, [CallerMemberName]string methodName = "", [CallerFilePath]string filePath = "", [CallerLineNumber]int lineNumber = 0)
        {
            LogDetails(log, LogLevel.Error, exc, state, message, methodName, filePath, lineNumber);

            return returnValue;
        }

        public static bool WriteCritical(this ILogger log, Exception exc, object state = null, string message = null, bool returnValue = false, [CallerMemberName]string methodName = "", [CallerFilePath]string filePath = "", [CallerLineNumber]int lineNumber = 0)
        {
            LogDetails(log, LogLevel.Critical, exc, state, message, methodName, filePath, lineNumber);

            return returnValue;
        }
        #endregion

        #region Private Static Methods
        private static void LogDetails(ILogger log, LogLevel level, Exception exc, object state, string message, string methodName, string filePath, int lineNumber)
        {
            StringBuilder messageBuilder = new StringBuilder();

            if (state != null)
            {
                string jsonState = UmbrellaStatics.SerializeJson(state);
                messageBuilder.Append($"{methodName}({jsonState})");
            }
            else
            {
                messageBuilder.Append($"{methodName}()");
            }

            if (level >= LogLevel.Error)
                messageBuilder.Append(" failed");

            if (!string.IsNullOrEmpty(message))
                messageBuilder.Append(" - " + message);

            messageBuilder.Append($" on Line: {lineNumber}, Path: {filePath}");

            string output = messageBuilder.ToString();

            switch (level)
            {
                case LogLevel.Debug:
                    log.LogDebug(output);
                    break;
                case LogLevel.Trace:
                    log.LogTrace(output);
                    break;
                case LogLevel.Information:
                    log.LogInformation(output);
                    break;
                case LogLevel.Warning when exc != null:
                    log.LogWarning(exc, output);
                    break;
                case LogLevel.Warning:
                    log.LogWarning(output);
                    break;
                case LogLevel.Error:
                    log.LogError(exc, output);
                    break;
                case LogLevel.Critical:
                    log.LogCritical(exc, output);
                    break;
            }
        }
        #endregion
    }
}