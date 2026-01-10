using MinskNavigationBot.Data;
using MinskNavigationBot.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MinskNavigationBot;

public static class BotHandlers
{
    // ✅ FIX: async убран, чтобы не было предупреждения "нет await"
    public static Task OnError(Exception exception, HandleErrorSource source)
    {
        Console.WriteLine(exception);
        return Task.CompletedTask;
    }

    // =============================
    // ✅ ХРАНЕНИЕ "КОРНЕВОГО" МЕНЮ
    // =============================
    // В этом сообщении бот всегда показывает экраны через EditMessageText.
    private static readonly Dictionary<long, int> RootMenuMessageId = new(); // chatId -> messageId

    // Временные сообщения, которые надо удалять (квиз, результаты, подсказки и т.п.)
    private static readonly Dictionary<long, HashSet<int>> TempMessages = new(); // chatId -> set(messageId)

    private static void TrackTempMessage(long chatId, int messageId)
    {
        // Не добавляем корневое меню в temp
        if (RootMenuMessageId.TryGetValue(chatId, out var rootId) && rootId == messageId)
            return;

        if (!TempMessages.TryGetValue(chatId, out var set))
        {
            set = new HashSet<int>();
            TempMessages[chatId] = set;
        }

        set.Add(messageId);
    }

    private static async Task SafeDeleteMessage(long chatId, int messageId)
    {
        try
        {
            await Globals.Bot.DeleteMessage(chatId: chatId, messageId: messageId);
        }
        catch
        {
            // игнорируем: нет прав/старое сообщение/уже удалено и т.п.
        }
    }

    private static async Task CleanupTempMessages(long chatId)
    {
        if (!TempMessages.TryGetValue(chatId, out var set) || set.Count == 0)
            return;

        var toDelete = set.ToList();
        set.Clear();

        foreach (var mid in toDelete)
            await SafeDeleteMessage(chatId, mid);
    }

    private static async Task EnsureRootMenuExists(Chat chat)
    {
        if (RootMenuMessageId.ContainsKey(chat.Id))
            return;

        var menuText = "🏠 <b>Главное меню</b>\n\nВыберите действие:";

        var menuMsg = await Globals.Bot.SendMessage(
            chatId: chat.Id,
            text: menuText,
            parseMode: ParseMode.Html,
            replyMarkup: Menu.MainMenu
        );

        RootMenuMessageId[chat.Id] = menuMsg.MessageId;
    }

    private static async Task ShowMainMenuAndCleanup(Chat chat)
    {
        await EnsureRootMenuExists(chat);

        // Удаляем все временные сообщения
        await CleanupTempMessages(chat.Id);

        var menuText = "🏠 <b>Главное меню</b>\n\nВыберите действие:";

        if (!RootMenuMessageId.TryGetValue(chat.Id, out var rootId))
        {
            await EnsureRootMenuExists(chat);
            return;
        }

        try
        {
            await Globals.Bot.EditMessageText(
                chatId: chat.Id,
                messageId: rootId,
                text: menuText,
                parseMode: ParseMode.Html,
                replyMarkup: Menu.MainMenu
            );
        }
        catch
        {
            // ✅ Ключевой фикс: удаляем старый root, чтобы не было второго меню
            await SafeDeleteMessage(chat.Id, rootId);

            // Создаём новый root и сохраняем его id
            var newRoot = await Globals.Bot.SendMessage(
                chatId: chat.Id,
                text: menuText,
                parseMode: ParseMode.Html,
                replyMarkup: Menu.MainMenu
            );

            RootMenuMessageId[chat.Id] = newRoot.MessageId;
        }
    }


    // Получение пути к папке Photos
    private static string GetPhotosPath()
    {
        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var assemblyDir = Path.GetDirectoryName(assemblyLocation);
        var photosPathNearExe = Path.Combine(assemblyDir ?? "", "Photos");

        if (Directory.Exists(photosPathNearExe))
            return photosPathNearExe;

        var projectRoot = assemblyDir;

        while (projectRoot != null && !File.Exists(Path.Combine(projectRoot, "MinskNavigationBot.csproj")))
        {
            var parent = Directory.GetParent(projectRoot);
            if (parent == null) break;
            projectRoot = parent.FullName;
        }

        if (projectRoot == null || !File.Exists(Path.Combine(projectRoot, "MinskNavigationBot.csproj")))
            projectRoot = Directory.GetCurrentDirectory();

        return Path.Combine(projectRoot, "Photos");
    }

    // =============================
    // Состояния
    // =============================
    private static readonly Dictionary<long, int> PendingReminderPlace = new();

    // ✅ ОТЗЫВЫ: ожидание текста отзыва
    private static readonly Dictionary<long, int> PendingReviewPlace = new();   // telegramUserId -> placeId
    private static readonly Dictionary<long, int> PendingReviewRating = new();  // telegramUserId -> rating

    // Состояние квиза для каждого пользователя
    private static readonly Dictionary<long, QuizState> QuizStates = new();

    private class QuizState
    {
        public List<Place> Questions { get; set; } = new();
        public List<Place> AllPlaces { get; set; } = new();
        public int CurrentQuestionIndex { get; set; }
        public int CorrectAnswers { get; set; }
        public List<int> SelectedPlaceIds { get; set; } = new();
    }

    // =============================
    // OnMessage
    // =============================
    public static async Task OnMessage(Message msg, UpdateType type)
    {
        if (msg.From != null)
        {
            await GetOrCreateUser(
                msg.From.Id,
                msg.From.Username,
                msg.From.FirstName,
                msg.From.LastName
            );
        }

        // ✅ Если пользователь пишет текст отзыва — сохраняем и удаляем ЕГО сообщение тоже
        if (msg.From != null
            && msg.Text != null
            && PendingReviewPlace.TryGetValue(msg.From.Id, out var reviewPlaceId)
            && PendingReviewRating.TryGetValue(msg.From.Id, out var reviewRating))
        {
            var text = msg.Text.Trim();
            if (text == "-") text = "";

            await SaveReview(msg.From.Id, reviewPlaceId, reviewRating, text);

            PendingReviewPlace.Remove(msg.From.Id);
            PendingReviewRating.Remove(msg.From.Id);

            // ✅ Удаляем сообщение пользователя с отзывом
            await SafeDeleteMessage(msg.Chat.Id, msg.MessageId);

            // ✅ Чистим всё временное и показываем главное меню
            await ShowMainMenuAndCleanup(msg.Chat);
            return;
        }

        if (msg.Text == "/start")
        {
            await EnsureRootMenuExists(msg.Chat);

            // если /start пришёл новым сообщением — можно тоже удалить его, чтобы не оставалось
            await SafeDeleteMessage(msg.Chat.Id, msg.MessageId);

            await ShowMainMenuAndCleanup(msg.Chat);
            return;
        }

        // Обработка ввода даты и времени для напоминания
        if (msg.Text != null && msg.Text.Contains(".") && msg.Text.Contains(":"))
        {
            if (Regex.IsMatch(msg.Text, @"\d{2}\.\d{2}\.\d{4}\s+\d{2}:\d{2}"))
                await ProcessReminderDateTime(msg.Chat, msg, msg.From!.Id);
        }
    }

