using System;

namespace TehGM.WolfBots.Caching
{
    public class CachedEntity<TEntity>
    {
        public TEntity Entity { get; }
        public DateTime CachingTimeUtc { get; }

        public CachedEntity(TEntity entity)
        {
            this.Entity = entity;
            this.CachingTimeUtc = DateTime.UtcNow;
        }

        public bool IsExpired(TimeSpan lifetime)
        {
            if (lifetime < TimeSpan.Zero)
                return false;
            return DateTime.UtcNow < CachingTimeUtc + lifetime;
        }
    }
}
