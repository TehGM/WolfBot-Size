namespace TehGM.WolfBots.PicSizeCheckBot.Caching
{
    public interface ISingleEntityCache<TEntity>
    {
        void Clear();
        void Replace(TEntity entity);
        TEntity Get();
    }
}
