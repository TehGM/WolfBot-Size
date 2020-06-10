using System;

namespace TehGM.WolfBots.PicSizeCheckBot.Caching.Services
{
    public class SingleEntityCache<TEntity> : ISingleEntityCache<TEntity>
    {
        private CachedEntity<TEntity> _cachedEntity;

        public void Clear()
        { 
            lock (_cachedEntity)
                _cachedEntity = null;
        }

        public TEntity Get()
        {
            lock (_cachedEntity)
            {
                if (_cachedEntity == null)
                    return default;
                return _cachedEntity.Entity;
            }
        }

        protected virtual bool IsEntityExpired(CachedEntity<TEntity> entity)
            => false;

        public void Replace(TEntity entity)
        {
            lock (_cachedEntity)
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));
                this._cachedEntity = new CachedEntity<TEntity>(entity);
            }
        }
    }
}
