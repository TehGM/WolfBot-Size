using System.Collections.Generic;

namespace TehGM.WolfBots.PicSizeCheckBot.Mentions
{
    public class MentionsOptions
    {
        public string DefaultMessageTemplate { get; set; } = "User {{UserName}} mentioned you in group [{{GroupName}}]: {{Message}}";

        public HashSet<uint> IgnoredUsers { get; set; }
        public HashSet<uint> IgnoredGroups { get; set; }

        public int MaxTextLength { get; set; } = 500;
    }
}
