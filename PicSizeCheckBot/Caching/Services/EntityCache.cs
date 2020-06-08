using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace TehGM.WolfBots.PicSizeCheckBot.Caching.Services
{
    public class EntityCache<TKey, TEntity> : IEntityCache<TKey, TEntity> where TEntity : IEntity<TKey>
    {
        private IDictionary<TKey, CachedEntity<TEntity>> _cachedEntities = new Dictionary<TKey, CachedEntity<TEntity>>();

        public int CachedCount => _cachedEntities.Count;

        public virtual void AddOrReplace(TEntity entity)
            => _cachedEntities[entity.ID] = new CachedEntity<TEntity>(entity);

        public void Clear()
            => _cachedEntities.Clear();

        public IEnumerable<TEntity> Find(Func<CachedEntity<TEntity>, bool> predicate)
            => _cachedEntities.Where(pair => predicate(pair.Value)).Select(e => e.Value.Entity).ToImmutableArray();

        public virtual TEntity Get(TKey key)
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

        public void Remove(TKey key)
            => _cachedEntities.Remove(key);
    }
}
