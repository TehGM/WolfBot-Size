using System.Collections.Generic;

namespace TehGM.WolfBots.PicSizeCheckBot.QueuesSystem
{
    public class QueuesSystemOptions
    {
        public HashSet<string> ForbiddenQueueNames { get; set; }
        public char[] IdSplitCharacters { get; set; } = new char[] { ' ', ',', ':', '.', '\'', ';', '\n', '-', '_', '?', '!', '*' };
    }
}
