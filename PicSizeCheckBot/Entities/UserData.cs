﻿using MongoDB.Bson.Serialization.Attributes;

namespace TehGM.WolfBots.PicSizeCheckBot
{
    /// <summary>Represents config and working data of the user.</summary>
    public class UserData : ITargetConfig
    {
        /// <summary>ID of the user<./summary>
        [BsonId]
        [BsonElement("_id")]
        public uint UserID { get; }

        // permissions
        /// <summary>Whether user should have access to admin-only commands.</summary>
        public bool IsBotAdmin { get; set; } = false;

        // settings
        /// <summary>Whether bot should post image URL for size checks when used in PM.</summary>
        public bool PostImageURL { get; set; } = true;

        [BsonConstructor(nameof(UserID))]
        public UserData(uint userID)
        {
            this.UserID = userID;
        }
    }
}
