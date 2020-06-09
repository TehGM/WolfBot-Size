namespace TehGM.WolfBots.PicSizeCheckBot.NextGameUtility
{
    public class NextGameOptions
    {
        public uint AutoPostBotID { get; set; } = 15145815;
        public string AutoPostBotPostCommand { get; set; } = "!ap post 1";
        public string AutoPostBotRemoveCommand { get; set; } = "!ap del 1";
        public string AutoPostBotAddCommand { get; set; } = "!ap add {{value}}";

        public int AutoPostBotWaitSeconds { get; set; } = 5;
    }
}
