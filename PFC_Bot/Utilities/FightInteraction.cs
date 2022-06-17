using System;
using Discord;

namespace PFC_Bot.Utilities
{
    public class FightInteraction
    {

        public FightEntity Fight { get; }

        public ulong MyId { get; }
        public Boolean IsDiscordId { get; }

        public UserEntity Me { get; }
        public ulong MyMessageId { get; }
        public String MyMessageJumpUrl { get; }

        public UserEntity TheOtherOne { get; }
        public ulong TheOtherOneMessageId { get; }
        public String TheOtherOneMessageJumpUrl { get; }


        public FightInteraction(FightEntity fight, ulong myId, Boolean isDiscordId=true)
        {
            Fight = fight;
            MyId = myId;
            IsDiscordId = isDiscordId;

            Console.Out.WriteLine($"{fight.Attacker} && {fight.Defender}");
            Console.Out.WriteLine($"Attacker: {fight.Attacker.Id_Discord}\nDefender: {fight.Defender.Id_Discord}\nMyId: {MyId}");
            if (isDiscordId && myId == fight.Attacker.Id_Discord ||
                !isDiscordId && myId == fight.Attacker.Id)
            {
                Me = fight.Attacker;
                TheOtherOne = fight.Defender;
                MyMessageId = (ulong)fight.Id_Message_Attacker;
                MyMessageJumpUrl = fight.Jump_Url_Attacker;
                TheOtherOneMessageId = (ulong)fight.Id_Message_Defender;
                TheOtherOneMessageJumpUrl = fight.Jump_Url_Defender;
            }
            else if (isDiscordId && myId == fight.Defender.Id_Discord ||
                !isDiscordId && myId == fight.Defender.Id)
            {
                Me = fight.Defender;
                TheOtherOne = fight.Attacker;
                MyMessageId = (ulong)fight.Id_Message_Defender;
                MyMessageJumpUrl = fight.Jump_Url_Defender;
                TheOtherOneMessageId = (ulong)fight.Id_Message_Attacker;
                TheOtherOneMessageJumpUrl = fight.Jump_Url_Attacker;

            }
            else
            {
                throw new Exception("L'identifiant donné n'est ni l'attaquant ni le défenseur");
            }
        }

    }
}
