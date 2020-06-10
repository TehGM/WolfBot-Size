using System;

namespace TehGM.WolfBots.PicSizeCheckBot.Options
{
    public class DatabaseOptions
    {
        public string ConnectionString { get; set; }

        // databases
        public string DatabaseName { get; set; } = "PicSizeBot";

        // collections
        public string UsersDataCollectionName { get; set; } = "UsersData";
        public string GroupConfigsCollectionName { get; set; } = "GroupConfigs";
        public string IdQueuesCollectionName { get; set; } = "IdQueues";
        public string UserNotesCollectionName { get; set; } = "UserNotes";

        // inserter batch delays
        public TimeSpan IdQueueBatchDelay { get; set; } = TimeSpan.FromMinutes(10);
        public TimeSpan GroupConfigsBatchDelay { get; set; } = TimeSpan.FromMinutes(10);
        public TimeSpan UserDataBatchDelay { get; set; } = TimeSpan.FromMinutes(5);
    }
}
