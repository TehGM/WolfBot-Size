using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TehGM.WolfBots.PicSizeCheckBot.Mentions
{
    public class MentionConfig : IEntity<uint>
    {
        /// <summary>ID of the user<./summary>
        [BsonId]
        public uint ID { get; }

        public string MessageTemplate { get; set; }
        public bool IgnoreSelf { get; set; } = true;

        public ICollection<MentionPattern> Patterns { get; set; }

        [BsonConstructor(nameof(ID))]
        public MentionConfig(uint userID)
        {
            this.ID = userID;
            this.Patterns ??= new List<MentionPattern>();
        }
    }
}
