using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace PFC_Bot.AutocompleteHandlers
{
    public class ProvockGifAutocompleteHandler : AutocompleteHandler
    {
        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            IEnumerable<AutocompleteResult> results = new[]
            {
                new AutocompleteResult("OSS117 bien dormi ?", "https://media.giphy.com/media/qVmtTKfh2sqx4Y37dg/giphy.gif"),
                new AutocompleteResult("Kaamelott Raclette !", "https://media.giphy.com/media/Zkw83bfonqWMH79Nm9/giphy.gif"),
                new AutocompleteResult("Kaamelott Au Bûchet !", "https://media.giphy.com/media/bZHv7NWtiakmI/giphy.gif")
            };

            // max - 25 suggestions at a time (API limit)
            return AutocompletionResult.FromSuccess(results.Take(25));

        }
    }
}