    // =============================
    // OnUpdate (CallbackQuery)
    // =============================
    public static async Task OnUpdate(Update update)
    {
        if (update is not { CallbackQuery: { } query })
            return;

        await GetOrCreateUser(
            query.From.Id,
            query.From.Username,
            query.From.FirstName,
            query.From.LastName
        );

        if (query.Message == null)
        {
            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }

        await EnsureRootMenuExists(query.Message.Chat);

        // Сообщение, с которого пришёл callback (квиз/результат/обычный экран)
        // Если это не root — считаем временным (чтобы можно было удалять)
        TrackTempMessage(query.Message.Chat.Id, query.Message.Id);

        // =============================
        // ✅ Главное меню — всегда чистим временные сообщения
        // =============================
        if (query.Data == "mainMenu")
        {
            // если пользователь нажал "главное меню" в квизе — завершаем квиз и чистим
            if (QuizStates.ContainsKey(query.From.Id))
                QuizStates.Remove(query.From.Id);

            await ShowMainMenuAndCleanup(query.Message.Chat);
            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }

        // =============================
        // Профиль / места
        // =============================
        if (query.Data == "seeProfile")
        {
            var rootId = RootMenuMessageId[query.Message.Chat.Id];
            await Globals.Bot.EditMessageText(
                chatId: query.Message.Chat.Id,
                messageId: rootId,
                text: $"👤 <b>Профиль</b>\n\nДобро пожаловать, {query.From.FirstName}!",
                parseMode: ParseMode.Html,
                replyMarkup: Menu.ProfileMenu
            );
            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }

        if (query.Data == "seePlaces" || query.Data == "filter_reset")
        {
            await ShowFilterTypeMenu(query.Message.Chat, RootMenuMessageId[query.Message.Chat.Id]);
            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }

        // Выбор типа фильтра
        if (query.Data == "filter_type_all")
        {
            await ShowPlacesMap(query.Message.Chat, RootMenuMessageId[query.Message.Chat.Id], null, null, 0);
            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }
        if (query.Data == "filter_type_district")
        {
            await ShowDistrictFilterMenu(query.Message.Chat, RootMenuMessageId[query.Message.Chat.Id]);
            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }
        if (query.Data == "filter_type_category")
        {
            await ShowCategoryFilterMenu(query.Message.Chat, RootMenuMessageId[query.Message.Chat.Id]);
            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }

        // Выбор района
        if (query.Data != null && query.Data.StartsWith("filter_district_"))
        {
            var district = query.Data.Replace("filter_district_", "");
            await ShowPlacesMap(query.Message.Chat, RootMenuMessageId[query.Message.Chat.Id],
                district == "all" ? null : district, null, 0);

            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }

        // Выбор категории
        if (query.Data != null && query.Data.StartsWith("filter_category_"))
        {
            var category = query.Data.Replace("filter_category_", "");
            await ShowPlacesMap(query.Message.Chat, RootMenuMessageId[query.Message.Chat.Id],
                null, category == "all" ? null : category, 0);

            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }

        // Пагинация
        if (query.Data != null && query.Data.StartsWith("places_page_"))
        {
            var parts = query.Data.Replace("places_page_", "").Split('_');
            if (parts.Length >= 3 && int.TryParse(parts[0], out int page))
            {
                var district = parts[1] == "null" ? null : parts[1];
                var category = parts[2] == "null" ? null : parts[2];
                await ShowPlacesMap(query.Message.Chat, RootMenuMessageId[query.Message.Chat.Id], district, category, page);
            }

            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }

        // =============================
        // Детали места
        // =============================
        if (query.Data?.StartsWith("place_") == true)
        {
            var placeIdStr = query.Data.Replace("place_", "").Replace("_first", "");
            if (!int.TryParse(placeIdStr, out int placeId))
            {
                await Globals.Bot.AnswerCallbackQuery(query.Id);
                return;
            }

            var isFirstTime = query.Data.EndsWith("_first");

            await ShowPlaceDetails(
                query.Message.Chat,
                RootMenuMessageId[query.Message.Chat.Id],
                placeId,
                query.From.Id,
                isFirstTime
            );

            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }

        // =============================
        // ✅ Показ отзывов по кнопке
        // =============================
        if (query.Data != null && query.Data.StartsWith("reviews_"))
        {
            var placeIdStr = query.Data.Replace("reviews_", "");
            if (int.TryParse(placeIdStr, out int placeId))
            {
                await ShowPlaceReviews(query.Message.Chat, RootMenuMessageId[query.Message.Chat.Id], placeId, query.From.Id);
            }

            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }

        // =============================
        // Напоминания
        // =============================
        if (query.Data != null && query.Data.StartsWith("reminder_date_"))
        {
            var placeIdStr = query.Data.Replace("reminder_date_", "");
            if (int.TryParse(placeIdStr, out int placeId))
            {
                PendingReminderPlace[query.From.Id] = placeId;
                await AskReminderDateTime(query.Message.Chat, RootMenuMessageId[query.Message.Chat.Id], placeId, query.From.Id);
            }

            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }

        if (query.Data != null && query.Data.StartsWith("reminder_"))
        {
            var placeIdStr = query.Data.Replace("reminder_", "");
            if (int.TryParse(placeIdStr, out int placeId))
                await ShowReminderMenu(query.Message.Chat, RootMenuMessageId[query.Message.Chat.Id], placeId, query.From.Id);

            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }

        if (query.Data != null && query.Data.StartsWith("set_reminder_"))
        {
            var parts = query.Data.Replace("set_reminder_", "").Split('_');
            if (parts.Length >= 2 && int.TryParse(parts[0], out int placeId) && int.TryParse(parts[1], out int days))
                await SetReminder(query.Message.Chat, RootMenuMessageId[query.Message.Chat.Id], placeId, query.From.Id, days, query.Id);

            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }

        // =============================
        // Достижения
        // =============================
        if (query.Data == "achievments")
        {
            await ShowAchievements(query.Message.Chat, RootMenuMessageId[query.Message.Chat.Id], query.From.Id);
            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }

        // =============================
        // Посещённые / напоминания список
        // =============================
        if (query.Data == "seeVisitedPlaces")
        {
            await ShowVisitedPlaces(query.Message.Chat, RootMenuMessageId[query.Message.Chat.Id], query.From.Id);
            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }

        if (query.Data == "seeReminders")
        {
            await ShowReminders(query.Message.Chat, RootMenuMessageId[query.Message.Chat.Id], query.From.Id);
            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }

        // =============================
        // ✅ Посещение + запуск отзыва
        // =============================
        if (query.Data != null && query.Data.StartsWith("visit_"))
        {
            var placeIdStr = query.Data.Replace("visit_", "");
            if (int.TryParse(placeIdStr, out int placeId))
                await MarkPlaceAsVisited(query.Message.Chat, RootMenuMessageId[query.Message.Chat.Id], placeId, query.From.Id, query.Id);

            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }

        // =============================
        // ✅ ОТЗЫВЫ: выбор оценки
        // =============================
        if (query.Data != null && query.Data.StartsWith("review_rate_"))
        {
            var parts = query.Data.Replace("review_rate_", "").Split('_');
            if (parts.Length == 2
                && int.TryParse(parts[0], out int placeId)
                && int.TryParse(parts[1], out int rating))
            {
                PendingReviewPlace[query.From.Id] = placeId;
                PendingReviewRating[query.From.Id] = rating;

                var msg = await Globals.Bot.SendMessage(
                    chatId: query.Message.Chat.Id,
                    text: $"✍️ Оценка: {rating}/5\nТеперь напишите текст отзыва одним сообщением.\n\nЕсли без текста — отправьте символ <b>-</b>",
                    parseMode: ParseMode.Html
                );

                TrackTempMessage(query.Message.Chat.Id, msg.MessageId);
            }

            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }

        // ✅ ОТЗЫВЫ: пропуск
        if (query.Data != null && query.Data.StartsWith("review_skip_"))
        {
            PendingReviewPlace.Remove(query.From.Id);
            PendingReviewRating.Remove(query.From.Id);

            // убираем подсказки/временные и возвращаем меню
            await ShowMainMenuAndCleanup(query.Message.Chat);

            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }

        // =============================
        // ✅ КВИЗ
        // =============================
        if (query.Data == "playGame")
        {
            // Перед стартом квиза чистим прошлые временные (если остались)
            await CleanupTempMessages(query.Message.Chat.Id);

            await StartQuiz(query.Message.Chat, query.From.Id);
            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }

        if (query.Data != null && query.Data.StartsWith("quiz_answer_"))
        {
            var parts = query.Data.Replace("quiz_answer_", "").Split('_');
            if (parts.Length >= 2 && int.TryParse(parts[0], out int questionIndex) && int.TryParse(parts[1], out int selectedPlaceId))
                await ProcessQuizAnswer(query.Message.Chat, query.From.Id, questionIndex, selectedPlaceId);

            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }

        if (query.Data != null && query.Data.StartsWith("quiz_next_"))
        {
            var questionIndexStr = query.Data.Replace("quiz_next_", "");
            if (int.TryParse(questionIndexStr, out int nextQuestionIndex))
            {
                // ✅ Удаляем сообщение результата (кнопка "следующий") — оно временное
                await SafeDeleteMessage(query.Message.Chat.Id, query.Message.Id);

                await ShowQuizQuestion(query.Message.Chat, query.From.Id, nextQuestionIndex);
            }

            await Globals.Bot.AnswerCallbackQuery(query.Id);
            return;
        }

        await Globals.Bot.AnswerCallbackQuery(query.Id);
    }

