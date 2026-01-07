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
        Env.Load();
        
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