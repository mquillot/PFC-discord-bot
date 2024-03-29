﻿using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using PFC_Bot.PreconditionAttributes;
using PFC_Bot.Services;
using PFC_Bot.Utilities;
using PFC_Bot.AutocompleteHandlers;
using Microsoft.Extensions.Configuration;

namespace PFC_Bot.Services
{
    // interation modules must be public and inherit from an IInterationModuleBase

    public class PFCGamePlayCommands : InteractionModuleBase<SocketInteractionContext>
    {
        // dependencies can be accessed through Property injection, public properties with public setters will be set by the service provider
        public InteractionService Commands { get; set; }

        private CommandHandler _handler;
        private readonly ApplicationDbContext _db;
        private readonly DiscordSocketClient _discord;
        private IConfigurationRoot _settings;

        private ComponentBuilder _choicesBuilder;
        private ComponentBuilder _waitingOtherBuilder;
        private ComponentBuilder _removeBuilder;

        private ComponentBuilder _replayBuilder;

        // constructor injection is also a valid way to access the dependecies
        public PFCGamePlayCommands(DiscordSocketClient discord, CommandHandler handler, IConfigurationRoot settings,  ApplicationDbContext db)
        {
            _handler = handler;
            _db = db;
            _discord = discord;
            _settings = settings;

            _choicesBuilder = new ComponentBuilder()
                .WithButton("pierre", "jouer:pierre", style: ButtonStyle.Secondary, emote: new Emoji("\U0000270A"))
                .WithButton("feuille", "jouer:feuille", style: ButtonStyle.Secondary, emote: new Emoji("\U0000270B"))
                .WithButton("ciseaux", "jouer:ciseaux", style: ButtonStyle.Secondary, emote: new Emoji("\U0000270C"));

            _waitingOtherBuilder = new ComponentBuilder()
                .WithButton("annuler", "annuler", style: ButtonStyle.Secondary, emote: new Emoji("\U0000274C"));

            _removeBuilder = new ComponentBuilder()
                .WithButton("supprimer", "supprimer", style: ButtonStyle.Secondary, emote: new Emoji("\U0000274C"));

            _replayBuilder = new ComponentBuilder()
                .WithButton("rejouer", "rejouer", style: ButtonStyle.Secondary, emote: new Emoji("\U0001F504"));

        }




        [ComponentInteraction("annuler")]
        public async Task CancelFight()
        {

            SocketMessageComponent component = (SocketMessageComponent)this.Context.Interaction;
            FightEntity fight = this.SearchFightOfComponentUtility(component, true);
            fight.Canceled = true;
            _db.SaveChanges();

            // Get Messages
            IMessageChannel dmAttacker = await getMessageChannelUtility(fight.Attacker.Id_Discord, null, true);
            IMessageChannel dmDefender = await getMessageChannelUtility(fight.Defender.Id_Discord, null, true);
            IMessageChannel theOtherOneMessageChannel =
                component.User.Id == fight.Attacker.Id_Discord ? dmDefender : dmAttacker;
            IMessageChannel myMessageChannel =
                component.User.Id == fight.Attacker.Id_Discord ? dmAttacker : dmDefender;


            await ModifyDMEmbedMessage(
                dmAttacker,
                (ulong)fight.Id_Message_Attacker,
                "Le match a été annulé :x:",
                new ComponentBuilder());


            await ModifyDMEmbedMessage(
                dmDefender,
                (ulong)fight.Id_Message_Defender,
                "Le match a été annulé :x:",
                new ComponentBuilder());
        }


        [ComponentInteraction("supprimer")]
        public async Task RemoveButtonHandler()
        {
            SocketMessageComponent component = (SocketMessageComponent)this.Context.Interaction;
            await component.Message.DeleteAsync();
            return;
        }


        [ComponentInteraction("rejouer")]
        public async Task ReplayButtonHandler()
        {
            
            SocketMessageComponent component = (SocketMessageComponent)this.Context.Interaction;

            if (!component.HasResponded)
                await component.DeferAsync();

            _db.Users.ToList();
            FightEntity fight = this.SearchFightOfComponentUtility(component, true);

            FightInteraction fightInteraction = new FightInteraction(fight, component.User.Id, true);

#if DEBUG
#else
            // Est-ce que les deux joueurs sont les mêmes ?
            if (fight.Defender.Id == fight.Attacker.Id)
            {
                await RespondAsync($"Vous ne pouvez pas vous auto-attaquer", ephemeral: true);
                return;
            }
#endif

            if (fight.Attacker.Freeze)
            {
                await RespondAsync("Le joueur a gelé son compte.");
                return;
            }

            FightEntity fightSearch = _db.Fights.SingleOrDefault(e =>
                ((e.Attacker.Id == fight.Defender.Id && e.Defender.Id == fight.Attacker.Id) || (e.Defender.Id == fight.Defender.Id && e.Attacker.Id == fight.Attacker.Id)) &&
                !(e.Choice_Attacker != null && e.Choice_Defender != null) &&
                e.Winner == null &&
                e.Canceled == false
                );

            IMessageChannel channelAttacker = await getMessageChannelUtility(fight.Defender.Id_Discord, null, true);

            if (fightSearch != null)
            {
                await channelAttacker.SendMessageAsync($"Vous avez déjà un combat avec {fight.Attacker.Pseudo}", components: _removeBuilder.Build());
                return;
            }

            FightEntity newFight = new FightEntity();
            newFight.Attacker = fight.Defender;
            newFight.Defender = fight.Attacker;
            newFight.Posting_Date = DateTime.Now;
            newFight.Ending_Date = DateTime.Now;



            var embed_builder_attacker = new EmbedBuilder()
            {
                Color = Color.Green,
                Title = $"Vous attaquez {newFight.Defender.Pseudo}",
                Description = $"Quel est votre choix ?"
            };
            IUserMessage attackerMessage = await channelAttacker.SendMessageAsync("", embed: embed_builder_attacker.Build(), components: _choicesBuilder.Build());


            newFight.Jump_Url_Attacker = attackerMessage.GetJumpUrl();
            newFight.Id_Message_Attacker = attackerMessage.Id;

            _db.Fights.Add(newFight);
            _db.SaveChanges();
            return;
        }





