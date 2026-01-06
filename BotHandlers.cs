using MinskNavigationBot.Data;
using MinskNavigationBot.Models;
using Microsoft.EntityFrameworkCore;
using System;
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
    // Обработчик всех обновлений
   public static async Task OnError(Exception exception, HandleErrorSource source)
    {
        Console.WriteLine(exception); // just dump the exception to the console
    }

    // method that handle messages received by the bot:
    private static readonly Dictionary<long, int> PendingReminderPlace
    = new();
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
        if (msg.Text == "/start")
        {
            await Globals.Bot.SendMessage(msg.Chat, "Welcome! Pick one direction",
                replyMarkup: Menu.MainMenu);
        }
        // Обработка ввода даты и времени для напоминания
        else if (msg.Text != null && msg.Text.Contains(".") && msg.Text.Contains(":"))
        {
            // Проверяем формат даты ДД.ММ.ГГГГ ЧЧ:ММ
            if (Regex.IsMatch(msg.Text, @"\d{2}\.\d{2}\.\d{4}\s+\d{2}:\d{2}"))
            {
                await ProcessReminderDateTime(msg.Chat, msg, msg.From.Id);
            }
        }
    }

    // method that handle other types of updates received by the bot:
    public static async Task OnUpdate(Update update)
    {
        if (update is { CallbackQuery: { } query })
        {
            await GetOrCreateUser(
                query.From.Id,
                query.From.Username,
                query.From.FirstName,
                query.From.LastName
            );

         
            if (query.Data == "seeProfile")
            {
                
                await Globals.Bot.EditMessageText(query.Message!.Chat, query.Message.Id,$"Welcome,{query.From.FirstName}!",replyMarkup:Menu.ProfileMenu);
            }
            //вывод из бд 
            if (query.Data == "seePlaces" || query.Data == "filter_reset")
            {
                await ShowFilterTypeMenu(query.Message!.Chat, query.Message.Id);
            }
            // Выбор типа фильтра
            else if (query.Data == "filter_type_all")
            {
                await ShowPlacesMap(query.Message!.Chat, query.Message.Id, null, null, 0);
            }
            else if (query.Data == "filter_type_district")
            {
                await ShowDistrictFilterMenu(query.Message!.Chat, query.Message.Id);
            }
            else if (query.Data == "filter_type_category")
            {
                await ShowCategoryFilterMenu(query.Message!.Chat, query.Message.Id);
            }
            // Выбор конкретного района
            else if (query.Data != null && query.Data.StartsWith("filter_district_"))
            {
                var district = query.Data.Replace("filter_district_", "");
                await ShowPlacesMap(query.Message!.Chat, query.Message.Id, 
                    district == "all" ? null : district, null, 0);
            }
            // Выбор конкретной категории
            else if (query.Data != null && query.Data.StartsWith("filter_category_"))
            {
                var category = query.Data.Replace("filter_category_", "");
                await ShowPlacesMap(query.Message!.Chat, query.Message.Id, 
                    null, category == "all" ? null : category, 0);
            }
            // Пагинация списка мест
            else if (query.Data != null && query.Data.StartsWith("places_page_"))
            {
                var parts = query.Data.Replace("places_page_", "").Split('_');
                if (parts.Length >= 3 && int.TryParse(parts[0], out int page))
                {
                    var district = parts[1] == "null" ? null : parts[1];
                    var category = parts[2] == "null" ? null : parts[2];
                    await ShowPlacesMap(query.Message!.Chat, query.Message.Id, district, category, page);
                }
            }
            if (query.Data?.StartsWith("place_") == true)
            {
                Console.WriteLine("зашел в обработчик");

                var placeIdStr = query.Data.Replace("place_", "").Replace("_first", "");

                if (!int.TryParse(placeIdStr, out int placeId))
                    return;

                var isFirstTime = query.Data.EndsWith("_first");

                try
                {
                    await Globals.Bot.AnswerCallbackQuery(query.Id);
                    Console.WriteLine("ответил");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Callback error: " + ex.Message);
                    return;
                }

                if (query.Message == null)
                {
                    Console.WriteLine("Message == null");
                    return;
                }

                try
                {
                    await ShowPlaceDetails(
                        query.Message.Chat,
                        query.Message.Id,
                        placeId,
                        query.From.Id,
                        isFirstTime
                    );
                    Console.WriteLine("функция");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ShowPlaceDetails error: " + ex);
                }
            }
            // Обработка ввода даты напоминания
            else if (query.Data != null && query.Data.StartsWith("reminder_date_"))
            {
                var placeIdStr = query.Data.Replace("reminder_date_", "");
                if (int.TryParse(placeIdStr, out int placeId))
                {
                    PendingReminderPlace[query.From.Id] = placeId;

                    await AskReminderDateTime(query.Message!.Chat, query.Message.Id, placeId, query.From.Id);
                    await Globals.Bot.AnswerCallbackQuery(query.Id);
                }
            }

            // Обработка достижений
            else if (query.Data == "achievments")
            {
                await ShowAchievements(query.Message!.Chat, query.Message.Id, query.From.Id);
                await Globals.Bot.AnswerCallbackQuery(query.Id);
            }
            // Отметка места как посещенного
            else if (query.Data != null && query.Data.StartsWith("visit_"))
            {
                var placeIdStr = query.Data.Replace("visit_", "");
                if (int.TryParse(placeIdStr, out int placeId))
                {
                    await MarkPlaceAsVisited(query.Message!.Chat, query.Message.Id, placeId, query.From.Id, query.Id);
                    await Globals.Bot.AnswerCallbackQuery(query.Id);
                }
            }
            // Установка напоминания
            else if (query.Data != null && query.Data.StartsWith("reminder_"))
            {
                var placeIdStr = query.Data.Replace("reminder_", "");
                if (int.TryParse(placeIdStr, out int placeId))
                {
                    await ShowReminderMenu(query.Message!.Chat, query.Message.Id, placeId, query.From.Id);
                    await Globals.Bot.AnswerCallbackQuery(query.Id);
                }
            }
            // Сохранение напоминания
            else if (query.Data != null && query.Data.StartsWith("set_reminder_"))
            {
                var parts = query.Data.Replace("set_reminder_", "").Split('_');
                if (parts.Length >= 2 && int.TryParse(parts[0], out int placeId) && int.TryParse(parts[1], out int days))
                {
                    await SetReminder(query.Message!.Chat, query.Message.Id, placeId, query.From.Id, days, query.Id);
                    await Globals.Bot.AnswerCallbackQuery(query.Id);
                }
            }
            // Просмотр посещенных мест
            else if (query.Data == "seeVisitedPlaces")
            {
                await ShowVisitedPlaces(query.Message!.Chat, query.Message.Id, query.From.Id);
                await Globals.Bot.AnswerCallbackQuery(query.Id);
            }
            // Просмотр напоминаний
            else if (query.Data == "seeReminders")
            {
                await ShowReminders(query.Message!.Chat, query.Message.Id, query.From.Id);
                await Globals.Bot.AnswerCallbackQuery(query.Id);
            }
            else
            {
                await Globals.Bot.AnswerCallbackQuery(query.Id);
            }
        }
    }

    // Показ меню выбора типа фильтра
    private static async Task ShowFilterTypeMenu(Chat chat, int messageId)
    {
        var buttons = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📍 Все места", "filter_type_all")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🏘️ По району", "filter_type_district")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🏷️ По категории", "filter_type_category")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("← Назад", "seeProfile")
            }
        });
        
        await Globals.Bot.EditMessageText(chat, messageId, 
            "🔍 <b>Выберите критерий фильтрации:</b>", 
            replyMarkup: buttons, parseMode: ParseMode.Html);
    }
    
    // Показ меню выбора района
    private static async Task ShowDistrictFilterMenu(Chat chat, int messageId)
    {
        using (var db = new BotDbContext())
        {
            var districts = db.Places.Where(p => !string.IsNullOrEmpty(p.District))
                .Select(p => p.District!)
                .Distinct()
                .OrderBy(d => d)
                .ToList();
            
            var buttons = new List<InlineKeyboardButton[]>();
            
            foreach (var district in districts)
            {
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData(district, $"filter_district_{district}")
                });
            }
            
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("← Назад к фильтрам", "seePlaces")
            });
            
            var keyboard = new InlineKeyboardMarkup(buttons);
            
            await Globals.Bot.EditMessageText(chat, messageId, 
                "🏘️ <b>Выберите район:</b>", 
                replyMarkup: keyboard, parseMode: ParseMode.Html);
        }
    }
    
    // Показ меню выбора категории
    private static async Task ShowCategoryFilterMenu(Chat chat, int messageId)
    {
        using (var db = new BotDbContext())
        {
            var categories = db.Places.Where(p => !string.IsNullOrEmpty(p.Category))
                .Select(p => p.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
            
            var buttons = new List<InlineKeyboardButton[]>();
            
            foreach (var category in categories)
            {
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData(category, $"filter_category_{category}")
                });
            }
            
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("← Назад к фильтрам", "seePlaces")
            });
            
            var keyboard = new InlineKeyboardMarkup(buttons);
            
            await Globals.Bot.EditMessageText(chat, messageId, 
                "🏷️ <b>Выберите категорию:</b>", 
                replyMarkup: keyboard, parseMode: ParseMode.Html);
        }
    }

    // Метод для отображения списка мест с пагинацией
    private static async Task ShowPlacesMap(Chat chat, int messageId, string? selectedDistrict, string? selectedCategory, int page)
    {
        const int placesPerPage = 8; // Количество мест на странице
        
        using (var db = new BotDbContext())
        {
            var placesQuery = db.Places.AsQueryable();
            
            // Применяем фильтры
            if (!string.IsNullOrEmpty(selectedDistrict))
                placesQuery = placesQuery.Where(p => p.District == selectedDistrict);
            
            if (!string.IsNullOrEmpty(selectedCategory))
                placesQuery = placesQuery.Where(p => p.Category == selectedCategory);
            
            var allPlaces = placesQuery.ToList();
            
            if (allPlaces.Count == 0)
            {
                var backButton = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("← Назад к фильтрам", "seePlaces")
                    }
                });
                await Globals.Bot.EditMessageText(chat, messageId, 
                    "❌ Места не найдены по выбранным фильтрам.", replyMarkup: backButton);
                return;
            }
            
            // Вычисляем пагинацию
            var totalPages = (int)Math.Ceiling(allPlaces.Count / (double)placesPerPage);
            if (page < 0) page = 0;
            if (page >= totalPages) page = totalPages - 1;
            
            var places = allPlaces.Skip(page * placesPerPage).Take(placesPerPage).ToList();
            
            // Формируем заголовок с информацией о фильтрах
            var header = new StringBuilder();
            if (!string.IsNullOrEmpty(selectedDistrict))
                header.AppendLine($"🏘️ <b>Район:</b> {selectedDistrict}");
            if (!string.IsNullOrEmpty(selectedCategory))
                header.AppendLine($"🏷️ <b>Категория:</b> {selectedCategory}");
            
            if (header.Length > 0)
                header.AppendLine();
            
            header.AppendLine($"📍 <b>Найдено мест: {allPlaces.Count}</b>");
            header.AppendLine($"📄 <b>Страница {page + 1} из {totalPages}</b>");
            
            // Создаём кнопки для мест текущей страницы
            var buttons = new List<InlineKeyboardButton[]>();
            foreach (var place in places)
            {
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData($"📍 {place.Name}", $"place_{place.Id}_first")
                });
            }
            
            // Добавляем кнопки навигации по страницам
            var navButtons = new List<InlineKeyboardButton>();
            
            if (page > 0)
            {
                var districtParam = selectedDistrict ?? "null";
                var categoryParam = selectedCategory ?? "null";
                navButtons.Add(InlineKeyboardButton.WithCallbackData("◀ Назад", 
                    $"places_page_{page - 1}_{districtParam}_{categoryParam}"));
            }
            
            if (page < totalPages - 1)
            {
                var districtParam = selectedDistrict ?? "null";
                var categoryParam = selectedCategory ?? "null";
                navButtons.Add(InlineKeyboardButton.WithCallbackData("Вперед ▶", 
                    $"places_page_{page + 1}_{districtParam}_{categoryParam}"));
            }
            
            if (navButtons.Count > 0)
                buttons.Add(navButtons.ToArray());
            
            // Добавляем кнопки управления
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("🔍 Изменить фильтр", "seePlaces")
            });
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("← Назад", "seeProfile"),
                InlineKeyboardButton.WithCallbackData("🏠 Главное меню", "seeProfile")
            });
            
            var keyboard = new InlineKeyboardMarkup(buttons);
            
            // Отправляем сообщение с кнопками
            try
            {
                await Globals.Bot.EditMessageText(chat, messageId, header.ToString(), 
                    replyMarkup: keyboard, parseMode: ParseMode.Html);
            }
            catch
            {
                // Если не удалось отредактировать, отправляем новое сообщение
                await Globals.Bot.SendMessage(chat, header.ToString(), 
                    replyMarkup: keyboard, parseMode: ParseMode.Html);
            }
        }
    }

    // Получение или создание пользователя
    private static async Task<Models.User> GetOrCreateUser(
        long telegramId,
        string? username,
        string? firstName,
        string? lastName)
    {
        await using var db = new BotDbContext();

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.TelegramId == telegramId);

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



    // Показ деталей места с картой
    private static async Task ShowPlaceDetails(Chat chat, int messageId, int placeId, long userId, bool sendLocation = false)
    {
        using (var db = new BotDbContext())
        {
            var place = db.Places.FirstOrDefault(p => p.Id == placeId);
            if (place == null)
            {
                await Globals.Bot.EditMessageText(chat, messageId, "❌ Место не найдено.");
                return;
            }
            
            // Получаем пользователя
            var user = await GetOrCreateUser(userId, null, null, null);
            
            // Проверяем, посещено ли место
            var isVisited = db.UserVisits.Any(v => v.UserId == user.Id && v.PlaceId == placeId);
            
            // Проверяем, есть ли активное напоминание
            var hasReminder = db.Reminders.Any(r => r.UserId == user.Id && r.PlaceId == placeId && !r.IsCompleted && r.ReminderDate >= DateTime.UtcNow);
            
            string placeInfo = $"📍 <b>{place.Name}</b>\n\n";
            
            if (!string.IsNullOrEmpty(place.Description))
                placeInfo += $"📝 {place.Description}\n\n";
            
            if (!string.IsNullOrEmpty(place.Address))
                placeInfo += $"📍 Адрес: {place.Address}\n";
            
            if (!string.IsNullOrEmpty(place.District))
                placeInfo += $"🏘️ Район: {place.District}\n";
            
            if (!string.IsNullOrEmpty(place.Category))
                placeInfo += $"🏷️ Категория: {place.Category}\n";
            
            if (isVisited)
                placeInfo += $"\n✅ <b>Вы уже посещали это место</b>\n";
            
            if (hasReminder)
            {
                var reminder = db.Reminders.FirstOrDefault(r => r.UserId == user.Id && r.PlaceId == placeId && !r.IsCompleted);
                if (reminder != null)
                    placeInfo += $"\n🔔 <b>Напоминание установлено на:</b> {reminder.ReminderDate:dd.MM.yyyy HH:mm}\n";
            }

            // Создаём кнопки
            var buttons = new List<InlineKeyboardButton[]>();
            
            if (!isVisited)
            {
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData("✅ Отметить как посещенное", $"visit_{placeId}")
                });
            }
            
            if (!hasReminder)
            {
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData("🔔 Установить напоминание", $"reminder_{placeId}")
                });
            }
            
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("← Назад к списку", "seePlaces"),
                InlineKeyboardButton.WithCallbackData("🏠 Главное меню", "seeProfile")
            });
            
            var keyboard = new InlineKeyboardMarkup(buttons);
            
            // Отправляем информацию о месте
            await Globals.Bot.EditMessageText(chat, messageId, placeInfo, 
                replyMarkup: keyboard, parseMode: ParseMode.Html);
            
            // Отправляем локацию только при первом выборе
            if (sendLocation)
            {
                try
                {
                    await Globals.Bot.SendLocation(chat, (float)place.Latitude, (float)place.Longitude);
                }
                catch { }
            }
        }
    }
    
    // Отметка места как посещенного
    private static async Task MarkPlaceAsVisited(Chat chat, int messageId, int placeId, long userId, string callbackQueryId)
    {
        using (var db = new BotDbContext())
        {
            var user = await GetOrCreateUser(userId, null, null, null);
            
            // Проверяем, не посещено ли уже
            if (db.UserVisits.Any(v => v.UserId == user.Id && v.PlaceId == placeId))
            {
                await Globals.Bot.AnswerCallbackQuery(callbackQueryId, "Это место уже отмечено как посещенное!");
                return;
            }
            
            // Добавляем посещение
            var visit = new UserVisit
            {
                UserId = user.Id,
                PlaceId = placeId,
                VisitedAt = DateTime.UtcNow
            };
            db.UserVisits.Add(visit);
            await db.SaveChangesAsync();
            
            await Globals.Bot.AnswerCallbackQuery(callbackQueryId, "Место отмечено как посещенное! ✅");
            
            // Обновляем информацию о месте (без отправки карты)
            await ShowPlaceDetails(chat, messageId, placeId, userId, false);
            
            // Проверяем достижения
            await CheckAchievements(userId);
        }
    }
    
    // Показ меню выбора даты напоминания
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
        
        var keyboard = new InlineKeyboardMarkup(buttons);
        
        await Globals.Bot.EditMessageText(chat, messageId, 
            "🔔 <b>Выберите, когда напомнить о посещении:</b>\n\n" +
            "Или настройте свою дату и время в формате: ДД.ММ.ГГГГ ЧЧ:ММ\n" +
            "Например: 25.12.2024 14:30", 
            replyMarkup: keyboard, parseMode: ParseMode.Html);
    }
    
    // Запрос даты и времени для напоминания
    private static async Task AskReminderDateTime(Chat chat, int messageId, int placeId, long userId)
    {
        var backButton = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("← Назад", $"reminder_{placeId}") }
        });
        
        await Globals.Bot.EditMessageText(chat, messageId, 
            "📅 <b>Введите дату и время напоминания:</b>\n\n" +
            "Формат: <b>ДД.ММ.ГГГГ ЧЧ:ММ</b>\n" +
            "Например: <b>25.12.2024 14:30</b>\n\n" +
            "Отправьте сообщение с датой и временем.", 
            replyMarkup: backButton, parseMode: ParseMode.Html);
    }
    
    // Установка напоминания
    private static async Task SetReminder(Chat chat, int messageId, int placeId, long userId, int days, string callbackQueryId)
    {
        using (var db = new BotDbContext())
        {
            var user = await GetOrCreateUser(userId, null, null, null);
            var place = db.Places.FirstOrDefault(p => p.Id == placeId);
            
            if (place == null)
            {
                await Globals.Bot.EditMessageText(chat, messageId, "❌ Место не найдено.");
                return;
            }
            
            // Удаляем старые невыполненные напоминания для этого места
            var oldReminders = db.Reminders.Where(r => r.UserId == user.Id && r.PlaceId == placeId && !r.IsCompleted);
            db.Reminders.RemoveRange(oldReminders);
            
            // Создаём новое напоминание
            var reminder = new Reminder
            {
                UserId = user.Id,
                PlaceId = placeId,
                ReminderDate = DateTime.UtcNow.AddDays(days),
                IsCompleted = false
            };
            db.Reminders.Add(reminder);
            await db.SaveChangesAsync();
            
            var reminderDateLocal = reminder.ReminderDate.ToLocalTime();
            await Globals.Bot.AnswerCallbackQuery(callbackQueryId, 
                $"Напоминание установлено на {reminderDateLocal:dd.MM.yyyy HH:mm}! 🔔");
            
            // Возвращаемся к информации о месте
            await ShowPlaceDetails(chat, messageId, placeId, userId);
        }
    }
    
    // Показ посещенных мест
    private static async Task ShowVisitedPlaces(Chat chat, int messageId, long userId)
    {
        using (var db = new BotDbContext())
        {
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
                    new[] 
                    { 
                        InlineKeyboardButton.WithCallbackData("← Назад", "seeProfile"),
                        InlineKeyboardButton.WithCallbackData("🏠 Главное меню", "seeProfile")
                    }
                });
                await Globals.Bot.EditMessageText(chat, messageId, 
                    "📍 У вас пока нет посещенных мест.", replyMarkup: backButton);
                return;
            }
            
            var message = new StringBuilder();
            message.AppendLine($"📍 <b>Посещенные места ({visits.Count}):</b>\n");
            
            var buttons = new List<InlineKeyboardButton[]>();
            
            foreach (var visit in visits)
            {
                var visitDate = visit.VisitedAt.ToLocalTime();
                message.AppendLine($"✅ <b>{visit.Place.Name}</b>");
                message.AppendLine($"   📅 {visitDate:dd.MM.yyyy HH:mm}");
                if (!string.IsNullOrEmpty(visit.Place.District))
                    message.AppendLine($"   🏘️ {visit.Place.District}");
                message.AppendLine();
                
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData($"📍 {visit.Place.Name}", $"place_{visit.Place.Id}")
                });
            }
            
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("← Назад", "seeProfile")
            });
            
            var keyboard = new InlineKeyboardMarkup(buttons);
            
            await Globals.Bot.EditMessageText(chat, messageId, message.ToString(), 
                replyMarkup: keyboard, parseMode: ParseMode.Html);
        }
    }
    
    // Показ напоминаний
    private static async Task ShowReminders(Chat chat, int messageId, long userId)
    {
        using (var db = new BotDbContext())
        {
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
                    new[] 
                    { 
                        InlineKeyboardButton.WithCallbackData("← Назад", "seeProfile"),
                        InlineKeyboardButton.WithCallbackData("🏠 Главное меню", "seeProfile")
                    }
                });
                await Globals.Bot.EditMessageText(chat, messageId, 
                    "🔔 У вас нет активных напоминаний.", replyMarkup: backButton);
                return;
            }
            
            var message = new StringBuilder();
            message.AppendLine($"🔔 <b>Активные напоминания ({reminders.Count}):</b>\n");
            
            var buttons = new List<InlineKeyboardButton[]>();
            
            foreach (var reminder in reminders)
            {
                var reminderDate = reminder.ReminderDate.ToLocalTime();
                message.AppendLine($"🔔 <b>{reminder.Place.Name}</b>");
                message.AppendLine($"   📅 {reminderDate:dd.MM.yyyy HH:mm}");
                if (!string.IsNullOrEmpty(reminder.Place.District))
                    message.AppendLine($"   🏘️ {reminder.Place.District}");
                message.AppendLine();
                
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData($"📍 {reminder.Place.Name}", $"place_{reminder.Place.Id}")
                });
            }
            
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("← Назад", "seeProfile"),
                InlineKeyboardButton.WithCallbackData("🏠 Главное меню", "seeProfile")
            });
            
            var keyboard = new InlineKeyboardMarkup(buttons);
            
            await Globals.Bot.EditMessageText(chat, messageId, message.ToString(), 
                replyMarkup: keyboard, parseMode: ParseMode.Html);
        }
    }

    // Обработка ввода даты и времени для напоминания
    private static async Task ProcessReminderDateTime(Chat chat, Message message, long userId)
    {
        if (!PendingReminderPlace.TryGetValue(userId, out var placeId))
        {
            await Globals.Bot.SendMessage(chat, "❌ Не удалось определить место для напоминания.");
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
                await Globals.Bot.SendMessage(chat, "❌ Дата и время должны быть в будущем!");
                return;
            }

            using var db = new BotDbContext();
            var user = await GetOrCreateUser(userId, null, null, null);

            // удаляем старые
            var oldReminders = db.Reminders
                .Where(r => r.UserId == user.Id && r.PlaceId == placeId && !r.IsCompleted);
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

            await Globals.Bot.SendMessage(chat,
                $"🔔 Напоминание установлено на {reminderDate:dd.MM.yyyy HH:mm}!");
        }
        catch
        {
            await Globals.Bot.SendMessage(chat, "❌ Неверный формат даты. Используйте: ДД.ММ.ГГГГ ЧЧ:ММ");
        }
    }


    // Показ достижений
    private static async Task ShowAchievements(Chat chat, int messageId, long userId)
    {
        using (var db = new BotDbContext())
        {
            var user = await GetOrCreateUser(userId, null, null, null);
            
            // Инициализируем достижения, если их еще нет
            await InitializeAchievements(db);
            
            var allAchievements = db.Achievements.ToList();
            var userAchievements = db.UserAchievements
                .Where(ua => ua.UserId == user.Id)
                .Include(ua => ua.Achievement)
                .ToList();
            
            var message = new StringBuilder();
            message.AppendLine("🏆 <b>Достижения:</b>\n");
            
            var buttons = new List<InlineKeyboardButton[]>();
            
            foreach (var achievement in allAchievements)
            {
                var isUnlocked = userAchievements.Any(ua => ua.AchievementId == achievement.Id);
                var icon = isUnlocked ? achievement.Icon : "🔒";
                var status = isUnlocked ? "✅" : "❌";
                
                message.AppendLine($"{icon} {status} <b>{achievement.Name}</b>");
                message.AppendLine($"   {achievement.Description}\n");
            }
            
            var unlockedCount = userAchievements.Count;
            message.AppendLine($"\n<b>Разблокировано: {unlockedCount} из {allAchievements.Count}</b>");
            
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("← Назад", "seeProfile"),
                InlineKeyboardButton.WithCallbackData("🏠 Главное меню", "seeProfile")
            });
            
            var keyboard = new InlineKeyboardMarkup(buttons);
            
            await Globals.Bot.EditMessageText(chat, messageId, message.ToString(), 
                replyMarkup: keyboard, parseMode: ParseMode.Html);
        }
    }
    
    // Инициализация достижений
    private static async Task InitializeAchievements(BotDbContext db)
    {
        if (db.Achievements.Any())
            return;
        
        db.Achievements.AddRange(
            new Achievement
            {
                Name = "Первый шаг",
                Description = "Посетите первое место",
                Icon = "🎯",
                Type = AchievementType.FirstVisit,
                RequiredValue = 1
            },
            new Achievement
            {
                Name = "Исследователь",
                Description = "Посетите 5 мест",
                Icon = "🗺️",
                Type = AchievementType.PlacesVisited,
                RequiredValue = 5
            },
            new Achievement
            {
                Name = "Путешественник",
                Description = "Посетите 10 мест",
                Icon = "🌍",
                Type = AchievementType.PlacesVisited,
                RequiredValue = 10
            },
            new Achievement
            {
                Name = "Гид Минска",
                Description = "Посетите 20 мест",
                Icon = "👑",
                Type = AchievementType.PlacesVisited,
                RequiredValue = 20
            },
            new Achievement
            {
                Name = "Знаток категорий",
                Description = "Посетите 3 места одной категории",
                Icon = "🏷️",
                Type = AchievementType.CategoryExplorer,
                RequiredValue = 3
            },
            new Achievement
            {
                Name = "Исследователь районов",
                Description = "Посетите 3 места одного района",
                Icon = "🏘️",
                Type = AchievementType.DistrictExplorer,
                RequiredValue = 3
            },
            new Achievement
            {
                Name = "Организованный",
                Description = "Установите 5 напоминаний",
                Icon = "🔔",
                Type = AchievementType.ReminderMaster,
                RequiredValue = 5
            }
        );
        
        await db.SaveChangesAsync();
    }
    
    // Проверка достижений
    private static async Task CheckAchievements(long userId)
    {
        using (var db = new BotDbContext())
        {
            var user = await GetOrCreateUser(userId, null, null, null);
            
            await InitializeAchievements(db);
            
            var visits = db.UserVisits
                .Where(v => v.UserId == user.Id)
                .Include(v => v.Place)
                .ToList();
            var reminders = db.Reminders.Where(r => r.UserId == user.Id).ToList();
            var userAchievements = db.UserAchievements
                .Where(ua => ua.UserId == user.Id)
                .Select(ua => ua.AchievementId)
                .ToList();
            
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
                        var categoryGroups = visits
                            .GroupBy(v => v.Place.Category)
                            .Where(g => g.Count() >= achievement.RequiredValue);
                        unlocked = categoryGroups.Any();
                        break;
                    case AchievementType.DistrictExplorer:
                        var districtGroups = visits
                            .GroupBy(v => v.Place.District)
                            .Where(g => g.Count() >= achievement.RequiredValue);
                        unlocked = districtGroups.Any();
                        break;
                    case AchievementType.ReminderMaster:
                        unlocked = reminders.Count >= achievement.RequiredValue;
                        break;
                }
                
                if (unlocked)
                {
                    var userAchievement = new UserAchievement
                    {
                        UserId = user.Id,
                        AchievementId = achievement.Id,
                        UnlockedAt = DateTime.UtcNow
                    };
                    db.UserAchievements.Add(userAchievement);
                    await db.SaveChangesAsync();
                    
                    // Уведомляем пользователя
                    try
                    {
                        await Globals.Bot.SendMessage(user.TelegramId, 
                            $"🎉 <b>Достижение разблокировано!</b>\n\n" +
                            $"{achievement.Icon} <b>{achievement.Name}</b>\n" +
                            $"{achievement.Description}", 
                            parseMode: ParseMode.Html);
                    }
                    catch { }
                }
            }
        }
    }

}


