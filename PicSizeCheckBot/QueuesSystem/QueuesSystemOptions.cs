using System.Collections.Generic;

namespace TehGM.WolfBots.PicSizeCheckBot.QueuesSystem
{
    public class QueuesSystemOptions
    {
        public HashSet<string> ForbiddenQueueNames { get; set; }
        public string SubmissionBotShowCommand { get; set; } = "!submit guesswhat show {{id}}";
    }
}
