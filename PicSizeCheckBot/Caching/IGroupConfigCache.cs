using Microsoft.Extensions.Options;
using System;
using TehGM.WolfBots.PicSizeCheckBot.Options;

namespace TehGM.WolfBots.PicSizeCheckBot.Caching
{
    public interface IGroupConfigCache : IEntityCache<uint, GroupConfig> { }
}
