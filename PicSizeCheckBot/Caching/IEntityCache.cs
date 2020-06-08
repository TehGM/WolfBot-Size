using System;
using System.Collections.Generic;

namespace TehGM.WolfBots.Caching
{
    public interface IEntityCache<TKey, TEntity>
    {
        int CachedCount { get; }

        void Clear();
        IEnumerable<TEntity> Find(Func<TEntity, bool> predicate);
        void AddOrReplace(TKey key, TEntity entity);
        TEntity GetByKey(TKey key);
    }
}
