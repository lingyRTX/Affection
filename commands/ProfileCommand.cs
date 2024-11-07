using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Discord_Bot.config;

namespace Discord_Bot.commands
{
    public class ProfileCommand : BaseCommandModule
    {
        private readonly DiscordClient _client;
        private readonly JSONReader _config;

        public ProfileCommand(DiscordClient client, JSONReader config)
        {
            _client = client;
            _config = config;
        }

        private static Dictionary<ulong, (int level, int experience)> userProfiles = new Dictionary<ulong, (int, int)>();
        private static Dictionary<ulong, List<string>> userAchievements = new Dictionary<ulong, List<string>>();
        private static Dictionary<string, (int levelRequired, int messagesRequired)> achievements = new Dictionary<string, (int, int)>
        {
            {"Первый шаг", (1, 10)},
            {"Покоритель", (5, 50)},
            {"Мастер", (10, 100)},
            {"Легенда", (15, 200)},
            {"Тысячник", (10, 1000)},
            {"Активный в выходные", (0, 50)}
        };
        private static Dictionary<ulong, int> userMessages = new Dictionary<ulong, int>();
        private static Dictionary<ulong, DateTime> lastDailyClaim = new Dictionary<ulong, DateTime>();
        private static Dictionary<ulong, (string quest, bool completed)> dailyQuests = new Dictionary<ulong, (string, bool)>();

        public void AddExperience(ulong userId, int amount)
        {
            if (!userProfiles.ContainsKey(userId))
                userProfiles[userId] = (1, 0);

            var (level, experience) = userProfiles[userId];
            experience += amount;

            int requiredExperience = level * 100;
            while (experience >= requiredExperience)
            {
                experience -= requiredExperience;
                level++;
                requiredExperience = level * 100;
                _ = NotifyLevelUp(userId, level);
            }

            userProfiles[userId] = (level, experience);
            Console.WriteLine($"Опыт добавлен для {userId}: текущий уровень {level}, опыт {experience}");
            CheckAchievements(userId);
        }
        private void GiveLevelReward(ulong userId, int level)
        {
            Console.WriteLine($"Выдана награда за уровень {level} для пользователя {userId}");
            if (level >= 10) // Пример: уведомляем о достижении уровня 10 и выше
                _ = NotifyLevelUp(userId, level); // Добавление задачи в очередь без ожидания
        }

        private void CheckAchievements(ulong userId)
        {
            if (!userAchievements.ContainsKey(userId))
                userAchievements[userId] = new List<string>();

            var (level, _) = userProfiles[userId];
            var messages = userMessages.ContainsKey(userId) ? userMessages[userId] : 0;

            foreach (var achievement in achievements)
            {
                if (level >= achievement.Value.levelRequired && messages >= achievement.Value.messagesRequired &&
                    !userAchievements[userId].Contains(achievement.Key))
                {
                    userAchievements[userId].Add(achievement.Key);
                    Console.WriteLine($"Достижение '{achievement.Key}' выдано для {userId}.");
                    _ = NotifyAchievement(userId, achievement.Key);
                }
            }
        }

        [Command("profile")]
        public async Task Profile(CommandContext ctx)
        {
            var userId = ctx.User.Id;

            if (!userProfiles.ContainsKey(userId))
            {
                Console.WriteLine($"Создание нового профиля для пользователя: {userId}");
                userProfiles[userId] = (1, 0);
            }

            CheckAchievements(userId);

            var (level, experience) = userProfiles[userId];
            int requiredExperience = level * 100;
            int experienceToNextLevel = requiredExperience - experience;
            var achievements = userAchievements.ContainsKey(userId) ? string.Join(", ", userAchievements[userId]) : "Нет достижений";

            int messageCount = 0;
            userMessages.TryGetValue(userId, out messageCount);

            Console.WriteLine($"Профиль запрашивается для: {ctx.User.Username} ({userId}) - Уровень: {level}, Опыт: {experience}, Сообщения: {messageCount}");

            var profileEmbed = new DiscordEmbedBuilder
            {
                Title = $"Профиль пользователя: {ctx.User.Username}",
                Description = $"**Уровень**: {level}\n**Опыт**: {experience}/{requiredExperience}\n**До следующего уровня**: {experienceToNextLevel} опыта\n**Достижения**: {achievements}",
                Color = DiscordColor.Blurple
            };

            await ctx.Channel.SendMessageAsync(embed: profileEmbed).ConfigureAwait(false);
        }

