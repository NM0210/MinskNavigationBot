namespace MinskNavigationBot.Models;

public class Reminder
{
    public int Id { get; set; }
    
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    
    public int PlaceId { get; set; }
    public Place Place { get; set; } = null!;
    
    public DateTime ReminderDate { get; set; }
    public string? Note { get; set; }
    public bool IsCompleted { get; set; }
}