    // =========================================
    // Меню фильтров
    // =========================================
    private static async Task ShowFilterTypeMenu(Chat chat, int messageId)
    {
        var buttons = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("📍 Все места", "filter_type_all") },
            new[] { InlineKeyboardButton.WithCallbackData("🏘️ По району", "filter_type_district") },
            new[] { InlineKeyboardButton.WithCallbackData("🏷️ По категории", "filter_type_category") },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("← Назад", "seeProfile"),
                InlineKeyboardButton.WithCallbackData("🏠 Главное меню", "mainMenu")
            }
        });

        await Globals.Bot.EditMessageText(
            chatId: chat.Id,
            messageId: messageId,
            text: "🔍 <b>Выберите критерий фильтрации:</b>",
            parseMode: ParseMode.Html,
            replyMarkup: buttons
        );
    }

    private static async Task ShowDistrictFilterMenu(Chat chat, int messageId)
    {
        using var db = new BotDbContext();

        var districts = db.Places
            .Where(p => !string.IsNullOrEmpty(p.District))
            .Select(p => p.District!)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var buttons = new List<InlineKeyboardButton[]>();

        foreach (var district in districts)
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData(district, $"filter_district_{district}") });

        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("← Назад к фильтрам", "seePlaces") });

        await Globals.Bot.EditMessageText(
            chatId: chat.Id,
            messageId: messageId,
            text: "🏘️ <b>Выберите район:</b>",
            parseMode: ParseMode.Html,
            replyMarkup: new InlineKeyboardMarkup(buttons)
        );
    }

    private static async Task ShowCategoryFilterMenu(Chat chat, int messageId)
    {
        using var db = new BotDbContext();

        var categories = db.Places
            .Where(p => !string.IsNullOrEmpty(p.Category))
            .Select(p => p.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        var buttons = new List<InlineKeyboardButton[]>();

        foreach (var category in categories)
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData(category, $"filter_category_{category}") });

        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("← Назад к фильтрам", "seePlaces") });

        await Globals.Bot.EditMessageText(
            chatId: chat.Id,
            messageId: messageId,
            text: "🏷️ <b>Выберите категорию:</b>",
            parseMode: ParseMode.Html,
            replyMarkup: new InlineKeyboardMarkup(buttons)
        );
    }

    // =========================================
    // Список мест
    // =========================================
    private static async Task ShowPlacesMap(Chat chat, int messageId, string? selectedDistrict, string? selectedCategory, int page)
    {
        const int placesPerPage = 8;

        using var db = new BotDbContext();

        var placesQuery = db.Places.AsQueryable();

        if (!string.IsNullOrEmpty(selectedDistrict))
            placesQuery = placesQuery.Where(p => p.District == selectedDistrict);

        if (!string.IsNullOrEmpty(selectedCategory))
            placesQuery = placesQuery.Where(p => p.Category == selectedCategory);

        var allPlaces = placesQuery.ToList();

        if (allPlaces.Count == 0)
        {
            var backButton = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("← Назад к фильтрам", "seePlaces") }
            });

            await Globals.Bot.EditMessageText(
                chatId: chat.Id,
                messageId: messageId,
                text: "❌ Места не найдены по выбранным фильтрам.",
                replyMarkup: backButton
            );
            return;
        }

        var totalPages = (int)Math.Ceiling(allPlaces.Count / (double)placesPerPage);
        if (page < 0) page = 0;
        if (page >= totalPages) page = totalPages - 1;

        var places = allPlaces.Skip(page * placesPerPage).Take(placesPerPage).ToList();

        var header = new StringBuilder();
        if (!string.IsNullOrEmpty(selectedDistrict))
            header.AppendLine($"🏘️ <b>Район:</b> {selectedDistrict}");
        if (!string.IsNullOrEmpty(selectedCategory))
            header.AppendLine($"🏷️ <b>Категория:</b> {selectedCategory}");

        if (header.Length > 0) header.AppendLine();

        header.AppendLine($"📍 <b>Найдено мест: {allPlaces.Count}</b>");
        header.AppendLine($"📄 <b>Страница {page + 1} из {totalPages}</b>");

        var buttons = new List<InlineKeyboardButton[]>();
        foreach (var place in places)
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData($"📍 {place.Name}", $"place_{place.Id}_first") });

        var navButtons = new List<InlineKeyboardButton>();

        if (page > 0)
        {
            var districtParam = selectedDistrict ?? "null";
            var categoryParam = selectedCategory ?? "null";
            navButtons.Add(InlineKeyboardButton.WithCallbackData("◀ Назад", $"places_page_{page - 1}_{districtParam}_{categoryParam}"));
        }

        if (page < totalPages - 1)
        {
            var districtParam = selectedDistrict ?? "null";
            var categoryParam = selectedCategory ?? "null";
            navButtons.Add(InlineKeyboardButton.WithCallbackData("Вперед ▶", $"places_page_{page + 1}_{districtParam}_{categoryParam}"));
        }

        if (navButtons.Count > 0)
            buttons.Add(navButtons.ToArray());

        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("🔍 Изменить фильтр", "seePlaces") });
        buttons.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("← Назад", "seeProfile"),
            InlineKeyboardButton.WithCallbackData("🏠 Главное меню", "mainMenu")
        });

        await Globals.Bot.EditMessageText(
            chatId: chat.Id,
            messageId: messageId,
            text: header.ToString(),
            parseMode: ParseMode.Html,
            replyMarkup: new InlineKeyboardMarkup(buttons)
        );
    }

    // =========================================
    // Пользователь
    // =========================================
    private static async Task<Models.User> GetOrCreateUser(long telegramId, string? username, string? firstName, string? lastName)
    {
        await using var db = new BotDbContext();

        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramId == telegramId);

        if (user == null)
        {
            user = new Models.User
            {
                TelegramId = telegramId,
                Username = username,
                FirstName = firstName,
                LastName = lastName,
                RegisteredAt = DateTime.UtcNow
            };

            await db.Users.AddAsync(user);
        }
        else
        {
            user.Username = username;
            user.FirstName = firstName;
            user.LastName = lastName;
        }

        await db.SaveChangesAsync();
        return user;
    }

    // =========================================
    // Детали места + рейтинг сразу + кнопка "Отзывы"
    // =========================================
    private static async Task ShowPlaceDetails(Chat chat, int messageId, int placeId, long userId, bool sendLocation = false)
    {
        using var db = new BotDbContext();

        var place = db.Places.FirstOrDefault(p => p.Id == placeId);
        if (place == null)
        {
            await Globals.Bot.EditMessageText(chatId: chat.Id, messageId: messageId, text: "❌ Место не найдено.");
            return;
        }

        var user = await GetOrCreateUser(userId, null, null, null);

        var isVisited = db.UserVisits.Any(v => v.UserId == user.Id && v.PlaceId == placeId);
        var hasReminder = db.Reminders.Any(r => r.UserId == user.Id && r.PlaceId == placeId && !r.IsCompleted && r.ReminderDate >= DateTime.UtcNow);

        // ✅ рейтинг сразу
        var reviewsQuery = db.Reviews.Where(r => r.PlaceId == placeId);
        var reviewsCount = reviewsQuery.Count();
        double? avgRating = reviewsCount > 0 ? reviewsQuery.Average(r => (double)r.Rating) : null;

        var placeInfo = new StringBuilder();
        placeInfo.AppendLine($"📍 <b>{place.Name}</b>\n");

        if (avgRating != null)
            placeInfo.AppendLine($"⭐ <b>Рейтинг:</b> {avgRating:0.0}/5 ({reviewsCount} отзывов)\n");
        else
            placeInfo.AppendLine($"⭐ <b>Рейтинг:</b> нет оценок\n");

        if (!string.IsNullOrEmpty(place.Description))
            placeInfo.AppendLine($"📝 {place.Description}\n");

        if (!string.IsNullOrEmpty(place.Address))
            placeInfo.AppendLine($"📍 Адрес: {place.Address}");

        if (!string.IsNullOrEmpty(place.District))
            placeInfo.AppendLine($"🏘️ Район: {place.District}");

        if (!string.IsNullOrEmpty(place.Category))
            placeInfo.AppendLine($"🏷️ Категория: {place.Category}");

        if (isVisited)
            placeInfo.AppendLine($"\n✅ <b>Вы уже посещали это место</b>");

        if (hasReminder)
        {
            var reminder = db.Reminders.FirstOrDefault(r => r.UserId == user.Id && r.PlaceId == placeId && !r.IsCompleted);
            if (reminder != null)
                placeInfo.AppendLine($"\n🔔 <b>Напоминание установлено на:</b> {reminder.ReminderDate:dd.MM.yyyy HH:mm}");
        }

        var buttons = new List<InlineKeyboardButton[]>();

        // ✅ кнопка отзывов (показ)
        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("💬 Посмотреть отзывы", $"reviews_{placeId}") });

        if (!isVisited)
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("✅ Отметить как посещенное", $"visit_{placeId}") });

        if (!hasReminder)
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("🔔 Установить напоминание", $"reminder_{placeId}") });

        buttons.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("← Назад к списку", "seePlaces"),
            InlineKeyboardButton.WithCallbackData("🏠 Главное меню", "mainMenu")
        });

        await Globals.Bot.EditMessageText(
            chatId: chat.Id,
            messageId: messageId,
            text: placeInfo.ToString(),
            parseMode: ParseMode.Html,
            replyMarkup: new InlineKeyboardMarkup(buttons)
        );

        if (sendLocation)
        {
            try
            {
                var locMsg = await Globals.Bot.SendLocation(chatId: chat.Id, latitude: (float)place.Latitude, longitude: (float)place.Longitude);
                TrackTempMessage(chat.Id, locMsg.MessageId);
            }
            catch { }
        }
    }

    private static async Task ShowPlaceReviews(Chat chat, int messageId, int placeId, long userId)
    {
        using var db = new BotDbContext();

        var place = db.Places.FirstOrDefault(p => p.Id == placeId);
        if (place == null)
        {
            await Globals.Bot.EditMessageText(chatId: chat.Id, messageId: messageId, text: "❌ Место не найдено.");
            return;
        }

        var reviews = db.Reviews
            .Where(r => r.PlaceId == placeId)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .Take(10)
            .ToList();

        var totalCount = db.Reviews.Count(r => r.PlaceId == placeId);
        var avg = totalCount > 0 ? db.Reviews.Where(r => r.PlaceId == placeId).Average(r => (double)r.Rating) : 0;

        var sb = new StringBuilder();
        sb.AppendLine($"💬 <b>Отзывы: {place.Name}</b>\n");
        if (totalCount > 0)
            sb.AppendLine($"⭐ <b>Рейтинг:</b> {avg:0.0}/5 (всего {totalCount})\n");
        else
            sb.AppendLine("⭐ <b>Рейтинг:</b> нет оценок\n");

        if (reviews.Count == 0)
        {
            sb.AppendLine("Пока нет отзывов.");
        }
        else
        {
            foreach (var r in reviews)
            {
                var name = r.User?.FirstName ?? r.User?.Username ?? "Пользователь";
                sb.AppendLine($"⭐ {r.Rating}/5 — <b>{name}</b> ({r.CreatedAt.ToLocalTime():dd.MM.yyyy HH:mm})");
                if (!string.IsNullOrWhiteSpace(r.Text))
                    sb.AppendLine($"📝 {r.Text}");
                sb.AppendLine();
            }
        }

        var kb = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("← Назад", $"place_{placeId}"),
                InlineKeyboardButton.WithCallbackData("🏠 Главное меню", "mainMenu")
            }
        });

        await Globals.Bot.EditMessageText(
            chatId: chat.Id,
            messageId: messageId,
            text: sb.ToString(),
            parseMode: ParseMode.Html,
            replyMarkup: kb
        );
    }

    // =========================================
    // ✅ ОТЗЫВЫ: спросить оценку после посещения
    // =========================================
    private static async Task AskReviewRating(Chat chat, long userTelegramId, int placeId)
    {
        PendingReviewPlace[userTelegramId] = placeId;

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⭐ 1", $"review_rate_{placeId}_1"),
                InlineKeyboardButton.WithCallbackData("⭐ 2", $"review_rate_{placeId}_2"),
                InlineKeyboardButton.WithCallbackData("⭐ 3", $"review_rate_{placeId}_3"),
                InlineKeyboardButton.WithCallbackData("⭐ 4", $"review_rate_{placeId}_4"),
                InlineKeyboardButton.WithCallbackData("⭐ 5", $"review_rate_{placeId}_5"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Пропустить", $"review_skip_{placeId}")
            }
        });

        var msg = await Globals.Bot.SendMessage(
            chatId: chat.Id,
            text: "📝 Хотите оставить отзыв? Сначала выберите оценку:",
            replyMarkup: keyboard
        );

        TrackTempMessage(chat.Id, msg.MessageId);
    }

    private static async Task SaveReview(long userTelegramId, int placeId, int rating, string? text)
    {
        await using var db = new BotDbContext();
        var user = await GetOrCreateUser(userTelegramId, null, null, null);

        db.Reviews.Add(new Review
        {
            UserId = user.Id,
            PlaceId = placeId,
            Rating = Math.Clamp(rating, 1, 5),
            Text = string.IsNullOrWhiteSpace(text) ? null : text,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    // =========================================
    // Посещение
    // =========================================
    private static async Task MarkPlaceAsVisited(Chat chat, int messageId, int placeId, long userId, string callbackQueryId)
    {
        using var db = new BotDbContext();
        var user = await GetOrCreateUser(userId, null, null, null);

        if (db.UserVisits.Any(v => v.UserId == user.Id && v.PlaceId == placeId))
        {
            await Globals.Bot.AnswerCallbackQuery(callbackQueryId, "Это место уже отмечено как посещенное!");
            return;
        }

        db.UserVisits.Add(new UserVisit
        {
            UserId = user.Id,
            PlaceId = placeId,
            VisitedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        await Globals.Bot.AnswerCallbackQuery(callbackQueryId, "Место отмечено как посещенное! ✅");

        // Обновляем экран места (в root)
        await ShowPlaceDetails(chat, messageId, placeId, userId, false);

        // Предлагаем отзыв (сообщение временное — будет чиститься)
        await AskReviewRating(chat, userId, placeId);

        await CheckAchievements(userId);
    }

    // =========================================
    // Напоминания
    // =========================================
    private static async Task ShowReminderMenu(Chat chat, int messageId, int placeId, long userId)
    {
        var buttons = new List<InlineKeyboardButton[]>
        {
            new[] { InlineKeyboardButton.WithCallbackData("📅 Настроить дату и время", $"reminder_date_{placeId}") },
            new[] { InlineKeyboardButton.WithCallbackData("📅 Через 1 день", $"set_reminder_{placeId}_1") },
            new[] { InlineKeyboardButton.WithCallbackData("📅 Через 3 дня", $"set_reminder_{placeId}_3") },
            new[] { InlineKeyboardButton.WithCallbackData("📅 Через 7 дней", $"set_reminder_{placeId}_7") },
            new[] { InlineKeyboardButton.WithCallbackData("📅 Через 14 дней", $"set_reminder_{placeId}_14") },
            new[] { InlineKeyboardButton.WithCallbackData("📅 Через 30 дней", $"set_reminder_{placeId}_30") },
            new[] { InlineKeyboardButton.WithCallbackData("← Назад", $"place_{placeId}") }
        };

        await Globals.Bot.EditMessageText(
            chatId: chat.Id,
            messageId: messageId,
            text: "🔔 <b>Выберите, когда напомнить о посещении:</b>\n\n" +
                  "Или настройте свою дату и время в формате: ДД.ММ.ГГГГ ЧЧ:ММ\n" +
                  "Например: 25.12.2024 14:30",
            parseMode: ParseMode.Html,
            replyMarkup: new InlineKeyboardMarkup(buttons)
        );
    }

    private static async Task AskReminderDateTime(Chat chat, int messageId, int placeId, long userId)
    {
        var backButton = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("← Назад", $"reminder_{placeId}") }
        });

        await Globals.Bot.EditMessageText(
            chatId: chat.Id,
            messageId: messageId,
            text: "📅 <b>Введите дату и время напоминания:</b>\n\n" +
                  "Формат: <b>ДД.ММ.ГГГГ ЧЧ:ММ</b>\n" +
                  "Например: <b>25.12.2024 14:30</b>\n\n" +
                  "Отправьте сообщение с датой и временем.",
            parseMode: ParseMode.Html,
            replyMarkup: backButton
        );
    }

    private static async Task SetReminder(Chat chat, int messageId, int placeId, long userId, int days, string callbackQueryId)
    {
        using var db = new BotDbContext();
        var user = await GetOrCreateUser(userId, null, null, null);
        var place = db.Places.FirstOrDefault(p => p.Id == placeId);

        if (place == null)
        {
            await Globals.Bot.EditMessageText(chatId: chat.Id, messageId: messageId, text: "❌ Место не найдено.");
            return;
        }

        var oldReminders = db.Reminders.Where(r => r.UserId == user.Id && r.PlaceId == placeId && !r.IsCompleted);
        db.Reminders.RemoveRange(oldReminders);

        var reminder = new Reminder
        {
            UserId = user.Id,
            PlaceId = placeId,
            ReminderDate = DateTime.UtcNow.AddDays(days),
            IsCompleted = false
        };

        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        await Globals.Bot.AnswerCallbackQuery(callbackQueryId, $"Напоминание установлено! 🔔");

        await ShowPlaceDetails(chat, messageId, placeId, userId);
    }

    private static async Task ProcessReminderDateTime(Chat chat, Message message, long userId)
    {
        if (!PendingReminderPlace.TryGetValue(userId, out var placeId))
        {
            var m = await Globals.Bot.SendMessage(chatId: chat.Id, text: "❌ Не удалось определить место для напоминания.");
            TrackTempMessage(chat.Id, m.MessageId);
            return;
        }

        try
        {
            var parts = message.Text!.Split(' ');
            var dateParts = parts[0].Split('.');
            var timeParts = parts[1].Split(':');

            var reminderDate = new DateTime(
                int.Parse(dateParts[2]),
                int.Parse(dateParts[1]),
                int.Parse(dateParts[0]),
                int.Parse(timeParts[0]),
                int.Parse(timeParts[1]),
                0
            );

            if (reminderDate < DateTime.Now)
            {
                var m = await Globals.Bot.SendMessage(chatId: chat.Id, text: "❌ Дата и время должны быть в будущем!");
                TrackTempMessage(chat.Id, m.MessageId);
                return;
            }

            using var db = new BotDbContext();
            var user = await GetOrCreateUser(userId, null, null, null);

            var oldReminders = db.Reminders.Where(r => r.UserId == user.Id && r.PlaceId == placeId && !r.IsCompleted);
            db.Reminders.RemoveRange(oldReminders);

            db.Reminders.Add(new Reminder
            {
                UserId = user.Id,
                PlaceId = placeId,
                ReminderDate = reminderDate.ToUniversalTime(),
                IsCompleted = false
            });

            await db.SaveChangesAsync();
            PendingReminderPlace.Remove(userId);

            // ✅ удалим сообщение пользователя с датой, чтобы не оставалось мусора
            await SafeDeleteMessage(chat.Id, message.MessageId);

            // ✅ вернемся в меню
            await ShowMainMenuAndCleanup(chat);
        }
        catch
        {
            var m = await Globals.Bot.SendMessage(chatId: chat.Id, text: "❌ Неверный формат даты. Используйте: ДД.ММ.ГГГГ ЧЧ:ММ");
            TrackTempMessage(chat.Id, m.MessageId);
        }
    }

    // =========================================
    // Посещенные / напоминания список
    // =========================================
    private static async Task ShowVisitedPlaces(Chat chat, int messageId, long userId)
    {
        using var db = new BotDbContext();
        var user = await GetOrCreateUser(userId, null, null, null);

        var visits = db.UserVisits
            .Where(v => v.UserId == user.Id)
            .Include(v => v.Place)
            .OrderByDescending(v => v.VisitedAt)
            .ToList();

        if (visits.Count == 0)
        {
            var backButton = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("🏠 Главное меню", "mainMenu") }
            });

            await Globals.Bot.EditMessageText(
                chatId: chat.Id,
                messageId: messageId,
                text: "📍 У вас пока нет посещенных мест.",
                replyMarkup: backButton
            );
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"📍 <b>Посещенные места ({visits.Count}):</b>\n");

        var buttons = new List<InlineKeyboardButton[]>();

        foreach (var visit in visits)
        {
            var visitDate = visit.VisitedAt.ToLocalTime();
            sb.AppendLine($"✅ <b>{visit.Place.Name}</b>");
            sb.AppendLine($"   📅 {visitDate:dd.MM.yyyy HH:mm}");
            if (!string.IsNullOrEmpty(visit.Place.District))
                sb.AppendLine($"   🏘️ {visit.Place.District}");
            sb.AppendLine();

            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData($"📍 {visit.Place.Name}", $"place_{visit.Place.Id}") });
        }

        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("🏠 Главное меню", "mainMenu") });

        await Globals.Bot.EditMessageText(
            chatId: chat.Id,
            messageId: messageId,
            text: sb.ToString(),
            parseMode: ParseMode.Html,
            replyMarkup: new InlineKeyboardMarkup(buttons)
        );
    }

    private static async Task ShowReminders(Chat chat, int messageId, long userId)
    {
        using var db = new BotDbContext();
        var user = await GetOrCreateUser(userId, null, null, null);

        var reminders = db.Reminders
            .Where(r => r.UserId == user.Id && !r.IsCompleted)
            .Include(r => r.Place)
            .OrderBy(r => r.ReminderDate)
            .ToList();

        if (reminders.Count == 0)
        {
            var backButton = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("🏠 Главное меню", "mainMenu") }
            });

            await Globals.Bot.EditMessageText(
                chatId: chat.Id,
                messageId: messageId,
                text: "🔔 У вас нет активных напоминаний.",
                replyMarkup: backButton
            );
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"🔔 <b>Активные напоминания ({reminders.Count}):</b>\n");

        var buttons = new List<InlineKeyboardButton[]>();

        foreach (var reminder in reminders)
        {
            var reminderDate = reminder.ReminderDate.ToLocalTime();
            sb.AppendLine($"🔔 <b>{reminder.Place.Name}</b>");
            sb.AppendLine($"   📅 {reminderDate:dd.MM.yyyy HH:mm}");
            if (!string.IsNullOrEmpty(reminder.Place.District))
                sb.AppendLine($"   🏘️ {reminder.Place.District}");
            sb.AppendLine();

            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData($"📍 {reminder.Place.Name}", $"place_{reminder.Place.Id}") });
        }

        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("🏠 Главное меню", "mainMenu") });

        await Globals.Bot.EditMessageText(
            chatId: chat.Id,
            messageId: messageId,
            text: sb.ToString(),
            parseMode: ParseMode.Html,
            replyMarkup: new InlineKeyboardMarkup(buttons)
        );
    }

    // =========================================
    // Достижения (как было)
    // =========================================
    private static async Task ShowAchievements(Chat chat, int messageId, long userId)
    {
        using var db = new BotDbContext();
        var user = await GetOrCreateUser(userId, null, null, null);

        await InitializeAchievements(db);

        var allAchievements = db.Achievements.ToList();
        var userAchievements = db.UserAchievements
            .Where(ua => ua.UserId == user.Id)
            .Include(ua => ua.Achievement)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("🏆 <b>Достижения:</b>\n");

        foreach (var achievement in allAchievements)
        {
            var isUnlocked = userAchievements.Any(ua => ua.AchievementId == achievement.Id);
            var icon = isUnlocked ? achievement.Icon : "🔒";
            var status = isUnlocked ? "✅" : "❌";

            sb.AppendLine($"{icon} {status} <b>{achievement.Name}</b>");
            sb.AppendLine($"   {achievement.Description}\n");
        }

        sb.AppendLine($"\n<b>Разблокировано: {userAchievements.Count} из {allAchievements.Count}</b>");

        var kb = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("🏠 Главное меню", "mainMenu") }
        });

        await Globals.Bot.EditMessageText(
            chatId: chat.Id,
            messageId: messageId,
            text: sb.ToString(),
            parseMode: ParseMode.Html,
            replyMarkup: kb
        );
    }

    private static async Task InitializeAchievements(BotDbContext db)
    {
        if (db.Achievements.Any())
            return;

        db.Achievements.AddRange(
            new Achievement { Name = "Первый шаг", Description = "Посетите первое место", Icon = "🎯", Type = AchievementType.FirstVisit, RequiredValue = 1 },
            new Achievement { Name = "Исследователь", Description = "Посетите 5 мест", Icon = "🗺️", Type = AchievementType.PlacesVisited, RequiredValue = 5 },
            new Achievement { Name = "Путешественник", Description = "Посетите 10 мест", Icon = "🌍", Type = AchievementType.PlacesVisited, RequiredValue = 10 },
            new Achievement { Name = "Гид Минска", Description = "Посетите 20 мест", Icon = "👑", Type = AchievementType.PlacesVisited, RequiredValue = 20 },
            new Achievement { Name = "Знаток категорий", Description = "Посетите 3 места одной категории", Icon = "🏷️", Type = AchievementType.CategoryExplorer, RequiredValue = 3 },
            new Achievement { Name = "Исследователь районов", Description = "Посетите 3 места одного района", Icon = "🏘️", Type = AchievementType.DistrictExplorer, RequiredValue = 3 },
            new Achievement { Name = "Организованный", Description = "Установите 5 напоминаний", Icon = "🔔", Type = AchievementType.ReminderMaster, RequiredValue = 5 },
            new Achievement { Name = "Новичок квиза", Description = "Правильно ответьте на 5 вопросов в квизе", Icon = "🎯", Type = AchievementType.QuizCompleted, RequiredValue = 5 },
            new Achievement { Name = "Знаток квиза", Description = "Правильно ответьте на 10 вопросов в квизе", Icon = "🎓", Type = AchievementType.QuizCompleted, RequiredValue = 10 },
            new Achievement { Name = "Мастер квиза", Description = "Правильно ответьте на 15 вопросов в квизе", Icon = "🏆", Type = AchievementType.QuizCompleted, RequiredValue = 15 },
            new Achievement { Name = "Эксперт Минска", Description = "Правильно ответьте на 50 вопросов во всех квизах", Icon = "👑", Type = AchievementType.QuizCompleted, RequiredValue = 50 }
        );

        await db.SaveChangesAsync();
    }

    private static async Task CheckAchievements(long userId)
    {
        using var db = new BotDbContext();
        var user = await GetOrCreateUser(userId, null, null, null);

        await InitializeAchievements(db);

        var visits = db.UserVisits.Where(v => v.UserId == user.Id).Include(v => v.Place).ToList();
        var reminders = db.Reminders.Where(r => r.UserId == user.Id).ToList();
        var userAchievements = db.UserAchievements.Where(ua => ua.UserId == user.Id).Select(ua => ua.AchievementId).ToList();

        var allAchievements = db.Achievements.ToList();

        foreach (var achievement in allAchievements)
        {
            if (userAchievements.Contains(achievement.Id))
                continue;

            bool unlocked = false;

            switch (achievement.Type)
            {
                case AchievementType.FirstVisit:
                    unlocked = visits.Count >= 1;
                    break;
                case AchievementType.PlacesVisited:
                    unlocked = visits.Count >= achievement.RequiredValue;
                    break;
                case AchievementType.CategoryExplorer:
                    unlocked = visits.GroupBy(v => v.Place.Category).Any(g => g.Count() >= achievement.RequiredValue);
                    break;
                case AchievementType.DistrictExplorer:
                    unlocked = visits.GroupBy(v => v.Place.District).Any(g => g.Count() >= achievement.RequiredValue);
                    break;
                case AchievementType.ReminderMaster:
                    unlocked = reminders.Count >= achievement.RequiredValue;
                    break;
            }

            if (!unlocked) continue;

            db.UserAchievements.Add(new UserAchievement
            {
                UserId = user.Id,
                AchievementId = achievement.Id,
                UnlockedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();

            try
            {
                var m = await Globals.Bot.SendMessage(
                    chatId: user.TelegramId,
                    text: $"🎉 <b>Достижение разблокировано!</b>\n\n{achievement.Icon} <b>{achievement.Name}</b>\n{achievement.Description}",
                    parseMode: ParseMode.Html
                );
                TrackTempMessage(user.TelegramId, m.MessageId);
            }
            catch { }
        }
    }

    // =========================================
    // Квиз
    // =========================================
    private static async Task StartQuiz(Chat chat, long userId)
    {
        using var db = new BotDbContext();

        var allPlaces = db.Places.Where(p => !string.IsNullOrEmpty(p.ImageUrl)).ToList();
        if (allPlaces.Count < 4)
        {
            var m = await Globals.Bot.SendMessage(chatId: chat.Id, text: "❌ Недостаточно мест с изображениями для квиза. Нужно минимум 4 места.", replyMarkup: Menu.MainMenu);
            TrackTempMessage(chat.Id, m.MessageId);
            return;
        }

        var random = new Random();
        var questionCount = Math.Min(random.Next(10, 16), allPlaces.Count);
        var questions = allPlaces.OrderBy(_ => random.Next()).Take(questionCount).ToList();

        QuizStates[userId] = new QuizState
        {
            Questions = questions,
            AllPlaces = allPlaces,
            CurrentQuestionIndex = 0,
            CorrectAnswers = 0,
            SelectedPlaceIds = new List<int>()
        };

        await ShowQuizQuestion(chat, userId, 0);
    }

    private static async Task ShowQuizQuestion(Chat chat, long userId, int questionIndex)
    {
        if (!QuizStates.TryGetValue(userId, out var quizState))
        {
            var m = await Globals.Bot.SendMessage(chatId: chat.Id, text: "❌ Квиз не найден. Начните заново.", replyMarkup: Menu.MainMenu);
            TrackTempMessage(chat.Id, m.MessageId);
            return;
        }

        if (questionIndex >= quizState.Questions.Count)
        {
            await FinishQuiz(chat, userId);
            return;
        }

        var questionPlace = quizState.Questions[questionIndex];
        var random = new Random();

        var wrongAnswers = quizState.AllPlaces
            .Where(p => p.Id != questionPlace.Id)
            .OrderBy(_ => random.Next())
            .Take(3)
            .ToList();

        var answers = new List<Place> { questionPlace };
        answers.AddRange(wrongAnswers);
        answers = answers.OrderBy(_ => random.Next()).ToList();

        var questionText = $"❓ <b>Вопрос {questionIndex + 1} из {quizState.Questions.Count}</b>\n\nКак называется это место?";

        var buttons = new List<InlineKeyboardButton[]>();
        foreach (var answer in answers)
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData($"📍 {answer.Name}", $"quiz_answer_{questionIndex}_{answer.Id}") });

        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("🏠 Главное меню", "mainMenu") });

        var keyboard = new InlineKeyboardMarkup(buttons);

        // ✅ Вопросы квиза — временные сообщения, всегда TrackTemp
        try
        {
            if (!string.IsNullOrEmpty(questionPlace.ImageUrl))
            {
                var photosPath = GetPhotosPath();
                var photoPath = Path.Combine(photosPath, questionPlace.ImageUrl);

                if (File.Exists(photoPath))
                {
                    using var fileStream = File.OpenRead(photoPath);
                    var inputFile = new Telegram.Bot.Types.InputFileStream(fileStream, Path.GetFileName(photoPath));

                    var sent = await Globals.Bot.SendPhoto(
                        chatId: chat.Id,
                        photo: inputFile,
                        caption: questionText,
                        replyMarkup: keyboard,
                        parseMode: ParseMode.Html
                    );

                    TrackTempMessage(chat.Id, sent.MessageId);
                    return;
                }
            }

            var fallback = await Globals.Bot.SendMessage(chatId: chat.Id, text: questionText, parseMode: ParseMode.Html, replyMarkup: keyboard);
            TrackTempMessage(chat.Id, fallback.MessageId);
        }
        catch
        {
            var fallback = await Globals.Bot.SendMessage(chatId: chat.Id, text: questionText, parseMode: ParseMode.Html, replyMarkup: keyboard);
            TrackTempMessage(chat.Id, fallback.MessageId);
        }
    }

    private static async Task ProcessQuizAnswer(Chat chat, long userId, int questionIndex, int selectedPlaceId)
    {
        if (!QuizStates.TryGetValue(userId, out var quizState))
        {
            var m = await Globals.Bot.SendMessage(chatId: chat.Id, text: "❌ Квиз не найден. Начните заново.", replyMarkup: Menu.MainMenu);
            TrackTempMessage(chat.Id, m.MessageId);
            return;
        }

        if (questionIndex >= quizState.Questions.Count)
        {
            await FinishQuiz(chat, userId);
            return;
        }

        var correctPlace = quizState.Questions[questionIndex];
        var isCorrect = correctPlace.Id == selectedPlaceId;

        if (isCorrect)
            quizState.CorrectAnswers++;

        quizState.SelectedPlaceIds.Add(selectedPlaceId);

        var resultText = isCorrect
            ? "✅ <b>Правильно!</b>"
            : $"❌ <b>Неправильно!</b>\n\nПравильный ответ: <b>{correctPlace.Name}</b>";

        var nextButton = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("➡️ Следующий вопрос", $"quiz_next_{questionIndex + 1}") },
            new[] { InlineKeyboardButton.WithCallbackData("🏠 Главное меню", "mainMenu") }
        });

        try
        {
            if (!string.IsNullOrEmpty(correctPlace.ImageUrl))
            {
                var photosPath = GetPhotosPath();
                var photoPath = Path.Combine(photosPath, correctPlace.ImageUrl);

                if (File.Exists(photoPath))
                {
                    using var fileStream = File.OpenRead(photoPath);
                    var inputFile = new Telegram.Bot.Types.InputFileStream(fileStream, Path.GetFileName(photoPath));

                    var sent = await Globals.Bot.SendPhoto(
                        chatId: chat.Id,
                        photo: inputFile,
                        caption: resultText,
                        replyMarkup: nextButton,
                        parseMode: ParseMode.Html
                    );

                    TrackTempMessage(chat.Id, sent.MessageId);
                    return;
                }
            }
        }
        catch { }

        var msg = await Globals.Bot.SendMessage(chatId: chat.Id, text: resultText, parseMode: ParseMode.Html, replyMarkup: nextButton);
        TrackTempMessage(chat.Id, msg.MessageId);
    }

    private static async Task FinishQuiz(Chat chat, long userId)
    {
        if (!QuizStates.TryGetValue(userId, out var quizState))
        {
            var m = await Globals.Bot.SendMessage(chatId: chat.Id, text: "❌ Квиз не найден.", replyMarkup: Menu.MainMenu);
            TrackTempMessage(chat.Id, m.MessageId);
            return;
        }

        using (var db = new BotDbContext())
        {
            var user = await GetOrCreateUser(userId, null, null, null);

            db.QuizResults.Add(new QuizResult
            {
                UserId = user.Id,
                TotalQuestions = quizState.Questions.Count,
                CorrectAnswers = quizState.CorrectAnswers,
                CompletedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        var percentage = (int)((double)quizState.CorrectAnswers / quizState.Questions.Count * 100);

        var resultText = $"🎉 <b>Квиз завершен!</b>\n\n" +
                         $"📊 <b>Результаты:</b>\n" +
                         $"✅ Правильных ответов: {quizState.CorrectAnswers} из {quizState.Questions.Count}\n" +
                         $"📈 Процент правильных: {percentage}%\n\n";

        resultText += percentage switch
        {
            100 => "🌟 Отличный результат! Вы знаток Минска!",
            >= 80 => "👏 Отличная работа!",
            >= 60 => "👍 Хороший результат!",
            _ => "💪 Продолжайте изучать Минск!"
        };

        // ✅ Отправляем результат как временное сообщение, затем пользователь жмёт "Главное меню" — и всё удалится
        var buttons = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("🏠 Главное меню", "mainMenu") }
        });

        var msg = await Globals.Bot.SendMessage(chatId: chat.Id, text: resultText, parseMode: ParseMode.Html, replyMarkup: buttons);
        TrackTempMessage(chat.Id, msg.MessageId);

        // Важное: состояние квиза убрать
        QuizStates.Remove(userId);
    }
}
