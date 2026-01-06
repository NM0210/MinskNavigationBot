using Microsoft.EntityFrameworkCore;
using MinskNavigationBot.Models;

namespace MinskNavigationBot.Data;

public class BotDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Place> Places => Set<Place>();
    public DbSet<UserVisit> UserVisits => Set<UserVisit>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite("Data Source=bot.db");
    }
}
