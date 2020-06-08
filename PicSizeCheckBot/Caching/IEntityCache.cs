using System;
using System.Collections.Generic;

namespace TehGM.WolfBots.PicSizeCheckBot.Caching
{
    public interface IEntityCache<TKey, TEntity> where TEntity : IEntity<TKey>
    {
        int CachedCount { get; }

        void Clear();
        IEnumerable<TEntity> Find(Func<CachedEntity<TEntity>, bool> predicate);
        void AddOrReplace(TEntity entity);
        TEntity GetByKey(TKey key);
    }
}