        [ComponentInteraction("jouer:*")]
        public async Task PlayButtonHandler(string choice)
        {
            SocketMessageComponent component = (SocketMessageComponent)this.Context.Interaction;

            if (!component.HasResponded)
                await component.DeferAsync();

            Console.Out.WriteLine(choice);
            // Récupérer le combat
            FightEntity fight;
            List<UserEntity> users = _db.Users.ToList();
            fight = SearchFightOfComponentUtility(component);


            // Si le combat n'existe pas
            if (fight == null)
            {
                await component.ModifyOriginalResponseAsync(
                    e => e.Components = new ComponentBuilder().Build());
                return;
            }

            // Est-ce que le choix est l'un de ceux attendus ?
            List<String> choices = new List<String>();
            choices.Add("pierre");
            choices.Add("feuille");
            choices.Add("ciseaux");

            if (!choices.Contains(choice))
                return;

            IMessageChannel channelAttacker;
            IMessageChannel channelDefender;


            // L'attaquant n'a pas joué ?
            if (fight.Choice_Attacker == null &&
                fight.Id_Message_Attacker == component.Message.Id &&
                component.User.Id == fight.Attacker.Id_Discord)
            {

                // Update fight with choice
                Console.Out.WriteLine(choice);
                fight.Choice_Attacker = choice[0];
                _db.SaveChanges();


                // Update attacker message
                channelAttacker = await getMessageChannelUtility(fight.Attacker.Id_Discord, null, true);
                await ModifyDMEmbedMessage(channelAttacker, (ulong)fight.Id_Message_Attacker, $"Vous avez joué {choice}", new ComponentBuilder());

                string description = $"Quel est votre choix ?";

                if (fight.Provock_Sentence != null)
                    description = $"Le joueur adverse te provoque. Je cite :\n> {fight.Provock_Sentence}";

                // Send Message to defender
                var embed_builder_defender = new EmbedBuilder()
                {
                    Color = Color.Green,
                    Title = $"{fight.Attacker.Pseudo} vous a attaqué",
                    Description = description,
                    ImageUrl = fight.Provock_Gif
                };

                channelDefender = await getMessageChannelUtility(fight.Defender.Id_Discord, null, true);
                var defenderMessage = await channelDefender.SendMessageAsync("", embed: embed_builder_defender.Build(), components: _choicesBuilder.Build());

                // Record in database message information
                fight.Jump_Url_Defender = defenderMessage.GetJumpUrl();
                fight.Id_Message_Defender = defenderMessage.Id;
                _db.SaveChanges();
            }


            // L'attaquant a déjà joué mais pas le défenseur ?
            else if (fight.Choice_Attacker != null
                && fight.Choice_Defender == null
                && fight.Id_Message_Defender == component.Message.Id
                && component.User.Id == fight.Defender.Id_Discord)
            {
                // Update fight with choice
                Console.Out.WriteLine("On a fait quelque chose");
                Console.Out.WriteLine(choice);
                fight.Choice_Defender = choice[0];
                string defenderDecisionDescription = $"Vous avez joué {choice}";

                bool attackerWins = ResolveFightUtility(ref fight);

                // Un gagnant ?
                if (fight.Winner != null)
                {
                    UserEntity looser = fight.Attacker == fight.Winner ? fight.Attacker : fight.Defender;
                    fight.Winner.Money += 1;
                    fight.Winner.Score += 5;



                    // Loose in a row 
                    fight.Winner.Defeat_In_A_Row = 0;
                    looser.Defeat_In_A_Row = looser.Defeat_In_A_Row + 1;
                    looser.Max_Defeat_In_A_Row = looser.Defeat_In_A_Row > looser.Max_Defeat_In_A_Row ?
                        looser.Defeat_In_A_Row :
                        looser.Max_Defeat_In_A_Row;

                    // Wins in a row
                    looser.Win_In_A_Row = 0;
                    fight.Winner.Win_In_A_Row = fight.Winner.Win_In_A_Row + 1;
                    fight.Winner.Max_Win_In_A_Row = looser.Win_In_A_Row > looser.Max_Win_In_A_Row ?
                        fight.Winner.Win_In_A_Row :
                        fight.Winner.Max_Win_In_A_Row;


                    _db.SaveChanges();

                    // TODO: Est-ce que la personne a changé de rang ?

                    // Est-ce que la personne a perdu plusieurs fois d'affilé ? 5, 10 ...
                    if(looser.Defeat_In_A_Row % 2 == 0)
                    {
                        // send a message
                        ITextChannel chan = (ITextChannel) await _discord.GetChannelAsync(_settings.GetValue<ulong>("chans:wallOfEpicness"));
                        await chan.SendMessageAsync($"<@{looser.Id_Discord}> a perdu {looser.Defeat_In_A_Row} fois d'affilé ! :sob:");
                    }

                    if (fight.Winner.Win_In_A_Row % 2 == 0)
                    {
                        // send a message
                        ITextChannel chan = (ITextChannel)await _discord.GetChannelAsync(_settings.GetValue<ulong>("chans:wallOfEpicness"));
                        await chan.SendMessageAsync($"<@{fight.Winner.Id_Discord}> a gagné {fight.Winner.Win_In_A_Row} fois d'affilé ! :muscle:");
                    }


                    string descriptionToWinner = "Vous avez gagné ! :partying_face:";
                    if (fight.Winner.Signature_Sentence != "" || fight.Winner.Signature_Url != "")
                        descriptionToWinner += "\nVotre signature a bien été envoyée.";
                    string descriptionToLooser = "Vous avez perdu ... :crying_cat_face:";
                    string descriptionToAddToAttacker = attackerWins ?
                        descriptionToWinner : descriptionToLooser;
                    string descriptionToAddToDefender = attackerWins ?
                        descriptionToLooser : descriptionToWinner;
                    descriptionToAddToDefender = $"{defenderDecisionDescription}\n{descriptionToAddToDefender}";


                    // Éditer le message de l'attaquant
                    channelAttacker = await getMessageChannelUtility(fight.Attacker.Id_Discord, null, true);
                    await ModifyDMEmbedMessage(channelAttacker, (ulong)fight.Id_Message_Attacker, descriptionToAddToAttacker);
                    

                    // Éditer le message du défenseur
                    channelDefender = await getMessageChannelUtility(fight.Defender.Id_Discord, null, true);
                    await ModifyDMEmbedMessage(channelDefender, (ulong)fight.Id_Message_Defender, descriptionToAddToDefender, _replayBuilder);

                    // Envoyer une notification à l'attaquant
                    String descriptionToSend = fight.Winner == fight.Attacker ?
                        $"Vous avez gagné le combat contre {fight.Defender.Pseudo} :partying_face: [Lien vers le combat]({fight.Jump_Url_Attacker})" :
                        $"Vous avez perdu le combat contre {fight.Defender.Pseudo} :crying_cat_face: [Lien vers le combat]({fight.Jump_Url_Attacker})";

                    var builder = new EmbedBuilder()
                    {
                        Color = Color.DarkerGrey,
                        Description = descriptionToSend
                    };
                    if(fight.Attacker.Id == fight.Winner.Id || fight.Winner.Signature_Sentence == "") 
                        await NotifyUser(fight.Attacker, channelAttacker, builder.Build());

                    // Envoyer la signature du gagnant s'il y en a une
                    if (fight.Winner.Signature_Sentence != "" && fight.Winner.Signature_Url != "")
                    {
                        IMessageChannel channelSignature = fight.Winner == fight.Attacker ? channelDefender : channelAttacker;

                        string linkMessageFight = fight.Winner.Id == fight.Attacker.Id ? fight.Jump_Url_Defender : fight.Jump_Url_Attacker;
                        var signatureEmbedBuilder = new EmbedBuilder()
                        {
                            //Optional color
                            Color = Color.Green,
                            Title = $"{fight.Winner.Pseudo} vous a défoncé",
                            Description = $"{fight.Winner.Pseudo} vous a laissé une signature\n> {fight.Winner.Signature_Sentence}\n[Lien vers le combat]({linkMessageFight})",
                            ImageUrl = fight.Winner.Signature_Url
                        };

                        await NotifyUser(fight.Winner == fight.Attacker ? fight.Defender: fight.Attacker, channelSignature, signatureEmbedBuilder.Build());
                    }
                }

                // Égalité ?
                else
                {
                    _db.SaveChanges();

                    String messageEquality = "Égalité\nFin du combat";

                    String messageEqualityDefender = $"{defenderDecisionDescription}\n{messageEquality}";
                    // Éditer le message de l'attaquant
                    channelAttacker = await getMessageChannelUtility(fight.Attacker.Id_Discord, null, true);
                    await ModifyDMEmbedMessage(channelAttacker, (ulong)fight.Id_Message_Attacker, messageEquality);

                    // Éditer le message du défenseur
                    channelDefender = await getMessageChannelUtility(fight.Defender.Id_Discord, null, true);
                    await ModifyDMEmbedMessage(channelDefender, (ulong)fight.Id_Message_Defender, messageEqualityDefender, _replayBuilder);


                    // Envoyer une notification à l'attaquant
                    String descriptionToSend = $"Vous avez fait égalité avec {fight.Defender.Pseudo} [Lien vers le combat]({fight.Jump_Url_Attacker})";

                    var builder = new EmbedBuilder()
                    {
                        Color = Color.DarkerGrey,
                        Description = descriptionToSend
                    };

                    await NotifyUser(fight.Attacker, channelAttacker, builder.Build());
                }



            }

            // Une erreur éventuelle à ne pas prendre en compte
            else
                return;
        }


