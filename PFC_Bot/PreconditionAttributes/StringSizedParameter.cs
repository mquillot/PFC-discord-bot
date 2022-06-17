using System;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;

namespace PFC_Bot.PreconditionAttributes
{
    /// <summary>
    ///     Check the size of a parameter
    /// </summary>
    public class StringSizedParameter : ParameterPreconditionAttribute
    {

        int min;
        int max;
        bool canBeEmpty;
        bool canBeNull;

        /// <summary>
        ///     Specify the minimum size and the maximum size. ( min <= size <= max )
        /// </summary>
        public StringSizedParameter(int min, int max, bool canBeEmpty = false, bool canBeNull = false)
        {
            this.canBeEmpty = canBeEmpty;
            this.canBeNull = canBeNull;
            if (min >= max)
            {
                throw new Exception("min doit être strictement inférieur à max.");
            }
            this.min = min;
            this.max = max;
        }

        public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, IParameterInfo parameterInfo, object? value, IServiceProvider services)
        {

            String message = (String)value;

            if (canBeNull && value == null || value != null && (message.Length >= min && message.Length <= max || message.Length == 0 && this.canBeEmpty))
                return PreconditionResult.FromSuccess();
            else
                return PreconditionResult.FromError($"Le message doit contenir entre {this.min} et {this.max} caractères.");
        }
    }
}