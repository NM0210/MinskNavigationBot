# Minsk Navigation Bot

Telegram бот для навигации по Минску с квизами, напоминаниями и достижениями.

## Требования

- .NET 8.0 SDK
- Telegram Bot Token (получить у [@BotFather](https://t.me/BotFather))

## Установка и запуск

1. **Создайте файл `.env` в корне проекта (папка MinskNavigationBot):**
   
   **Способ 1 (Windows - через Блокнот):**
   - Откройте Блокнот (Notepad)
   - Введите следующую строку:
     ```
     BOT_TOKEN=ваш_токен_бота_здесь
     ```
   - Сохраните файл как `.env` (важно: имя файла должно начинаться с точки!)
   - При сохранении выберите "Все файлы" в типе файла
   - Убедитесь, что файл сохранен в папке `D:\проект_надя\MinskNavigationBot\`
   
   **Способ 2 (Windows - через командную строку):**
   ```cmd
   cd D:\проект_надя\MinskNavigationBot
   echo BOT_TOKEN=ваш_токен_бота_здесь > .env
   ```
   
   **Способ 3 (через PowerShell):**
   ```powershell
   cd "D:\проект_надя\MinskNavigationBot"
   "BOT_TOKEN=ваш_токен_бота_здесь" | Out-File -FilePath .env -Encoding utf8
   ```

2. **Откройте файл `.env` и замените `ваш_токен_бота_здесь` на реальный токен бота:**
   ```
   BOT_TOKEN=1234567890:ABCdefGHIjklMNOpqrsTUVwxyz
   ```

3. **Восстановите зависимости:**
   ```bash
   dotnet restore
   ```

4. **Запустите проект:**
   ```bash
   dotnet run
   ```
   
   Или из папки проекта:
   ```bash
   cd MinskNavigationBot
   dotnet run
   ```

## Как получить токен бота

1. Откройте Telegram и найдите [@BotFather](https://t.me/BotFather)
2. Отправьте команду `/newbot`
3. Следуйте инструкциям для создания бота
4. Скопируйте полученный токен в файл `.env`

## Структура проекта

- `Program.cs` - точка входа приложения
- `BotHandlers.cs` - обработчики сообщений и callback'ов
- `BotInitializer.cs` - инициализация бота
- `Menu.cs` - меню бота
- `Models/` - модели данных
- `Data/` - контекст базы данных и сидер

## База данных

База данных SQLite создается автоматически при первом запуске в файле `bot.db`.

