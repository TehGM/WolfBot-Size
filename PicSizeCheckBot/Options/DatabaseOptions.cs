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
    }
}
