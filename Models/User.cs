namespace MinskNavigationBot.Models;

public class User
{
    public ICollection<UserVisit> Visits { get; set; } = new List<UserVisit>();
    public ICollection<Reminder> Reminders { get; set; } = new List<Reminder>();
    public ICollection<UserAchievement> Achievements { get; set; } = new List<UserAchievement>();
    public int Id { get; set; }
    public long TelegramId { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime RegisteredAt { get; set; }
}
