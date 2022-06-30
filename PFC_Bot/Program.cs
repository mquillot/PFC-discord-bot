using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Discord;
using Discord.WebSocket;
using System.Threading;
using PFC_Bot.Services;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;

namespace PFC_Bot
{
	public class Program
	{
		private DiscordSocketClient _client;
		private InteractionService _commands;
		private IConfigurationRoot _settings;
		static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();


		public async Task MainAsync()
		{
			_settings = new ConfigurationBuilder()
				.SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
				.AddJsonFile("settings.json").Build();

			
			
			using (var services = ConfigureServices())
			{
				_client = services.GetRequiredService<DiscordSocketClient>();
				_commands = services.GetRequiredService<InteractionService>();

				// setup logging and the ready event
				_client.Log += LogAsync;
				_client.JoinedGuild += JoinedGuild;
				
				_commands.Log += LogAsync;
				_client.Ready += ReadyAsync;
				_client.ModalSubmitted += ModalAsync;

				var token = _settings.GetValue<String>("discord:token");


				// Starting bot
				await _client.LoginAsync(TokenType.Bot, token);
				await _client.StartAsync();
				


				await services.GetRequiredService<CommandHandler>().InitializeAsync();


				Console.WriteLine("BOT LANCÉ");
				await Task.Delay(Timeout.Infinite);
			}

		}

		private static Task JoinedGuild(SocketGuild socket)
        {
			return Task.CompletedTask;
		}

		private Task LogAsync(LogMessage msg)
        {
			Console.WriteLine(msg.ToString());
			return Task.CompletedTask;
        }


		private ServiceProvider ConfigureServices()
		{
			DiscordSocketConfig configSocket = new DiscordSocketConfig
			{
				GatewayIntents = GatewayIntents.All,
				AlwaysDownloadUsers = true
			};
			Console.Out.WriteLine($"Server={_settings.GetValue<String>("database:server")};Port={_settings.GetValue<String>("database:port")};Database={_settings.GetValue<String>("database:database")};User Id={_settings.GetValue<String>("database:userId")};Password={_settings.GetValue<String>("database:password")};");
			return new ServiceCollection()
				.AddSingleton<DiscordSocketClient>(new DiscordSocketClient(configSocket))
				.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
				.AddSingleton<CommandHandler>()
				.AddSingleton <IConfigurationRoot>(_settings)
				.AddDbContext<ApplicationDbContext>(
					options => options.UseNpgsql($"Server={_settings.GetValue<String>("database:server")};Port={_settings.GetValue<String>("database:port")};Database={_settings.GetValue<String>("database:database")};User Id={_settings.GetValue<String>("database:userId")};Password={_settings.GetValue<String>("database:password")};Include Error Detail=true")
					)
				.BuildServiceProvider();
		}

		private async Task ReadyAsync()
		{
			await _commands.RegisterCommandsGloballyAsync(true);
			//await _commands.RegisterCommandsToGuildAsync(401521860124344332);
			Console.WriteLine($"Connected as -> [{_client.CurrentUser}] :)");
		}

		private async Task ModalAsync(SocketModal modal)
        {
				// Get the values of components.
			List<SocketMessageComponentData> components =
				modal.Data.Components.ToList();
			string food = components
				.First(x => x.CustomId == "food_name").Value;
			string reason = components
				.First(x => x.CustomId == "food_reason").Value;

			// Build the message to send.
			string message = "hey @everyone; I just learned " +
				$"{modal.User.Mention}'s favorite food is " +
				$"{food} because {reason}.";

			// Specify the AllowedMentions so we don't actually ping everyone.
			AllowedMentions mentions = new AllowedMentions();
			mentions.AllowedTypes = AllowedMentionTypes.Users;

			// Respond to the modal.
			await modal.RespondAsync(message, allowedMentions: mentions);

		}
	}
}
