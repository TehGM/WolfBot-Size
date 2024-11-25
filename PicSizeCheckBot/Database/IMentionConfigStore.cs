using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TehGM.WolfBots.PicSizeCheckBot.Mentions;

namespace TehGM.WolfBots.PicSizeCheckBot.Database
{
    public interface IMentionConfigStore
    {
        Task<IEnumerable<MentionConfig>> GetAllAsync(CancellationToken cancellationToken = default);
    }
}
