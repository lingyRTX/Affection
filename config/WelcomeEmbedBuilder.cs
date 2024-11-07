using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Discord_Bot.config
{
    public static class WelcomeEmbedBuilder
    {
        public static DiscordEmbed CreateWelcomeEmbed(DiscordMember member)
        {
            // Установка цвета в RGB
            var embedColor = new DiscordColor(135, 206, 250); // Светло-голубой цвет в формате RGB

            var embed = new DiscordEmbedBuilder
            {
                Title = "Добро пожаловать!",
                Description = $"{member.Mention}, рады приветствовать тебя на сервере {member.Guild.Name}!",
                Color = embedColor,  // Применяем цвет в RGB
                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail { Url = member.AvatarUrl }
            };

            embed.AddField("Участников на сервере", member.Guild.MemberCount.ToString(), true);
            embed.AddField("Дата регистрации", member.CreationTimestamp.ToString("dd/MM/yyyy"), true);
            embed.WithFooter("Присоединяйся к беседе и следи за правилами!");
            embed.WithTimestamp(DateTime.Now);

            return embed;
        }
    }
}

