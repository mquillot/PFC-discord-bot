using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace PFC_Bot.AutocompleteHandlers
{
    public class SignatureSentenceAutocompleteHandler : AutocompleteHandler
    {
        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            IEnumerable<AutocompleteResult> results = new[]
            {
                new AutocompleteResult("T'en as pris plein la vue !", "T'en as pris plein la vue !"),
                new AutocompleteResult("Si tu me vois signer, c'est que t'es nul !", "Si tu me vois signer, c'est que t'es nul !"),
                new AutocompleteResult("C'est la piquette Jack ! Tu ne sais pas jouer Jack ! T'es mauvais !", "C'est la piquette Jack ! Tu ne sais pas jouer Jack ! T'es mauvais !")
            };

            // max - 25 suggestions at a time (API limit)
            return AutocompletionResult.FromSuccess(results.Take(25));

        }
    }
}
