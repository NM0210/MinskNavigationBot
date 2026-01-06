using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MinskNavigationBot;

public static class BotInitializer
{
    public static async Task Start()
    {
        var token = Environment.GetEnvironmentVariable("BOT_TOKEN");

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new Exception("BOT_TOKEN is missing");
        }

        Globals.Bot = new TelegramBotClient(token);

    }
    private static async Task HandleUpdateAsync(
        ITelegramBotClient bot,
        Update update,
        CancellationToken ct)
    {
        if (update.Message != null)
        {
            await BotHandlers.OnMessage(update.Message, update.Type);
        }
        else if (update.CallbackQuery != null)
        {
            await BotHandlers.OnUpdate(update);
        }
    }

    private static Task HandleErrorAsync(
        ITelegramBotClient bot,
        Exception exception,
        CancellationToken ct)
    {
        Console.WriteLine("Telegram error: " + exception);
        return Task.CompletedTask;
    }
}