        [SlashCommand("attack", "Attaque un joueur")]
        [RequireSignUp]
        public async Task Attack(
            [Autocomplete(typeof(UsernameAutocompleteHandler))]String username,
            [Autocomplete(typeof(ProvockSentenceAutocompleteHandler)),StringSizedParameter(5, 255, false, true)]String? provockSentence = null,
            [Autocomplete(typeof(ProvockGifAutocompleteHandler)),StringSizedParameter(5, 255, false, true)]String? provockGif = null
            )
        {
            // Si ce n'est pas en DM, CIAO
            Console.Out.WriteLine(Context);
            if (!Context.Interaction.IsDMInteraction)
            {
                await this.RespondAsync("Cette commande doit être utilisée en DM", ephemeral:true);
                return;
            }

            // Est-ce que l'utilisateur à attaquer existe ?
            UserEntity user_defender = _db.Users.SingleOrDefault(e => e.Pseudo == username);
            if (user_defender == null || user_defender.Freeze)
            {
                await RespondAsync($"L'utilisateur {username} n'existe pas ou s'est désactivé.", ephemeral: true);
                return;
            }


            // Récupérer l'attaquant ?
            UserEntity user_attacker = _db.Users.SingleOrDefault(e => e.Id_Discord == Context.User.Id);

#if DEBUG
#else
            // Est-ce que les deux joueurs sont les mêmes ?
            if (user_defender.Id == user_attacker.Id)
            {
                await RespondAsync($"Vous ne pouvez pas vous auto-attaquer", ephemeral: true);
                return;
            }
#endif

            // Est-ce que les deux personnes ont déjà un combat ?
            FightEntity fight = _db.Fights.SingleOrDefault(e =>
                ((e.Attacker.Id == user_attacker.Id && e.Defender.Id == user_defender.Id) || (e.Defender.Id == user_attacker.Id && e.Attacker.Id == user_defender.Id)) &&
                !(e.Choice_Attacker != null && e.Choice_Defender != null) &&
                e.Winner == null &&
                e.Canceled == false
                );

            if (fight != null)
            {
                await RespondAsync($"Vous avez déjà un combat avec {username}", ephemeral: true);
                return;
            }

            await RespondAsync("Votre combat a bien été envoyé !", ephemeral: true);

            FightEntity new_fight = new FightEntity();
            new_fight.Attacker = user_attacker;
            new_fight.Defender = user_defender;
            new_fight.Posting_Date = DateTime.Now;
            new_fight.Ending_Date = DateTime.Now;
            new_fight.Provock_Gif = provockGif;
            new_fight.Provock_Sentence = provockSentence;


            // Envoyer un message à l'attaquant (en DM ou sur le canal)
            IUserMessage attacker_message = null;

            string description = $"Quel est votre choix ?";

            if(provockGif != null)
            {
                description += "\nVous avez envoyé un gif";
            }
            if(provockSentence != null)
            {
                description += $"\nVous avez envoyer une phrase de provocation.\n> {provockSentence}";
            }
            

            var embed_builder_attacker = new EmbedBuilder()
            {
                //Optional color
                Color = Color.Green,
                Title = $"Vous attaquez {user_defender.Pseudo}",
                Description = description,
                ImageUrl = provockGif
            };


            attacker_message = await ReplyAsync("", embed: embed_builder_attacker.Build(), components: _choicesBuilder.Build());
            new_fight.Jump_Url_Attacker = attacker_message.GetJumpUrl();
            new_fight.Id_Message_Attacker = attacker_message.Id;

            _db.Fights.Add(new_fight);
            _db.SaveChanges();
        }


