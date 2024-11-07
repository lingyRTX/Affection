using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Discord_Bot.config
{
    public class JSONReader  // Изменено на public для устранения ошибки CS0051
    {
        public string Token { get; private set; }
        public string Prefix { get; private set; }
        public ulong WelcomeChannelId { get; private set; }

        public async Task ReadJson()
        {
            using (StreamReader r = new StreamReader("config.json"))
            {
                var json = await r.ReadToEndAsync();
                dynamic config = JsonConvert.DeserializeObject(json);

                Token = config.token;
                Prefix = config.prefix;

                // Проверка и преобразование welcomeChannelId в ulong
                if (ulong.TryParse((string)config.welcomeChannelId?.ToString(), out ulong channelId))
                {
                    WelcomeChannelId = channelId;
                }
                else
                {
                    throw new Exception("ID канала приветствия (welcomeChannelId) не найден или некорректен в config.json.");
                }
            }
        }
    }
}
