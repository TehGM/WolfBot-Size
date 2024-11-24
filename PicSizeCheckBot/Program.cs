/*
    WOLF Pic Size bot, a WOLF bot to check sizes of posted pictures
    Copyright (C) 2020, TehGM

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You can view license copy at https://github.com/TehGM/WolfBot-Size/blob/master/LICENSE.
    If not, see https://www.gnu.org/licenses/.
*/

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
using TehGM.WolfBots.PicSizeCheckBot.Mentions;
using TehGM.WolfBots.PicSizeCheckBot.NextGameUtility;
using TehGM.WolfBots.PicSizeCheckBot.Options;
using TehGM.WolfBots.PicSizeCheckBot.QueuesSystem;
using TehGM.WolfBots.PicSizeCheckBot.SizeChecking;
using TehGM.WolfBots.PicSizeCheckBot.UserNotes;
using TehGM.Wolfringo.Commands;
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
                    config.AddJsonFile("guesswhat-ids.json", optional: true);
                    config.AddJsonFile($"guesswhat-ids.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
                })
                .ConfigureServices((context, services) =>
                {
                    // configure options
                    services.Configure<BotOptions>(context.Configuration);
                    services.Configure<CommandsOptions>(context.Configuration.GetSection("Commands"));
                    services.Configure<HostedWolfClientOptions>(context.Configuration.GetSection("WolfClient"));
                    services.Configure<SizeCheckingOptions>(context.Configuration.GetSection("PictureSize"));
                    services.Configure<QueuesSystemOptions>(context.Configuration.GetSection("QueuesSystem"));
                    services.Configure<NextGameOptions>(context.Configuration.GetSection("NextGame"));
                    services.Configure<UserNotesOptions>(context.Configuration.GetSection("UserNotes"));
                    services.Configure<MentionsOptions>(context.Configuration.GetSection("Mentions"));
                    services.Configure<DatabaseOptions>(context.Configuration.GetSection("Database"));
                    services.Configure<CachingOptions>(UserDataCache.OptionName, context.Configuration.GetSection("Caching:" + UserDataCache.OptionName));
                    services.Configure<CachingOptions>(GroupConfigCache.OptionName, context.Configuration.GetSection("Caching:" + GroupConfigCache.OptionName));
                    services.Configure<CachingOptions>(IdQueueCache.OptionName, context.Configuration.GetSection("Caching:" + IdQueueCache.OptionName));
                    services.Configure<CachingOptions>(MentionConfigCache.OptionName, context.Configuration.GetSection("Caching:" + MentionConfigCache.OptionName));

                    // add framework services
                    services.AddHttpClient();

                    // add hosted wolf client
                    services.AddWolfClient();
                    services.AddWolfringoCommands();

                    // add caching
                    services.AddEntityCaching();

                    // add features
                    services.AddSizeChecking();
                    services.AddAdminUtilities();
                    services.AddQueuesSystem();
                    services.AddNextGameUtility();
                    services.AddUserNotes();
                    services.AddMentions();
                })
                .UseSerilog((context, config) => ConfigureSerilog(context, config), true)
                .Build();
            await host.RunAsync().ConfigureAwait(false);
        }

        private static void ConfigureSerilog(HostBuilderContext context, LoggerConfiguration config)
        {
            config.ReadFrom.Configuration(context.Configuration);
            DatadogOptions ddOptions = context.Configuration.GetSection("Serilog")?.GetSection("DataDog")?.Get<DatadogOptions>();
            if (ddOptions != null)
            {
                config.WriteTo.DatadogLogs(
                    ddOptions.ApiKey,
                    source: ".NET",
                    service: ddOptions.ServiceName ?? "WolfBots-Size",
                    host: ddOptions.HostName ?? Environment.MachineName,
                    [
                                $"env:{(ddOptions.EnvironmentName ?? context.HostingEnvironment.EnvironmentName)}",
                                $"assembly:{(ddOptions.AssemblyName ?? context.HostingEnvironment.ApplicationName)}"
                    ],
                    ddOptions.ToDatadogConfiguration(),
                    // no need for debug logs in datadag
                    restrictedToMinimumLevel: ddOptions.OverrideLogLevel ?? LogEventLevel.Verbose
                );
            }
        }

        private static void EnableUnhandledExceptionLogging()
        {
            // add default logger for errors that happen before host runs
            Log.Logger = new LoggerConfiguration()
                        .WriteTo.Console()
                        .WriteTo.File("logs/log-unhandled.txt",
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
