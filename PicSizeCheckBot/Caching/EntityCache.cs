using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace TehGM.WolfBots.Caching
{
    public class EntityCache<TKey, TEntity> : IEntityCache<TKey, TEntity>, IEnumerable<TEntity>
    {
        private IDictionary<TKey, TEntity> _cachedEntities = new Dictionary<TKey, TEntity>();

        public int CachedCount => _cachedEntities.Count;

        public void AddOrReplace(TKey key, TEntity entity)
            => _cachedEntities[key] = entity;

        public void Clear()
            => _cachedEntities.Clear();

        public IEnumerable<TEntity> Find(Func<TEntity, bool> predicate)
            => _cachedEntities.Values.Where(predicate).ToImmutableArray();

        public TEntity GetByKey(TKey key)
        {
            if (_cachedEntities.TryGetValue(key, out TEntity result))
                return result;
            return default;
        }

        IEnumerator<TEntity> IEnumerable<TEntity>.GetEnumerator()
            => _cachedEntities.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => _cachedEntities.Values.GetEnumerator();
    }
}
