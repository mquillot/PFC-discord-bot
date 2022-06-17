using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace PFC_Bot.PreconditionAttributes
{
    // Inherit from PreconditionAttribute
    public class RequireSignUp : PreconditionAttribute
    {
        // Override the CheckPermissions method
        public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
        {

            ApplicationDbContext database = (ApplicationDbContext)services.GetService(typeof(ApplicationDbContext));
            UserEntity user = database.Users.SingleOrDefault(e => e.Id_Discord == context.User.Id);


            if (user == null)
                return PreconditionResult.FromError($"Tu dois d'abord t'inscrire !");
            else
                return PreconditionResult.FromSuccess();
        }
    }
}
