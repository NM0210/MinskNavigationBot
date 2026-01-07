using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MinskNavigationBot;

public class UpdateHandler : IUpdateHandler
{
    public async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        try
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
        catch (Exception ex)
        {
            Console.WriteLine("Error handling update: " + ex);
        }
    }

    public async Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Polling error from {source}: " + exception);
        await Task.CompletedTask;
    }
}

public static class BotInitializer
{
    public static Task Start()
    {
        var token = Environment.GetEnvironmentVariable("BOT_TOKEN");

        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("❌ ОШИБКА: BOT_TOKEN не найден!");
            Console.WriteLine();
            Console.WriteLine("Для запуска бота необходимо:");
            Console.WriteLine("1. Создать файл .env в папке MinskNavigationBot");
            Console.WriteLine("2. Добавить в него строку: BOT_TOKEN=ваш_токен_бота");
            Console.WriteLine();
            Console.WriteLine("Как получить токен:");
            Console.WriteLine("- Откройте Telegram и найдите @BotFather");
            Console.WriteLine("- Отправьте команду /newbot");
            Console.WriteLine("- Следуйте инструкциям для создания бота");
            Console.WriteLine("- Скопируйте полученный токен в файл .env");
            Console.WriteLine();
            throw new Exception("BOT_TOKEN is missing. Создайте файл .env с токеном бота.");
        }

        // Проверяем, что токен не является placeholder
        if (token.Contains("ваш_токен") || token.Contains("your_bot_token") || token.Trim().Length < 20)
        {
            Console.WriteLine("❌ ОШИБКА: Невалидный токен бота!");
            Console.WriteLine();
            Console.WriteLine($"Текущий токен: {token.Substring(0, Math.Min(20, token.Length))}...");
            Console.WriteLine();
            Console.WriteLine("Похоже, что в файле .env остался placeholder вместо реального токена.");
            Console.WriteLine("Пожалуйста:");
            Console.WriteLine("1. Откройте файл .env в корне проекта");
            Console.WriteLine("2. Замените 'ваш_токен_бота_здесь' на реальный токен от @BotFather");
            Console.WriteLine();
            Console.WriteLine("Как получить токен:");
            Console.WriteLine("- Откройте Telegram и найдите @BotFather");
            Console.WriteLine("- Отправьте команду /newbot");
            Console.WriteLine("- Следуйте инструкциям для создания бота");
            Console.WriteLine("- Скопируйте полученный токен в файл .env");
            Console.WriteLine();
            throw new Exception("BOT_TOKEN is invalid. Замените placeholder на реальный токен бота.");
        }

        try
        {
            Globals.Bot = new TelegramBotClient(token);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("token") || ex.Message.Contains("Token"))
        {
            Console.WriteLine("❌ ОШИБКА: Невалидный токен бота!");
            Console.WriteLine();
            Console.WriteLine($"Ошибка: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("Пожалуйста, проверьте:");
            Console.WriteLine("1. Токен скопирован полностью (без пробелов в начале/конце)");
            Console.WriteLine("2. В файле .env формат: BOT_TOKEN=токен (без кавычек)");
            Console.WriteLine("3. Токен получен от @BotFather и актуален");
            Console.WriteLine();
            Console.WriteLine("Как получить новый токен:");
            Console.WriteLine("- Откройте Telegram и найдите @BotFather");
            Console.WriteLine("- Отправьте команду /token");
            Console.WriteLine("- Выберите вашего бота");
            Console.WriteLine("- Скопируйте новый токен в файл .env");
            Console.WriteLine();
            throw new Exception($"BOT_TOKEN is invalid: {ex.Message}. Проверьте токен в файле .env.", ex);
        }
        
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };
        
        var updateHandler = new UpdateHandler();
        
        Globals.Bot.StartReceiving(
            updateHandler: updateHandler,
            receiverOptions: receiverOptions
        );
        
        return Task.CompletedTask;
    }
}
