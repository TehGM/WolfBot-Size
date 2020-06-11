//#define TEST_ONLY

using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Database.Conventions;

namespace TehGM.WolfBots.PicSizeCheckBot.EncodingMigration
{
    class Program
    {
        static async Task Main(string[] args)
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
                        log.Error("Invalid environment. Allowed environments: dev, prod");
                        Console.ReadLine();
                        return;
                    }
            }

            log.Debug("Loading {FileName}", filename);
            Settings settings = Settings.Load(filename);
            log.Debug("Settings file loaded");

            log.Information("Establishing database connection, DB {DatabaseName}", settings.DatabaseName);
            MongoClient client = new MongoClient(settings.ConnectionString);
            ConventionPack conventionPack = new ConventionPack();
            conventionPack.Add(new MapReadOnlyPropertiesConvention());
            conventionPack.Add(new GuidAsStringRepresentationConvention());
            ConventionRegistry.Register("Conventions", conventionPack, _ => true);
            IMongoDatabase db = client.GetDatabase(settings.DatabaseName);

            await MigrateEntitiesAsync<IdQueue>(db, "IdQueues", log, PerformQueueMigration);
            await MigrateEntitiesAsync<MentionConfig>(db, "Mentions", log, PerformMentionMigration);
            await MigrateEntitiesAsync<UserData>(db, "UsersData", log, PerformUserDataMigration);

            log.Information("Done");
            Console.ReadLine();
        }

        delegate bool MigrationDelegate<T>(T entity, ILogger log, out T newEntity, out Expression<Func<T, bool>> selector);

        private static async Task MigrateEntitiesAsync<T>(IMongoDatabase db, string collectionName, ILogger log,
            MigrationDelegate<T> migrationMethod)
        {
            Console.WriteLine();
            Console.WriteLine();
            log.Information("Starting {EntityType} migration", typeof(T).Name);
            log.Debug("Opening collection {CollectionName}", collectionName);
            IMongoCollection<T> collection = db.GetCollection<T>(collectionName);
            log.Debug("Requesting all entities from the collection");
            IEnumerable<T> allEntities = await collection.Find(_ => true).ToListAsync();
            log.Debug("{EntityCount} entities found in {CollectionName} collection", allEntities.Count(), collectionName);

            ReplaceOptions options = new ReplaceOptions()
            {
                BypassDocumentValidation = false,
                IsUpsert = false
            };
            foreach (T entity in allEntities)
            {
                if (migrationMethod(entity, log, out T newEntity, out Expression<Func<T, bool>> selector))
                {
                    #if !TEST_ONLY
                    await collection.ReplaceOneAsync(selector, newEntity, options);
                    #endif
                }
            }
        }

        private static bool PerformQueueMigration(IdQueue entity, ILogger log, out IdQueue newEntity, out Expression<Func<IdQueue, bool>> selector)
        {
            selector = dbEntity => dbEntity.ID == entity.ID;

            newEntity = entity;
            string newName = ToUtf8(entity.Name);
            if (newName.Equals(entity.Name))
            {
                log.Debug("Skipping: {OldEntityValue}", entity.Name);
                return false;
            }

            log.Debug("Updating: {OldEntityValue} -> {NewEntityValue}", entity.Name, newName);
            newEntity.Name = newName;
            return true;
        }

        private static bool PerformMentionMigration(MentionConfig entity, ILogger log, out MentionConfig newEntity,
            out Expression<Func<MentionConfig, bool>> selector)
        {
            selector = dbEntity => dbEntity.ID == entity.ID;

            newEntity = entity;

            List<MentionPattern> newPatterns = new List<MentionPattern>(entity.Patterns.Count);
            int updatedCount = 0;
            foreach (MentionPattern pattern in entity.Patterns)
            {
                string oldPatternText = (string)pattern.GetType().GetField("_pattern", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pattern);
                string newPatternText = ToUtf8(oldPatternText);
                newPatterns.Add(new MentionPattern(newPatternText, pattern.IgnoreCase));
                if (!newPatternText.Equals(oldPatternText))
                {
                    log.Debug("Updating {ID}: {OldEntityValue} -> {NewEntityValue}", entity.ID, oldPatternText, newPatternText);
                    updatedCount++;
                }
            }

            if (updatedCount == 0)
            {
                log.Debug("Skipping: {OldEntityValue}", entity.ID);
                return false;
            }

            newEntity.Patterns.Clear();
            foreach (MentionPattern pattern in newPatterns)
                newEntity.Patterns.Add(pattern);

            return true;
        }

        private static bool PerformUserDataMigration(UserData entity, ILogger log, out UserData newEntity,
            out Expression<Func<UserData, bool>> selector)
        {
            selector = dbEntity => dbEntity.ID == entity.ID;

            newEntity = entity;

            Dictionary<uint, string> newNotes = new Dictionary<uint, string>(entity.Notes.Count);
            int updatedCount = 0;
            foreach (KeyValuePair<uint, string> note in entity.Notes)
            {
                string newNoteText = ToUtf8(note.Value);
                newNotes.Add(note.Key, newNoteText);
                if (!newNoteText.Equals(note.Value))
                {
                    log.Debug("Updating {ID}: {OldEntityValue} -> {NewEntityValue}", entity.ID, note.Value, newNoteText);
                    updatedCount++;
                }
            }

            if (updatedCount == 0)
            {
                log.Debug("Skipping: {OldEntityValue}", entity.ID);
                return false;
            }

            newEntity.Notes.Clear();
            foreach (KeyValuePair<uint, string> note in newNotes)
                newEntity.Notes.Add(note.Key, note.Value);

            return true;
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

        private static Encoding Win1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252);
        private static string ToUtf8(string valueWindows1252)
        {
            byte[] bytes = Win1252.GetBytes(valueWindows1252);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
