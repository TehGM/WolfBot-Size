using TehGM.WolfBots.Caching;

namespace TehGM.WolfBots.PicSizeCheckBot.Caching
{
    public interface IUserDataCache : IEntityCache<uint, UserData> { }
    public class UserDataCache : EntityCache<uint, UserData>, IUserDataCache { }
}
