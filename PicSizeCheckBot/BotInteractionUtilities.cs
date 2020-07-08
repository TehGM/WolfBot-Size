using System.Text;
using TehGM.WolfBots.PicSizeCheckBot.NextGameUtility;
using TehGM.WolfBots.PicSizeCheckBot.Options;

namespace TehGM.WolfBots.PicSizeCheckBot
{
    public static class BotInteractionUtilities
    {
        public static string GetSubmissionBotShowCommand(BotOptions options, uint gameID)
        {
            StringBuilder builder = new StringBuilder(options.SubmissionBotShowCommand);
            builder.Replace("{{id}}", gameID.ToString());
            return builder.ToString();
        }

        public static string GetAutopostBotAddCommand<T>(NextGameOptions options, T value)
        {
            StringBuilder builder = new StringBuilder(options.AutoPostBotAddCommand);
            builder.Replace("{{value}}", value.ToString());
            return builder.ToString();
        }
    }
}