        [SlashCommand("signup", "Permet de s'inscrire au jeu en donnant son pseudo")]
        [RequireNotSignUp()]
        public async Task SignUp([StringSizedParameter(5, 20, false)][NoSpecialCharacterParameter()]string username)
        {
            // Si on a bien donné un username
            if(username.CompareTo("") != 0)
            {
                // Est-ce que le pseudo existe déjà ?
                UserEntity user = _db.Users.SingleOrDefault(e => e.Pseudo == username);
                if (user != null)
                {
                    await RespondAsync($"Le pseudo {username} existe déjà !");
                    return;
                }
            }


            UserEntity user2 = _db.Users.SingleOrDefault(e => e.Id_Discord == Context.User.Id);
            if (user2 != null)
            {
                await RespondAsync($"Vous êtes déjà inscrit(e).");
                return;
            }

            String used_username = username;
            if(username == "")
            {
                used_username = Context.User.Username;
            }

            UserEntity new_user = new UserEntity();
            new_user.Id_Discord = Context.User.Id;
            new_user.Pseudo = used_username;

            _db.Users.Add(new_user);
            _db.SaveChanges();


            EmbedBuilder embedBuilder = new EmbedBuilder()
            {
                Title = "Inscription réussie !",
                Description = $"Jeune Pillow, tu as bien réussi à t'inscrire en tant que {used_username} :partying_face:. Voici quelques rudimentaires pour apprendre à jouer.\n\n" +
                $"Tout d'abord, sâche que tu peux lancer des combats de pierre-feuille-ciseaux contre des gens. Il suffit pour ça d'utiliser la commande attack comme ci-dessous :\n" +
                $"```/attack [pseudo du joueur à attaquer]```\n" +
                $"\n" +
                $"Chaque fois que tu gagnes, tu récupères 1 papoule, c'est ta monnaie ! Grâce aux papoules tu peux lancer des sorts sur les autres joueurs comme la s0ckattack :\n" +
                $"```/s0cattack [pseudo du joueur cible]```\n" +
                $"\n" + 
                $"Tu peux aussi consulter ton profil !\n" +
                $"```/my-profile```\n" +
                $"Si tu veux flex sur un de tes potes, tu peux utiliser cette commande (directement sur le serveur :wink:)\n" +
                $"```/stats-with [pseudo du joueur cible]```\n" +
                $"\n"
            };

            await RespondAsync("", embed: embedBuilder.Build());
        }





        [SlashCommand("test-signup", "Un test pour voir le message de signup")]
        [RequireNotSignUp()]
        public async Task testSignUp()
        {
            EmbedBuilder embedBuilder = new EmbedBuilder()
            {
                Title = "Inscription réussie !",
                Description = "Jeune Pillow, tu as bien réussi à t'inscrire en tant que {used_username} :partying_face:. Voici quelques rudimentaires pour apprendre à jouer.\n\n" +
                $"Tout d'abord, sâche que tu peux lancer des combats de pierre-feuille-ciseaux contre des gens. Il suffit pour ça d'utiliser la commande attack comme ci-dessous :\n" +
                $"```/attack [pseudo du joueur à attaquer]```\n" +
                $"\n" +
                $"Chaque fois que tu gagnes, tu récupères 1 papoule, c'est ta monnaie ! Grâce aux papoules tu peux lancer des sorts sur les autres joueurs comme la s0ckattack :\n" +
                $"```/s0cattack [pseudo du joueur cible]```\n" +
                $"\n" +
                $"Tu peux aussi consulter ton profil !\n" +
                $"```/my-profile```\n" +
                $"Si tu veux flex sur un de tes potes, tu peux utiliser cette commande (directement sur le serveur :wink:)\n" +
                $"```/stats-with [pseudo du joueur cible]```\n" +
                $"\n"
            };

            await RespondAsync("", embed: embedBuilder.Build());
        }




        [SlashCommand("test-joined-server", "Un test pour voir le message lorsque quelqu'un rejoint le serveur")]
        [RequireNotSignUp()]
        public async Task testJoinedServer()
        {
            EmbedBuilder embedBuilder = new EmbedBuilder()
            {
                Title = "Bonjour jeune futur Pillow",
                Description = $"Je suis le bot du Pierre-Feuille-Ciseaux (PFC-Bot) et tu viens de rejoindre le serveur de StillinBed.\n\nSur ce serveur, nous jouons toutes et tous (ou presque) au PFC. Quand tu t'embrouilles avec quelqu'un, allez hop un PFC et il n'y a plus d'embrouille ! Quand tu ne t'embrouilles pas avec quelqu'un, allez hop un PFC et tu t'embrouilles avec cette personne ! **Le PFC est vraiment la solution à tous tes problème alors n'attends plus pour jouer !**\n" +
                $"\nConvaincu(e) ? Tu veux jouer avec nous ?\n" +
                $"\n[Là, tu réponds oui :rolling_eyes:]\n" +
                $"\nSuper ! :sunglasses: Pour jouer, rien de plus simple, il faut d'abord que tu t'enregistres auprès de moi. Je t'expliquerai le reste ensuite. :wink:\n" +
                $"Pour t'enregistrer, tu dois m'écrire la commande suivante, en remplaçant \"[ton pseudo]\" par le pseudo que tu veux avoir dans le jeu " +
                $"```/signup [ton pseudo]```\n" +
                $"\nTu vois, c'est super simple !\n" +
                $"Allez ! À de suite !",
                ImageUrl = "https://media.giphy.com/media/l49JLqDArrAoVy4wM/giphy.gif"
            };

            await RespondAsync("", embed: embedBuilder.Build());
        }




