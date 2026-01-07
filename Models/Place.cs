
namespace MinskNavigationBot.Models;

public class Place
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? District { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public string? Category { get; set; }
    public string? ImageUrl { get; set; }

    public ICollection<UserVisit> Visits { get; set; } = new List<UserVisit>();
    public ICollection<Reminder> Reminders { get; set; } = new List<Reminder>();
}
