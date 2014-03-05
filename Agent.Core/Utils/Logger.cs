using System;
using NLog.Config;
using NLog.Targets;
using NLog;
using System.IO;

namespace Agent.Core.Utils
{
    public static class Logger
    {
        private static bool _initialized = false;
        private static NLog.Logger _logger;

        public static void Initialize(string logName)
        {
            CreateLogConfig(logName + ".log");
            _initialized = true;
        }

        private static void CreateLogConfig(string logFile)
        {
            // Create configuration object 
            var config = new LoggingConfiguration();

            // Create targets and add them to the configuration 
            var consoleTarget = new ConsoleTarget();
            config.AddTarget("console", consoleTarget);

            var fileTarget = new FileTarget();
            config.AddTarget("logFile", fileTarget);

            // Set target properties 
            consoleTarget.Layout = "${level:uppercase=true} : ${date:format=G:culture=en-US} : ${message}";
            fileTarget.FileName = Path.Combine(Settings.LogDirectory, logFile);
            fileTarget.Layout = "${level:uppercase=true} : ${date:format=G:culture=en-US} : ${message}";

            // Define rules
            var rule1 = new LoggingRule("*", Settings.LogLevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            var rule2 = new LoggingRule("*", Settings.LogLevel, fileTarget);
            config.LoggingRules.Add(rule2);

            LogManager.Configuration = config;
            _logger = LogManager.GetLogger("log.all");
        }

        public static void Log(string entry, LogLevel level = LogLevel.Info)
        {
            if (!_initialized) return;
            switch (level)
            {
                case LogLevel.Debug:
                    _logger.Debug(entry);
                    break;
                case LogLevel.Info:
                    _logger.Info(entry);
                    break;
                case LogLevel.Warning:
                    _logger.Warn(entry);
                    break;
                case LogLevel.Error:
                    _logger.Error(entry);
                    break;
                case LogLevel.Critical:
                    _logger.Fatal(entry);
                    break;
                default:
                    _logger.Info(entry);
                    break;
            }
        }

        public static void Log(string entryFormat, LogLevel level = LogLevel.Info, params object[] args)
        {
            Log(String.Format(entryFormat, args), level);
        }

        public static void LogException(Exception e)
        {
            Log("Exception: {0}", LogLevel.Error, e.Message);
            if (e.InnerException != null)
            {
                Log("Inner exception: {0}", LogLevel.Error, e.InnerException.Message);
            }
            Log("Stack trace: {0}", LogLevel.Error, e);
        }

        public static void Exception(Exception e)
        {
            LogException(e);
        }

        public static void Info(string entryFormat, params object[] args)
        {
            Log(String.Format(entryFormat, args), LogLevel.Info);
        }

        public static void Debug(string entryFormat, params object[] args)
        {
            Log(String.Format(entryFormat, args), LogLevel.Debug);
        }

        public static void Warning(string entryFormat, params object[] args)
        {
            Log(String.Format(entryFormat, args), LogLevel.Warning);
        }

        public static void Critical(string entryFormat, params object[] args)
        {
            Log(String.Format(entryFormat, args), LogLevel.Critical);
        }

        public static void Error(string entryFormat, params object[] args)
        {
            Log(String.Format(entryFormat, args), LogLevel.Error);
        }


    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }
}
