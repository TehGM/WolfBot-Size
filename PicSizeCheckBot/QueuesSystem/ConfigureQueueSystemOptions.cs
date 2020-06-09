using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;

namespace TehGM.WolfBots.PicSizeCheckBot.QueuesSystem
{
    public class ConfigureQueueSystemOptions : IConfigureOptions<QueuesSystemOptions>
    {
        private readonly IConfiguration _configuration;

        public ConfigureQueueSystemOptions(IConfiguration configuration)
        {
            this._configuration = configuration;
        }

        public void Configure(QueuesSystemOptions options)
        {
            IEnumerable<string> forbiddenNames = _configuration.GetSection("ForbiddenQueueNames")?.Get<IEnumerable<string>>();
            if (forbiddenNames == null)
                forbiddenNames = new string[] { "cache", "posturl", "enable", "disable", "next", "help", "profile", "listen", "post url", "continue", "update", "max size", "join", "leave", "mention", "admin" };
            options.ForbiddenQueueNames = new HashSet<string>(forbiddenNames, StringComparer.OrdinalIgnoreCase);
        }
    }
}
