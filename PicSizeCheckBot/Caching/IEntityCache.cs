using System;
using System.Collections.Generic;

namespace TehGM.WolfBots.PicSizeCheckBot.Caching
{
    public interface IEntityCache<TKey, TEntity> where TEntity : IEntity<TKey>
    {
        int CachedCount { get; }

        void Clear();
        IEnumerable<TEntity> Find(Func<CachedEntity<TEntity>, bool> predicate, bool excludeExpired = true);
        void AddOrReplace(TEntity entity);
        void Remove(TKey key);
        TEntity Get(TKey key);
    }
}
