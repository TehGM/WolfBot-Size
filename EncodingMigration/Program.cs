using Serilog;
using System;
using System.Text;

namespace TehGM.WolfBots.PicSizeCheckBot.EncodingMigration
{
    class Program
    {
        static void Main(string[] args)
        {
            ILogger log = StartLogging();

            if (args.Length == 0)
            {
                log.Error("Specify environment. Allowed environments: dev, prod");
                Console.ReadLine();
                return;
            }

            string filename;
            switch (args[0].ToLower())
            {
                case "dev":
                    {
                        Console.Write("Are you sure you want to process on DEVELOPMENT environment? (Y): ");
                        if (!Console.ReadLine().Equals("Y", StringComparison.OrdinalIgnoreCase))
                        {
                            log.Information("Operation aborted");
                            Console.ReadLine();
                            return;
                        }
                        log.Information("Using development environment");
                        filename = "appsecrets.Development.json";
                    }
                    break;
                case "prod":
                    {
                        Console.Write("Are you sure you want to process on PRODUCTION environment? (Y): ");
                        if (!Console.ReadLine().Equals("Y", StringComparison.OrdinalIgnoreCase))
                        {
                            log.Information("Operation aborted.");
                            Console.ReadLine();
                            return;
                        }
                        log.Information("Using production environment");
                        filename = "appsecrets.json";
                    }
                    break;
                default:
                    {
                        log.Error("Specify environment. Allowed environments: dev, prod");
                        Console.ReadLine();
                        return;
                    }
            }

            log.Debug("Loading {FileName}", filename);
            Settings settings = Settings.Load(filename);
            log.Debug("Settings file loaded");
        }

        private static ILogger StartLogging()
        {
            Log.Logger = new LoggerConfiguration()
                        .WriteTo.Console()
                        .WriteTo.File("logs/log.txt",
                        fileSizeLimitBytes: 5242880,        // 5MB
                        rollOnFileSizeLimit: true,
                        retainedFileCountLimit: 5,
                        rollingInterval: RollingInterval.Day)
                        .MinimumLevel.Debug()
                        .CreateLogger();
            // capture unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            return Log.Logger;
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Log.Logger.Error((Exception)e.ExceptionObject, "An exception was unhandled");
                Log.CloseAndFlush();
            }
            catch { }
        }

        private static Encoding Win1252 = Encoding.GetEncoding("windows-1252");
        private static string ToUtf8(string valueWindows1252)
        {
            byte[] bytes = Win1252.GetBytes(valueWindows1252);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
