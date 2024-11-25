using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;
using TehGM.WolfBots.PicSizeCheckBot.Mentions.Filters;

namespace TehGM.WolfBots.PicSizeCheckBot.Mentions
{
    public class MentionConfig : IEntity<uint>
    {
        /// <summary>ID of the user<./summary>
        [BsonId]
        public uint ID { get; }

        [BsonElement("MessageTemplate"), BsonIgnoreIfNull]
        public string MessageTemplate { get; init; }

        [BsonElement("Patterns")]
        public ICollection<MentionPattern> Patterns { get; set; }
        [BsonElement("GlobalFilters")]
        public ICollection<IMentionFilter> GlobalFilters { get; set; }

        [BsonConstructor(nameof(ID))]
        public MentionConfig(uint userID)
        {
            this.ID = userID;
            this.Patterns ??= new List<MentionPattern>();
        }
    }
}
