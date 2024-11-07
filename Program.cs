using Discord_Bot.commands;
using Discord_Bot.config;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace Discord_Bot
{
    internal class Program
    {
        public static DiscordClient Client { get; private set; }
        public static CommandsNextExtension Commands { get; private set; }

        public static async Task Main(string[] args)
        {
            // Настройка конфигурации JSON
            var jsonReader = new JSONReader();
            await jsonReader.ReadJson();

            // Создание конфигурации Discord
            var discordConfig = new DiscordConfiguration()
            {
                Token = jsonReader.Token,
                TokenType = TokenType.Bot,
                Intents = DiscordIntents.All,
                AutoReconnect = true
            };

            // Настройка контейнера зависимостей
            var services = new ServiceCollection();
            Client = new DiscordClient(discordConfig);
            services.AddSingleton(Client);
            services.AddSingleton(jsonReader);

            var serviceProvider = services.BuildServiceProvider();

            // Настройка событий
            Client.Ready += ClientOnReady;
            Client.GuildMemberAdded += async (sender, eventArgs) =>
                await OnGuildMemberAdded(sender, eventArgs, jsonReader.WelcomeChannelId);

            // Настройка команд
            var commandsConfig = new CommandsNextConfiguration()
            {
                StringPrefixes = new[] { jsonReader.Prefix }, // Префикс, загруженный из JSON
                EnableMentionPrefix = true,
                EnableDms = true,
                EnableDefaultHelp = false,
                Services = serviceProvider
            };

            Commands = Client.UseCommandsNext(commandsConfig);
            var profileCommand = new ProfileCommand(Client, jsonReader);
            Commands.RegisterCommands<ProfileCommand>();
            Client.MessageCreated += profileCommand.OnMessageCreated; // Подключаем обработчик сообщений

            await Client.ConnectAsync();
            await Task.Delay(-1);
        }

        private static Task ClientOnReady(DiscordClient sender, ReadyEventArgs args)
        {
            Console.WriteLine("Бот готов к работе!");
            return Task.CompletedTask;
        }

        private static async Task OnGuildMemberAdded(DiscordClient sender, GuildMemberAddEventArgs args, ulong welcomeChannelId)
        {
            var channel = await sender.GetChannelAsync(welcomeChannelId);
            if (channel != null)
            {
                var embed = WelcomeEmbedBuilder.CreateWelcomeEmbed(args.Member);
                await channel.SendMessageAsync(embed: embed);
            }
            else
            {
                Console.WriteLine("Приветственный канал не найден. Проверьте ID канала.");
            }
        }
    }
}
