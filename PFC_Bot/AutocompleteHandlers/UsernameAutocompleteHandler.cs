using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace PFC_Bot.AutocompleteHandlers
{
    public class UsernameAutocompleteHandler : AutocompleteHandler
    {
        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            ApplicationDbContext database = (ApplicationDbContext)services.GetService(typeof(ApplicationDbContext));
            
            List<UserEntity> users = database.Users.Where(e=> (e.Pseudo.StartsWith((string)autocompleteInteraction.Data.Current.Value) && e.Freeze == false)).ToList();


            List<AutocompleteResult> results = new List<AutocompleteResult>();
            foreach(UserEntity user in users)
            {
                results.Add(new AutocompleteResult(user.Pseudo, user.Pseudo));
            }

            IEnumerable<AutocompleteResult> iresults = results;
            return AutocompletionResult.FromSuccess(results.Take(25));
        }
    }
}
