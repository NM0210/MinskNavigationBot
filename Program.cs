using DotNetEnv;
using MinskNavigationBot.Data;
using Telegram.Bot;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Quartz.Impl;

namespace MinskNavigationBot;


class Program
{
    static async Task Main()
    {
        // Загружаем .env файл из корня проекта
        // Находим корень проекта, поднимаясь от исполняемого файла до папки с .csproj
        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var assemblyDir = Path.GetDirectoryName(assemblyLocation);
        var projectRoot = assemblyDir;
        
        // Поднимаемся вверх по директориям, пока не найдем папку с .csproj файлом
        while (projectRoot != null && !File.Exists(Path.Combine(projectRoot, "MinskNavigationBot.csproj")))
        {
            var parent = Directory.GetParent(projectRoot);
            if (parent == null) break;
            projectRoot = parent.FullName;
        }
        
        // Если не нашли .csproj, используем текущую директорию
        if (projectRoot == null || !File.Exists(Path.Combine(projectRoot, "MinskNavigationBot.csproj")))
        {
            projectRoot = Directory.GetCurrentDirectory();
        }
        
        var envPath = Path.Combine(projectRoot, ".env");
        
        if (File.Exists(envPath))
        {
            Env.Load(envPath);
            Console.WriteLine($"✓ Загружен .env файл из: {envPath}");
        }
        else
        {
            // Пробуем загрузить из текущей директории (стандартное поведение DotNetEnv)
            Env.Load();
            Console.WriteLine($"⚠ Предупреждение: .env файл не найден в корне проекта: {projectRoot}");
            Console.WriteLine($"  Ищу .env в текущей директории: {Directory.GetCurrentDirectory()}");
        }
        
        // Инициализация базы данных
        using (var db = new BotDbContext())
        {
            db.Database.EnsureCreated();
            DbSeeder.Seed(db);
        }
        
        await BotInitializer.Start();
        var schedulerFactory = new StdSchedulerFactory();
        var scheduler = await schedulerFactory.GetScheduler();
        await scheduler.Start();

        var job = JobBuilder.Create<ReminderJob>()
            .WithIdentity("reminder-job")
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity("reminder-trigger")
            .StartNow()
            .WithSimpleSchedule(x => x
                .WithIntervalInSeconds(30)
                .RepeatForever())
            .Build();

        await scheduler.ScheduleJob(job, trigger);

        Console.WriteLine("Бот запущен и готов к работе!");
        Console.WriteLine("Нажмите Ctrl+C для остановки.");
        
        // Ожидаем бесконечно, чтобы бот продолжал работать
        await Task.Delay(-1);
    }
}