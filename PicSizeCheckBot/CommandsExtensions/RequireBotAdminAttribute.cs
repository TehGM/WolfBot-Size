using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TehGM.WolfBots.PicSizeCheckBot;
using TehGM.WolfBots.PicSizeCheckBot.Database;
using TehGM.Wolfringo.Commands.Attributes;

namespace TehGM.Wolfringo.Commands
{
    public class RequireBotAdminAttribute : CommandRequirementAttribute
    {
        public RequireBotAdminAttribute() : base()
        {
            ErrorMessage = "(n) You are not permitted to do this!";
        }

        public override async Task<ICommandResult> CheckAsync(ICommandContext context, IServiceProvider services, CancellationToken cancellationToken = default)
        {
            IUserDataStore userDataStore = services.GetRequiredService<IUserDataStore>();
            // check if user is bot admin
            UserData userData = await userDataStore.GetUserDataAsync(context.Message.SenderID.Value, cancellationToken).ConfigureAwait(false);
            return base.ResultFromBoolean(userData.IsBotAdmin);
        }
    }
}
