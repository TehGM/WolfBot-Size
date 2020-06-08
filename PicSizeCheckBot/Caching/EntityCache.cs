using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace TehGM.WolfBots.Caching
{
    public class EntityCache<TKey, TEntity> : IEntityCache<TKey, TEntity>, IEnumerable<CachedEntity<TEntity>>, IEnumerable<TEntity>
    {
        private IDictionary<TKey, CachedEntity<TEntity>> _cachedEntities = new Dictionary<TKey, CachedEntity<TEntity>>();

        public int CachedCount => _cachedEntities.Count;

        public virtual void AddOrReplace(TKey key, TEntity entity)
            => _cachedEntities[key] = new CachedEntity<TEntity>(entity);

        public void Clear()
            => _cachedEntities.Clear();

        public IEnumerable<TEntity> Find(Func<CachedEntity<TEntity>, bool> predicate)
            => _cachedEntities.Where(pair => predicate(pair.Value)).Select(e => e.Value.Entity).ToImmutableArray();

        public TEntity GetByKey(TKey key)
        {
            if (_cachedEntities.TryGetValue(key, out CachedEntity<TEntity> result))
            {
                if (!IsEntityExpired(result))
                    return result.Entity;
                else _cachedEntities.Remove(key);
            }
            return default;
        }

        protected virtual bool IsEntityExpired(CachedEntity<TEntity> entity)
            => false;

        protected int ClearExpired()
        {
            KeyValuePair<TKey, CachedEntity<TEntity>>[] expired = _cachedEntities.Where(pair => IsEntityExpired(pair.Value)).ToArray();
            for (int i = 0; i < expired.Length; i++)
                _cachedEntities.Remove(expired[i].Key);
            return expired.Length;
        }

        IEnumerator<TEntity> IEnumerable<TEntity>.GetEnumerator()
            => _cachedEntities.Values.Select(e => e.Entity).GetEnumerator();

        IEnumerator<CachedEntity<TEntity>> IEnumerable<CachedEntity<TEntity>>.GetEnumerator()
            => _cachedEntities.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => _cachedEntities.Values.GetEnumerator();
    }
}
