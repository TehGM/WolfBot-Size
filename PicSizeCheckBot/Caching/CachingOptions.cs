using System;

namespace TehGM.WolfBots.PicSizeCheckBot.Caching
{
    public class CachingOptions
    {
        public bool Enable { get; set; } = true;
        public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(30);
    }
}
