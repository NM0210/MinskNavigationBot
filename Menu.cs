using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.ReplyMarkups;

namespace MinskNavigationBot
{
    public static class Menu
    {
        public static InlineKeyboardMarkup MainMenu => new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("👤 Профиль", "seeProfile"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📍 Места", "seePlaces"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🎮 Квиз", "playGame"),
            }
        });

        public static InlineKeyboardMarkup ProfileMenu => new InlineKeyboardMarkup(new[]
       {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📍 Посещенные места", "seeVisitedPlaces"),
            },
             new[]
            {
                InlineKeyboardButton.WithCallbackData("🔔 Напоминания", "seeReminders"),
            }, 
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🏆 Достижения", "achievments"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🏠 Главное меню", "mainMenu"),
            }
        });
    }
}
