﻿namespace TehGM.WolfBots.PicSizeCheckBot
{
    /// <summary>Represents config and working data of the user.</summary>
    public class UserData
    {
        /// <summary>ID of the user<./summary>
        public uint UserID { get; private set; }

        // permissions
        /// <summary>Whether user should have access to admin-only commands.</summary>
        public uint IsBotAdmin { get; set; }

        // settings
        /// <summary>Whether bot should post image URL for size checks when used in PM.</summary>
        public bool PostImageURL { get; set; }

        public UserData(uint userID)
        {
            this.UserID = userID;
        }
    }
}
