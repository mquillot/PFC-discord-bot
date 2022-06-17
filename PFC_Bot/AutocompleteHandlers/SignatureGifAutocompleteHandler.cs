using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace PFC_Bot.AutocompleteHandlers
{
    public class SignatureGifAutocompleteHandler : AutocompleteHandler
    {
        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            IEnumerable<AutocompleteResult> results = new[]
            {
                new AutocompleteResult("OSS 117", "https://media.giphy.com/media/VC96RhR4TT9AI/giphy.gif"),
                new AutocompleteResult("Drunk car", "https://media.giphy.com/media/OQ4PtxAZUM4p2/giphy-downsized-large.gif"),
                new AutocompleteResult("Experts Miami", "https://media.giphy.com/media/v9rfTQBNqdsSA/giphy.gif")
            };

            // max - 25 suggestions at a time (API limit)
            return AutocompletionResult.FromSuccess(results.Take(25));

        }
    }
}
