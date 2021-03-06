﻿using MongoDB.Bson.Serialization.Attributes;

namespace TehGM.WolfBots.PicSizeCheckBot
{
    /// <summary>Represents group configuration.</summary>
    public class GroupConfig : ITargetConfig, IEntity<uint>
    {
        /// <summary>ID of the group<./summary>
        [BsonId]
        [BsonElement("_id")]
        public uint ID { get; }

        // listen modes
        /// <summary>Whether bot should check sizes of images posted by admins.</summary>
        public bool ListenAdmins { get; set; } = true;
        /// <summary>Whether bot should check sizes of images posted by mods.</summary>
        public bool ListenMods { get; set; } = true;
        /// <summary>Whether bot should check sizes of images posted by users without privileges.</summary>
        public bool ListenUsers { get; set; } = true;
        /// <summary>Whether bot should check sizes of images posted by official bots.</summary>
        public bool ListenBots { get; set; } = true;

        // settings
        /// <summary>Whether bot should post image URL for size checks when used in PM.</summary>
        public bool PostImageURL { get; set; } = true;
        /// <summary>Whether automatic size checks are enabled in this group at all.</summary>
        public bool IsEnabled { get; set; } = true;

        // data
        /// <summary>ID of current guesswhat game in this group.</summary>
        [BsonElement("NextGuesswhatGameID")]
        public uint? CurrentGuesswhatGameID { get; set; }

        [BsonConstructor(nameof(ID))]
        public GroupConfig(uint groupID)
        {
            this.ID = groupID;
        }
    }
}
