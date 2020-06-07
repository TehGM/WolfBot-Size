namespace TehGM.WolfBots.PicSizeCheckBot.Options
{
    public class BotOptions
    {
        public string CommandPrefix { get; set; } = "!size ";
        public bool RequirePrefixInPrivate { get; set; } = false;

        public uint OwnerID { get; set; }
    }
}
