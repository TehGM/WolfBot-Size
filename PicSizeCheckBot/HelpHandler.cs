using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TehGM.Wolfringo;
using TehGM.Wolfringo.Commands;

namespace TehGM.WolfBots.PicSizeCheckBot
{
    [CommandsHandler]
    public class HelpHandler
    {
        [Command("help")]
        [Priority(-99999)]
        private Task CmdHelpAsync(CommandContext context, CancellationToken cancellationToken = default)
            => context.ReplyTextAsync(
@$"I will post size of images posted in this group. 
I can also store your notes and ID queues.
Last but not least, I can make pulling games one-by-one from Submission bot a lot easier!

Bot features and commands: https://github.com/TehGM/WolfBot-Size/wiki

Questions, suggestions or bugs reports: https://github.com/TehGM/WolfBot-Size/issues.
Sponsor my work: https://github.com/sponsors/TehGM

Using Wolfringo library v1.0.0-beta5
Bot version: v{GetVersion()}
Copyright © 2020 TehGM",  // due to AGPL licensing, this line cannot be changed or removed, unless by the original author
cancellationToken);

        private static string GetVersion()
        {
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(typeof(HelpHandler).Assembly.Location);
            if (!string.IsNullOrWhiteSpace(versionInfo.ProductVersion))
                return versionInfo.ProductVersion;
            string result = $"{versionInfo.FileMajorPart}.{versionInfo.FileMinorPart}.{versionInfo.FileBuildPart}";
            if (versionInfo.FilePrivatePart != 0)
                result += $".{versionInfo.FilePrivatePart}";
            return result;
        }
    }
}
