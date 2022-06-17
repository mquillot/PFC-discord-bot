using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PFC_Bot.PreconditionAttributes
{
    public class CheckRole : PreconditionAttribute
    {
        private List<string> _roles;

        public CheckRole(params string[] roles)
        {
            _roles = roles.ToList();
        }

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo commandInfo, IServiceProvider service)
        {

            Console.WriteLine("On entre par là !");
            var user = context.User as IGuildUser;
            var discordRoles = context.Guild.Roles.Where(gr => _roles.Any(r => gr.Name == r));

            foreach (var role in discordRoles)
            {
                var userInRole = user.RoleIds.Any(ri => ri == role.Id);

                if (userInRole)
                {
                    return Task.FromResult(PreconditionResult.FromSuccess());
                }
            }

            return Task.FromResult(PreconditionResult.FromError("You do not have permission to use this role."));
        }
    }
}
