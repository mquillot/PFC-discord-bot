using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;

namespace PFC_Bot.PreconditionAttributes
{
    public class NoSpecialCharacterParameter : ParameterPreconditionAttribute
    {
        public NoSpecialCharacterParameter()
        {

        }

        public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, IParameterInfo parameterInfo, object value, IServiceProvider services)
        {
            String message = (String)value;
            bool isAlphaNum = true;
            for(int i=0; i<message.Length; i++)
            {
                if(!char.IsLetterOrDigit(message[i]))
                {
                    isAlphaNum = false;
                }
            }
            if (isAlphaNum)
            {
                return PreconditionResult.FromSuccess();
            }
            else
            {
                return PreconditionResult.FromError($"Seuls les caractères alphanumériques sont acceptés.");
            }
        }
    }
}
