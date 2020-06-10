using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace TehGM.WolfBots.PicSizeCheckBot.Caching.Services
{
    public class EntityCache<TKey, TEntity> : IEntityCache<TKey, TEntity> where TEntity : IEntity<TKey>
    {
        private readonly IDictionary<TKey, CachedEntity<TEntity>> _cachedEntities;

        public int CachedCount => _cachedEntities.Count;

        public EntityCache(IEqualityComparer<TKey> comparer)
        {
            this._cachedEntities = new Dictionary<TKey, CachedEntity<TEntity>>(comparer);
        }

        public EntityCache() : this(EqualityComparer<TKey>.Default) { }

        public virtual void AddOrReplace(TEntity entity)
        {
            lock (_cachedEntities)
                _cachedEntities[entity.ID] = new CachedEntity<TEntity>(entity);
        }

        public void Clear()
        {
            lock (_cachedEntities)
                _cachedEntities.Clear();
        }

        public IEnumerable<TEntity> Find(Func<CachedEntity<TEntity>, bool> predicate)
        {
            lock (_cachedEntities)
                return _cachedEntities.Where(pair => predicate(pair.Value)).Select(e => e.Value.Entity).ToImmutableArray();
        }

        public virtual TEntity Get(TKey key)
        {
            lock (_cachedEntities)
            {
                if (_cachedEntities.TryGetValue(key, out CachedEntity<TEntity> result))
                {
                    if (!IsEntityExpired(result))
                        return result.Entity;
                    else _cachedEntities.Remove(key);
                }
                return default;
            }
        }

        protected virtual bool IsEntityExpired(CachedEntity<TEntity> entity)
            => false;

        public void Remove(TKey key)
        {
            lock (_cachedEntities)
                _cachedEntities.Remove(key);
        }
    }
}
