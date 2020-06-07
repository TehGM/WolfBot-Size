using MongoDB.Bson.Serialization.Attributes;

namespace TehGM.WolfBots.PicSizeCheckBot
{
    /// <summary>Represents group configuration.</summary>
    public class GroupConfig : ITargetConfig
    {
        /// <summary>ID of the group<./summary>
        [BsonId]
        [BsonElement("_id")]
        public uint GroupID { get; }

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

        [BsonConstructor(nameof(GroupID))]
        public GroupConfig(uint groupID)
        {
            this.GroupID = groupID;
        }
    }
}
