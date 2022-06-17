using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace PFC_Bot.AutocompleteHandlers
{
    public class ProvockSentenceAutocompleteHandler : AutocompleteHandler
    {
        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            IEnumerable<AutocompleteResult> results = new[]
            {
                new AutocompleteResult("Je vais t'en mettre plein la vue !", "Je vais t'en mettre plein la vue !"),
                new AutocompleteResult("T'es prêt à te battre ?", "T'es prêt à te battre ?"),
                new AutocompleteResult("La blanquette est bonne ?", "La blanquette est bonne ?")
            };

            // max - 25 suggestions at a time (API limit)
            return AutocompletionResult.FromSuccess(results.Take(25));

        }
    }
}
