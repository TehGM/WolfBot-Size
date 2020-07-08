namespace TehGM.WolfBots.PicSizeCheckBot.Options
{
    public class BotOptions
    {
        public string CommandPrefix { get; set; } = "!size ";
        public bool RequirePrefixInPrivate { get; set; } = false;

        public uint OwnerID { get; set; } = 2644384;
        public uint TestGroupID { get; set; } = 2790082;

        public string SubmissionBotShowCommand { get; set; } = "!submit guesswhat show {{id}}";
    }
}