        [SlashCommand("users", "Liste les utilisateurs")]
        public async Task ListUsers()
        {

            List<ulong> connectedList = new List<ulong>();
            List<ulong> notConnectedList = new List<ulong>();
            if (Context.Guild != null)
            {
                List<IReadOnlyCollection<IGuildUser>> usersGuild = await Context.Guild.GetUsersAsync().ToListAsync();


                foreach (IReadOnlyCollection<IGuildUser> userCollection in usersGuild)
                {
                    foreach(IGuildUser user in userCollection)
                    {
                        if(user.Status == UserStatus.Online && !user.IsBot)
                        {
                            connectedList.Add(user.Id);
                        }
                        else if(!user.IsBot)
                        {
                            notConnectedList.Add(user.Id);
                        }
                    }
                }
            }
            
            var users = _db.Users.OrderBy(u => u.Pseudo);
            String usernames = "";
            String connectedUsernames = "";
            foreach (UserEntity user in users)
            {
                if(connectedList.Contains(user.Id_Discord)) {
                    connectedUsernames += ":green_circle: ";
                    connectedUsernames += Context.Guild == null ?
                        $"{user.Pseudo}\n" :
                        $"<@{user.Id_Discord}>\n";
                }
                else if(notConnectedList.Contains(user.Id_Discord))
                {
                    usernames += Context.Guild == null ?
                        $"{user.Pseudo}\n" :
                        $"<@{user.Id_Discord}>\n";
                }
                else
                {
                    usernames += Context.Guild == null ?
                        $"{user.Pseudo}\n" :
                        $"{user.Pseudo}\n";
                }
            }

            var builder = new EmbedBuilder()
            {
                Color = Color.Green,
                Description = "Liste des utilisateurs"
            };

            builder.AddField(x =>
            {
                x.Name = "Nom";
                x.Value = $"{connectedUsernames}{usernames}";
                x.IsInline = true;
            });

            await RespondAsync("", embed: builder.Build(), ephemeral: true);
        }


        [SlashCommand("rank", "Liste les 20 meilleurs utilisateurs")]
        public async Task Rank(bool ephemeral = false)
        {
            List<UserEntity> users = _db.Users.OrderByDescending(x => x.Score).Take(20).ToList();

            String usernames = "";
            String scores = "";
            int i = 1;
            foreach (UserEntity user in users) {
                if (i == 1)
                    usernames += ":first_place: ";
                else if (i == 2)
                    usernames += ":second_place: ";
                else if (i == 3)
                    usernames += ":third_place: ";
                else
                    usernames += $"{i}- ";
                usernames += $"{user.Pseudo}\n";
                scores += $"{user.Score}\n";
                i++;
            }

            var builder = new EmbedBuilder()
            {
                //Optional color
                Color = Color.Green,
                Description = "Rank des meilleurs joueurs"
            };

            builder.AddField(x =>
            {
                x.Name = "Nom";
                x.Value = usernames;
                x.IsInline = true;
            });

            builder.AddField(x =>
            {
                x.Name = "Score";
                x.Value = scores;
                x.IsInline = true;
            });

            await RespondAsync("", embed:builder.Build(), ephemeral: ephemeral);
        }


        [SlashCommand("myfights", "show my fights")]
        [RequireSignUp()]
        public async Task MyFights()
        {
            
            List<FightEntity> fights = _db.Fights.Where(
                e =>
                (e.Attacker.Id_Discord == Context.User.Id || e.Defender.Id_Discord == Context.User.Id && e.Jump_Url_Defender != null) &&
                (e.Choice_Attacker == null || e.Choice_Defender == null)
                ).Take(20).ToList();
            List<UserEntity> users = _db.Users.ToList();

            UserEntity user = _db.Users.SingleOrDefault(e => e.Id_Discord == Context.User.Id);

            String message_sent = ":crossed_swords: Combats envoyés :\n";
            String message_received = ":shield: Combats reçus :\n";
            int nb_fight_sent = 0;
            int nb_fight_received = 0;
            //Console.Out.WriteLine($"Le nombre d'éléments : {fights.Count}");
            foreach(FightEntity fight in fights)
            {
                string fightUrl = fight.Attacker.Id == user.Id ? fight.Jump_Url_Attacker: fight.Jump_Url_Defender;
                string fightPseudo = fight.Attacker.Id == user.Id ? fight.Defender.Pseudo : fight.Attacker.Pseudo;
                //Console.Out.WriteLine($"{fight.Attacker.Id} : {fight.Attacker.Pseudo} ({fight.choice_attacker}) <=> {fight.Defender.Pseudo} ({fight.choice_defender})");

                String message_to_add = $"[{fightPseudo}]({fightUrl})" + "\n";
                if (fight.Attacker.Id == user.Id)
                {
                    message_sent += message_to_add;
                    nb_fight_sent++;
                }
                else
                {
                    message_received += message_to_add;
                    nb_fight_received++;
                }
            }

            if(nb_fight_sent == 0)
            {
                message_sent += "Aucun";
            }
            if(nb_fight_received == 0)
            {
                message_received += "Aucun";
            }


            var builder = new EmbedBuilder()
            {
                //Optional color
                Title = $"Vous avez {fights.Count} combats en cours",
                Color = Color.Green,
                Description = message_sent + "\n" + message_received,
                Url = "https://www.twitch.tv/stillinbed",
                Footer = new EmbedFooterBuilder() {
                    IconUrl = "https://static-cdn.jtvnw.net/jtv_user_pictures/3aa40a4d-12cb-49aa-94f7-2af93d8fe7eb-profile_image-70x70.png",
                    Text = "join StillinBed on Twitch"
                }
            };

            await RespondAsync("", embed: builder.Build());
        }

