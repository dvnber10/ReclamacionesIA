using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebUI.Options
{
    public class TelegramOptions
    {
        public const string Seccion = "Telegram";

        public string BotToken { get; set; } = "";
        public string BotUsername { get; set; } = "";
    }
}