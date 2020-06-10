using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using System;
using System.Threading.Tasks;
using TehGM.WolfBots.Options;
using TehGM.WolfBots.PicSizeCheckBot.Caching;
using TehGM.WolfBots.PicSizeCheckBot.Caching.Services;
using TehGM.WolfBots.PicSizeCheckBot.NextGameUtility;
using TehGM.WolfBots.PicSizeCheckBot.Options;
using TehGM.WolfBots.PicSizeCheckBot.QueuesSystem;
using TehGM.WolfBots.PicSizeCheckBot.SizeChecking;
using TehGM.WolfBots.PicSizeCheckBot.UserNotes;
using TehGM.Wolfringo.Hosting;

namespace TehGM.WolfBots.PicSizeCheckBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            EnableUnhandledExceptionLogging();

            IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsecrets.json", optional: true);
                    config.AddJsonFile($"appsecrets.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
                })
                .ConfigureServices((context, services) =>
                {
                    // configure options
                    services.Configure<BotOptions>(context.Configuration);
                    services.Configure<HostedWolfClientOptions>(context.Configuration.GetSection("WolfClient"));
                    services.Configure<SizeCheckingOptions>(context.Configuration.GetSection("PictureSize"));
                    services.Configure<QueuesSystemOptions>(context.Configuration.GetSection("QueuesSystem"));
                    services.Configure<NextGameOptions>(context.Configuration.GetSection("NextGame"));
                    services.Configure<UserNotesOptions>(context.Configuration.GetSection("UserNotes"));
                    services.Configure<DatabaseOptions>(context.Configuration.GetSection("Database"));
                    services.Configure<CachingOptions>(UserDataCache.OptionName, context.Configuration.GetSection("Caching:" + UserDataCache.OptionName));
                    services.Configure<CachingOptions>(GroupConfigCache.OptionName, context.Configuration.GetSection("Caching:" + GroupConfigCache.OptionName));
                    services.Configure<CachingOptions>(IdQueueCache.OptionName, context.Configuration.GetSection("Caching:" + IdQueueCache.OptionName));

                    // add framework services
                    services.AddHttpClient();

                    // add hosted wolf client
                    services.AddWolfClient();

                    // add caching
                    services.AddEntityCaching();

                    // add handlers
                    services.AddSizeChecking();
                    services.AddAdminUtilities();
                    services.AddQueuesSystem();
                    services.AddNextGameUtility();
                    services.AddUserNotes();
                })
                .UseSerilog((context, config) => ConfigureSerilog(context, config), true)
                .Build();
            await host.RunAsync().ConfigureAwait(false);
        }

        private static void ConfigureSerilog(HostBuilderContext context, LoggerConfiguration config)
        {
            DatadogOptions ddOptions = context.Configuration.GetSection("Serilog").GetSection("DataDog").Get<DatadogOptions>();
            config.ReadFrom.Configuration(context.Configuration)
                .WriteTo.DatadogLogs(
                    ddOptions.ApiKey,
                    source: ".NET",
                    service: ddOptions.ServiceName ?? "WolfBots-Size",
                    host: ddOptions.HostName ?? Environment.MachineName,
                    new string[] {
                                $"env:{(ddOptions.EnvironmentName ?? context.HostingEnvironment.EnvironmentName)}",
                                $"assembly:{(ddOptions.AssemblyName ?? context.HostingEnvironment.ApplicationName)}"
                    },
                    ddOptions.ToDatadogConfiguration(),
                    // no need for debug logs in datadag
                    logLevel: ddOptions.OverrideLogLevel ?? LogEventLevel.Verbose
                );
        }

        private static void EnableUnhandledExceptionLogging()
        {
            // add default logger for errors that happen before host runs
            Log.Logger = new LoggerConfiguration()
                        .WriteTo.Console()
                        .WriteTo.File("data/logs/tepn-unhandled.log",
                        fileSizeLimitBytes: 1048576,        // 1MB
                        rollOnFileSizeLimit: true,
                        retainedFileCountLimit: 5,
                        rollingInterval: RollingInterval.Day)
                        .CreateLogger();
            // capture unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
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
    }
}
