using Newtonsoft.Json;
using System.IO;

namespace TehGM.WolfBots.PicSizeCheckBot.EncodingMigration
{
    public class Settings
    {
        [JsonProperty("ConnectionString")]
        public string ConnectionString { get; private set; }
        [JsonProperty("DatabaseName")]
        public string DatabaseName { get; private set; }

        public static Settings Load(string filename)
            => JsonConvert.DeserializeObject<Settings>(File.ReadAllText(filename));
    }
}
