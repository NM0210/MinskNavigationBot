using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Quartz;
using MinskNavigationBot.Data;
using Telegram.Bot;

namespace MinskNavigationBot;

public class ReminderJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            using var db = new BotDbContext();

            var reminders = await db.Reminders
                .Include(r => r.User)
                .Include(r => r.Place)
                .Where(r => !r.IsCompleted && r.ReminderDate <= DateTime.UtcNow)
                .ToListAsync();

            foreach (var reminder in reminders)
            {
                await Globals.Bot.SendMessage(
                    chatId: reminder.User.TelegramId,
                    text:
                        "🔔 Напоминание!\n\n" +
                        "📍 Пора посетить место:\n" +
                        reminder.Place.Name
                );

                reminder.IsCompleted = true;
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine("ReminderJob ERROR: " + ex);
        }
    }
}