        [SlashCommand("set-signature", "définir ma signature, un texte et un gif")]
        [RequireSignUp()]
        public async Task Signature([Autocomplete(typeof(SignatureSentenceAutocompleteHandler)),StringSizedParameter(5, 255, false)] string signature, [Autocomplete(typeof(SignatureGifAutocompleteHandler)),StringSizedParameter(5, 255, false)] string signatureGif)
        {
            UserEntity user = _db.Users.SingleOrDefault(e => e.Id_Discord == Context.User.Id);
            user.Signature_Sentence = signature;
            user.Signature_Url = signatureGif;
            _db.SaveChanges();
            var builder = new EmbedBuilder()
            {
                //Optional color
                Color = Color.Green,
                Description = $"Votre signature a bien été modifiée\n> {user.Signature_Sentence}",
                ImageUrl = user.Signature_Url
            };

            
            await RespondAsync("", embed: builder.Build(), components: _removeBuilder.Build());
        }


        [SlashCommand("my-signature", "permet de voir une signature si on en a une")]
        [RequireSignUp()]
        public async Task MySignature()
        {
            UserEntity user = _db.Users.SingleOrDefault(e => e.Id_Discord == Context.User.Id);

            var builder = new EmbedBuilder();
            if(user.Signature_Sentence != "" && user.Signature_Url != "")
            {
                builder = new EmbedBuilder()
                {
                    Color = Color.Green,
                    Description = $"Votre signature\n> {user.Signature_Sentence}",
                    ImageUrl = user.Signature_Url
                };
            }
            else
            {
                builder = new EmbedBuilder()
                {
                    Color = Color.Green,
                    Description = $"Vous n'avez en défini de signature",
                };

            }


            await RespondAsync("", embed: builder.Build(), components: _removeBuilder.Build());
        }


        [SlashCommand("stats-with", "permet de voir en détails les stats des combats avec un joueur en particulier")]
        [RequireSignUp()]
        public async Task StatsWith([Autocomplete(typeof(UsernameAutocompleteHandler))] string username)
        {

            UserEntity userMe = _db.Users.SingleOrDefault(e => e.Id_Discord == Context.User.Id);

            UserEntity userFocus = _db.Users.SingleOrDefault(e => e.Pseudo == username);
            if(userFocus == null)
            {
                await RespondAsync($"L'utilisateur {username} n'existe pas", ephemeral: true);
                return;
            }

            List<FightEntity> fights = _db.Fights.Where(f => f.Attacker.Id_Discord == userFocus.Id_Discord && f.Defender.Id_Discord == userMe.Id_Discord || f.Attacker.Id_Discord == userMe.Id_Discord && f.Defender.Id_Discord == userFocus.Id_Discord).ToList();

            long meWins = 0;
            foreach(FightEntity fight in fights)
            {
                if(fight.Winner == userMe)
                    meWins++;
            }

            float winRate = ((float)meWins) / ((float)fights.Count);


            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = Color.Green,
                Title = $"Vos statistiques contre {userFocus.Pseudo}",
                Description = $"Taux de victoire : {winRate}\n" +
                $"Vos victoires : {meWins}\n" +
                $"Ses victoires : {fights.Count - meWins}"
            };

            await RespondAsync("", embed: builder.Build());
        }


        [SlashCommand("clear", "clear the database")]
        [RequireOwner]
        public async Task ClearDatabase()
        {
            _db.Users.RemoveRange(_db.Users);
            _db.SaveChanges();
            await RespondAsync("Tous les utilisateurs ont été supprimés");
        }



        [SlashCommand("my-profile", "montre ton profil")]
        public async Task MyProfile()
        {
            UserEntity userMe = _db.Users.SingleOrDefault(e => e.Id_Discord == Context.User.Id);

            List<UserEntity> users = _db.Users.OrderByDescending(x => x.Score).ToList();
            int myposition = 0;

            for(int i=0; i < users.Count; i++)
            {
                if (userMe == users[i])
                {
                    myposition = i + 1;
                    break;
                }
            }


            string notificationPart = userMe.Notification == true ? ":white_check_mark:": ":white_large_square:";
            string freezePart = userMe.Freeze == true ? ":white_check_mark:": ":white_large_square:";
            string positionText = myposition == 1 ? "Position : 1er" : $"Position : {myposition}ème";
            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = Color.Green,
                Title = $"Votre profile",
                Description = $"Mon pseudo : {userMe.Pseudo}\n" +
                $"Score : {userMe.Score}\n\n" +
                $":chicken: {userMe.Money}\n\n" +
                positionText + "\n" +
                $"Notifications : {notificationPart}\n" +
                $"Compte gelé ? {freezePart}\n" +
                $"Max victoires d'affilé : {userMe.Max_Win_In_A_Row}\n" +
                $"Max défaites d'affilé : {userMe.Max_Defeat_In_A_Row}"
            };

