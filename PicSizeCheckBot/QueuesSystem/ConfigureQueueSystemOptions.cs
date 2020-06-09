using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TehGM.WolfBots.PicSizeCheckBot.QueuesSystem
{
    public class ConfigureQueueSystemOptions : IPostConfigureOptions<QueuesSystemOptions>
    {
        public void PostConfigure(string name, QueuesSystemOptions options)
        {
            if (options.ForbiddenQueueNames?.Any() == true)
                return;

            string[] forbiddenNames = new string[] { "cache", "posturl", "enable", "disable", "next", "help", "profile", "listen", "post url", "continue", "update", "max size", "join", "leave", "mention", "admin" };
            options.ForbiddenQueueNames = new HashSet<string>(forbiddenNames, StringComparer.OrdinalIgnoreCase);
        }
    }
}