        [Command("daily")]
        public async Task DailyReward(CommandContext ctx)
        {
            var userId = ctx.User.Id;
            DateTime lastClaimTime;
            lastDailyClaim.TryGetValue(userId, out lastClaimTime);

            if (lastClaimTime.Date == DateTime.UtcNow.Date)
            {
                await ctx.Channel.SendMessageAsync("Вы уже получили ежедневную награду сегодня!").ConfigureAwait(false);
                return;
            }

            AddExperience(userId, 50); // Начисляем 50 опыта за ежедневный вход
            lastDailyClaim[userId] = DateTime.UtcNow;

            await ctx.Channel.SendMessageAsync("Вы получили свою ежедневную награду: 50 опыта!").ConfigureAwait(false);
        }

        [Command("quest")]
        public async Task ShowQuest(CommandContext ctx)
        {
            var userId = ctx.User.Id;
            if (!dailyQuests.ContainsKey(userId))
                await AssignDailyQuest(userId);

            var (quest, completed) = dailyQuests[userId];
            string status = completed ? "Завершено" : "В процессе";
            await ctx.Channel.SendMessageAsync($"Ваш квест: {quest}. Статус: {status}").ConfigureAwait(false);
        }

        public Task AssignDailyQuest(ulong userId)
        {
            string quest = "Отправьте 20 сообщений за день";
            dailyQuests[userId] = (quest, false);
            return Task.CompletedTask;
        }

        public Task CompleteQuest(ulong userId)
        {
            if (!dailyQuests.ContainsKey(userId) || dailyQuests[userId].completed)
                return Task.CompletedTask;

            dailyQuests[userId] = (dailyQuests[userId].quest, true);
            AddExperience(userId, 100);
            Console.WriteLine($"Квест завершён для {userId}, начислено 100 опыта.");

            return Task.CompletedTask;
        }

        [Command("leaderboard")]
        public async Task Leaderboard(CommandContext ctx, string period = "all")
        {
            IEnumerable<KeyValuePair<ulong, (int level, int experience)>> topUsers;

            if (period == "monthly")
                topUsers = userProfiles.OrderByDescending(u => u.Value.experience).Take(5);
            else if (period == "weekly")
                topUsers = userProfiles.OrderByDescending(u => u.Value.experience).Take(5);
            else
                topUsers = userProfiles.OrderByDescending(u => u.Value.level).ThenByDescending(u => u.Value.experience).Take(5);

            string leaderboardText = string.Join("\n", topUsers.Select((u, i) => $"{i + 1}. <@{u.Key}> - Уровень {u.Value.level}, Опыт {u.Value.experience}"));
            var leaderboardEmbed = new DiscordEmbedBuilder
            {
                Title = "Топ пользователей по уровню",
                Description = leaderboardText,
                Color = DiscordColor.Green
            };

            await ctx.Channel.SendMessageAsync(embed: leaderboardEmbed).ConfigureAwait(false);
        }

        [Command("stats")]
        public async Task Stats(CommandContext ctx)
        {
            var userId = ctx.User.Id;
            var (level, experience) = userProfiles.ContainsKey(userId) ? userProfiles[userId] : (1, 0);
            int messageCount = userMessages.ContainsKey(userId) ? userMessages[userId] : 0;
            var achievementsList = userAchievements.ContainsKey(userId) ? string.Join(", ", userAchievements[userId]) : "Нет достижений";

            var statsEmbed = new DiscordEmbedBuilder
            {
                Title = $"Статистика пользователя: {ctx.User.Username}",
                Description = $"**Уровень**: {level}\n**Опыт**: {experience}\n**Сообщения**: {messageCount}\n**Достижения**: {achievementsList}",
                Color = DiscordColor.Blurple
            };

            await ctx.Channel.SendMessageAsync(embed: statsEmbed).ConfigureAwait(false);
        }

        public async Task NotifyLevelUp(ulong userId, int level)
        {
            var channel = await _client.GetChannelAsync(_config.WelcomeChannelId);
            await channel.SendMessageAsync($"Поздравляем <@{userId}> с достижением уровня {level}!");
        }

        public async Task NotifyAchievement(ulong userId, string achievement)
        {
            var channel = await _client.GetChannelAsync(_config.WelcomeChannelId);
            await channel.SendMessageAsync($"<@{userId}> получил достижение: {achievement}!");
        }

        public Task OnMessageCreated(DiscordClient client, MessageCreateEventArgs e)
        {
            if (e.Author.IsBot)
                return Task.CompletedTask;

            ulong userId = e.Author.Id;

            // Добавляем запись в словарь, если ее нет
            if (!userMessages.ContainsKey(userId))
                userMessages[userId] = 0;

            // Увеличиваем количество сообщений и добавляем опыт
            userMessages[userId]++;
            AddExperience(userId, 10); // Добавляем фиксированное количество опыта за сообщение
            Console.WriteLine($"Сообщение от {e.Author.Username} ({userId}), добавлено 10 опыта.");

            return Task.CompletedTask;
        }
    }
}
