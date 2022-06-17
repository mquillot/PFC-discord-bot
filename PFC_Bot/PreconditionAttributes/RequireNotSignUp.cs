using System;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace PFC_Bot.PreconditionAttributes
{
    // Inherit from PreconditionAttribute
    public class RequireNotSignUp : PreconditionAttribute
    {

        // Override the CheckPermissions method
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            ApplicationDbContext database = (ApplicationDbContext)services.GetService(typeof(ApplicationDbContext));

            UserEntity user = database.Users.SingleOrDefault(e => e.Id_Discord == context.User.Id);

            if (user == null)
                return PreconditionResult.FromSuccess();
            else
                return PreconditionResult.FromError($"Tu es déjà inscrit !");

        }
    }
}
