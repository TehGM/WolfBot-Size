using System;

namespace TehGM.WolfBots.PicSizeCheckBot.Options
{
    public class CachingOptions
    {
        public bool Enable { get; set; } = true;
        public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(30);
    }
}