            await RespondAsync("", embed: builder.Build());
        }

        [SlashCommand("deactivate-notifications", "désactive les notifications. Vous recevrez tout de même les messages des combats.")]
        [RequireSignUp()]
        public async Task DeactivateNotifications()
        {
            UserEntity userMe = _db.Users.SingleOrDefault(e => e.Id_Discord == Context.User.Id);
            userMe.Notification = false;
            _db.SaveChanges();
            await RespondAsync("", embed: new EmbedBuilder()
            {
                Description = "Les notifications sont désactivées sur votre compte. Vous recevrez quand même les combats. \nSi vous souhaitez désactiver les combats, vous pouvez utiliser la commande \"freeze-account\""
            }.Build());
        }

        [SlashCommand("activate-notifications", "active les notifications.")]
        [RequireSignUp()]
        public async Task ActivateNotifications()
        {
            UserEntity userMe = _db.Users.SingleOrDefault(e => e.Id_Discord == Context.User.Id);
            userMe.Notification = true;
            _db.SaveChanges();
            await RespondAsync("", embed: new EmbedBuilder()
            {
                Description = "Les notifications sont activées sur votre compte."
            }.Build());
        }

        

        [SlashCommand("freeze-account", "désactive votre compte. Vous ne perdez rien.")]
        [RequireSignUp()]
        public async Task FreezeAccount()
        {
            UserEntity userMe = _db.Users.SingleOrDefault(e => e.Id_Discord == Context.User.Id);
            userMe.Freeze = true;
            _db.SaveChanges();
            await RespondAsync("", embed: new EmbedBuilder()
            {
                Description = "Votre compte a bien été gelé. Rien n'a été perdu, ne vous inquiétez pas."
            }.Build());
        }

        [SlashCommand("unfreeze-account", "active votre compte. Vous ne perdez rien.")]
        [RequireSignUp()]
        public async Task UnfreezeAccount()
        {
            UserEntity userMe = _db.Users.SingleOrDefault(e => e.Id_Discord == Context.User.Id);
            userMe.Freeze = false;
            _db.SaveChanges();
            await RespondAsync("", embed: new EmbedBuilder()
            {
                Description = "Votre compte n'est plus gelé. Allez vous battre un peu là !"
            }.Build());
        }



        [SlashCommand("s0cattack", "coûte 1 papoules, enlève 5 points à la cible et vous en enlève 5 aussi")]
        [RequireSignUp()]
        public async Task Socattack([Autocomplete(typeof(UsernameAutocompleteHandler))] string username)
        {
            // Est-ce que le joueur existe ?
            UserEntity targetUser = _db.Users.SingleOrDefault(e => e.Pseudo == username);
            if (targetUser == null || targetUser.Freeze)
            {
                await RespondAsync($"L'utilisateur {username} n'existe pas ou s'est désactivé.", ephemeral: true);
                return;
            }


            // Récupérer l'attaquant ?
            UserEntity user = _db.Users.SingleOrDefault(e => e.Id_Discord == Context.User.Id);
            int cost = 1;
            ulong scoreLossTarget = 5;
            ulong scoreLossMe = 5;

            if (user.Money < cost)
            {

                await RespondAsync($"Vous n'avez pas assez de papoules. Il en faut {cost}.", ephemeral: true);
                return;
            }

            if (targetUser.Score < scoreLossTarget)
            {

                await RespondAsync($"Votre cible doit avoir au moins {scoreLossTarget} à perdre.", ephemeral: true);
                return;
            }

            if (user.Score < scoreLossMe)
            {
                await RespondAsync($"Il vous faut au moins {scoreLossMe} de score pour lancer ce sort.");
            }

            targetUser.Score -= scoreLossTarget;
            user.Money -= cost;
            user.Score -= scoreLossMe;
            _db.SaveChanges();

            EmbedBuilder embedBuilder = new EmbedBuilder()
            {
                Description = $"Vous avez bien utilisé la s0cattack sur {targetUser.Pseudo}"
            };

            await RespondAsync("", embed: embedBuilder.Build(), ephemeral: true);

            IMessageChannel channelTarget = await getMessageChannelUtility(targetUser.Id_Discord, null, true);
            Embed embedTarget = new EmbedBuilder()
            {
                Description = $"{user.Pseudo} vous a s0ckattaqué !"
            }.Build();
            await NotifyUser(targetUser, channelTarget, embedTarget);

            ITextChannel chan = (ITextChannel)await _discord.GetChannelAsync(_settings.GetValue<ulong>("chans:wallOfEpicness"));
            await chan.SendMessageAsync($"<@{user.Id_Discord}> a s0cattacké <@{targetUser.Id_Discord}> ! :sob:");
        }




        [SlashCommand("big-s0cattack", "coûte 20 papoules, enlève 100 points à la cible")]
        [RequireSignUp()]
        public async Task BigSocattack([Autocomplete(typeof(UsernameAutocompleteHandler))] string username)
        {
            // Est-ce que le joueur existe ?
            UserEntity targetUser = _db.Users.SingleOrDefault(e => e.Pseudo == username);
            if (targetUser == null || targetUser.Freeze)
            {
                await RespondAsync($"L'utilisateur {username} n'existe pas ou s'est désactivé.", ephemeral: true);
                return;
            }


            // Récupérer l'attaquant ?
            UserEntity user = _db.Users.SingleOrDefault(e => e.Id_Discord == Context.User.Id);
            int cost = 20;
            ulong scoreLoss = 100;

            if(user.Money < cost)
            {

                await RespondAsync($"Vous n'avez pas assez de papoules. Il en faut {cost}.", ephemeral: true);
                return;
            }

            if (targetUser.Score < scoreLoss)
            {
                await RespondAsync($"Votre cible doit avoir au moins {scoreLoss} à perdre.", ephemeral: true);
                return;
            }

            targetUser.Score -= scoreLoss;
            user.Money -= cost;
            _db.SaveChanges();

            EmbedBuilder embedBuilder = new EmbedBuilder()
            {
                Description = $"Vous avez bien utilisé la big-s0cattack sur {targetUser.Pseudo}"
            };

            await RespondAsync("", embed: embedBuilder.Build(), ephemeral: true);

            ITextChannel chan = (ITextChannel)await _discord.GetChannelAsync(_settings.GetValue<ulong>("chans:wallOfEpicness"));
            await chan.SendMessageAsync($"<@{user.Id_Discord}> a big-s0cattacké <@{targetUser.Id_Discord}> ! :sob:");
        }





        [SlashCommand("test-gif", "permet de tester si l'url de votre gif fonctionne")]
        [RequireSignUp()]
        public async Task TestGif(String url, bool onMessage = false)
        {
            EmbedBuilder embed_builder;
            if(onMessage)
            {
                embed_builder = new EmbedBuilder()
                {
                    Color = Color.Green,
                    Title = $"Test de votre gif",
                    Description = $"Si vous ne voyez pas votre gif – il charge indéfiniment par exemple – c'est certainement que Discord n'arrive pas à l'afficher dans un *embed* avec cette adresse-là. Veuillez essayer avec une autre adresse. Je vous conseille d'aller sur Giphy, de trouver un gif et d'appuyer d'abord sur le bouton \"Share\" puis sur le bouton \"Copy GIF Link\".\n{url}"
                };
            }
            else
            {
                embed_builder = new EmbedBuilder()
                {
                    Color = Color.Green,
                    Title = $"Test de votre gif",
                    Description = "Si vous ne voyez pas votre gif – il charge indéfiniment par exemple – c'est certainement que Discord n'arrive pas à l'afficher dans un *embed* avec cette adresse-là. Veuillez essayer avec une autre adresse. Je vous conseille d'aller sur Giphy, de trouver un gif et d'appuyer d'abord sur le bouton \"Share\" puis sur le bouton \"Copy GIF Link\".",
                    ImageUrl = url
                };
            }

            await RespondAsync("", embed: embed_builder.Build(), ephemeral: true);
        }




        [SlashCommand("play", "permet de jouer pour la première fois")]
        [RequireSignUp()]
        public async Task Play(Boolean ephemeral=true)
        {

            EmbedBuilder embedBuilder = new EmbedBuilder()
            {
                Title = "Comment jouer au Pierre-Feuille-Ciseaux ?",
                Description = "Tu veux jouer au PFC-Bot ? Rien de plus simple, pour cela, il suffit d'envoyer un message privé à <@698934530987262022> et à lui écrire la commande suivante en remplaçant \"[ton pseudo]\" par le pseudo que tu souhaites avoir sur le jeu :\n" +
                "```/signup [ton pseudo]```\n"
            };
            await RespondAsync("", embed:embedBuilder.Build(), ephemeral:ephemeral);
        }

        private FightEntity SearchFightOfComponentUtility(SocketMessageComponent component, bool fightFinished=false)
        {
            if(fightFinished)
            {
                return _db.Fights.SingleOrDefault(e =>
                    e.Choice_Defender.HasValue && e.Choice_Attacker.HasValue && (
                        e.Id_Message_Attacker == component.Message.Id &&
                        e.Attacker.Id_Discord == component.User.Id ||
                        e.Id_Message_Defender == component.Message.Id &&
                        e.Defender.Id_Discord == component.User.Id
                        )
                    );
            }
            else
            {
                return _db.Fights.SingleOrDefault(e =>
                    e.Id_Message_Attacker == component.Message.Id &&
                    e.Attacker.Id_Discord == component.User.Id &&
                    !e.Choice_Attacker.HasValue ||
                    e.Id_Message_Defender == component.Message.Id &&
                    e.Defender.Id_Discord == component.User.Id &&
                    !e.Choice_Defender.HasValue
                    );
            }
        }


        private bool FightNeedToBeResolvedUtility(FightEntity fight)
        {
            return fight.Choice_Attacker.HasValue && fight.Choice_Defender.HasValue;
        }


        /*
         * Return true if the attacker wins or if equality
         * Return false if the defender wins
         */
        private Boolean ResolveFightUtility(ref FightEntity fight)
        {
            switch (fight.Choice_Attacker)
            {
                case 'p':
                    if (fight.Choice_Defender == 'f')
                    {
                        fight.Winner = fight.Defender;
                        return false;
                    }
                    else if (fight.Choice_Defender == 'c')
                    {
                        fight.Winner = fight.Attacker;
                        return true;
                    }
                    break;
                case 'f':
                    if (fight.Choice_Defender == 'p')
                    {
                        fight.Winner = fight.Attacker;
                        return true;
                    }
                    else if (fight.Choice_Defender == 'c')
                    {
                        fight.Winner = fight.Defender;
                        return false;
                    }
                    break;
                case 'c':
                    if (fight.Choice_Defender == 'p')
                    {
                        fight.Winner = fight.Defender;
                        return false;
                    }
                    else if (fight.Choice_Defender == 'f')
                    {
                        fight.Winner = fight.Attacker;
                        return true;
                    }
                    break;
            }
            return true;
        }



        private async Task<IMessageChannel> getMessageChannelUtility(ulong idDiscordUser, ulong? idChannel, bool isDm)
        {
            IUser user_attacker = await _discord.GetUserAsync(idDiscordUser);
            IMessageChannel chan_attacker = null;
            if (isDm)
                chan_attacker = await user_attacker.CreateDMChannelAsync();
            else
                chan_attacker = (IMessageChannel)await _discord.GetChannelAsync((ulong)idChannel);
            return chan_attacker;
        }


        private async Task AddToEmbedDescription(string addedPart, ulong idMessage, ulong idDiscordUser)
        {
            IMessageChannel dmChannel = await getMessageChannelUtility(idDiscordUser, null, true);
            IMessage message = await dmChannel.GetMessageAsync(idMessage);
            String messageToSend = message.Embeds.ElementAt(0).Description;
            await dmChannel.ModifyMessageAsync(idMessage, x =>
            {
                var builder = new EmbedBuilder()
                {
                    //Optional color
                    Title = message.Embeds.ElementAt(0).Title,
                    Color = message.Embeds.ElementAt(0).Color,
                    Description = $"{message.Embeds.ElementAt(0).Description}\n{addedPart}"
                };
                x.Embed = builder.Build();
            });
        }


        private async Task NotifyUser(UserEntity user, IMessageChannel channel, Embed embed)
        {
            if(user.Notification && !user.Freeze)
                await channel.SendMessageAsync("", embed: embed, components: _removeBuilder.Build());
        }

        private async Task ModifyDMEmbedMessage(IMessageChannel messageChannel, ulong messageId, String addToDescription=null, ComponentBuilder? componentBuilder=null)
        {

            // Get the embed message
            IMessage message = await messageChannel.GetMessageAsync(messageId);
            String messageToSend = message.Embeds.ElementAt(0).Description;

            string? imageUrl = message.Embeds.ElementAt(0).Image != null ?
                message.Embeds.ElementAt(0).Image.Value.Url:
                null;


            // Finaly modify the message
            if (componentBuilder == null)
            {
                //IMessageChannel dmChannel = await getMessageChannelUtility(idDiscordUser, null, true);
                
                await messageChannel.ModifyMessageAsync(messageId, x =>
                {
                    var builder = new EmbedBuilder()
                    {
                        //Optional color
                        Title = message.Embeds.ElementAt(0).Title,
                        Color = message.Embeds.ElementAt(0).Color,
                        Description = $"{message.Embeds.ElementAt(0).Description}\n{addToDescription}",
                        ImageUrl = imageUrl
                    };
                    x.Embed = builder.Build();
                    
                });
            }
            else
            {
                await messageChannel.ModifyMessageAsync(messageId, x =>
                {
                    var builder = new EmbedBuilder()
                    {
                        //Optional color
                        Title = message.Embeds.ElementAt(0).Title,
                        Color = message.Embeds.ElementAt(0).Color,
                        Description = $"{message.Embeds.ElementAt(0).Description}\n{addToDescription}",
                        ImageUrl = imageUrl
                    };
                    x.Embed = builder.Build();
                    x.Components = componentBuilder.Build();
                });
            }
        }




    }

}
