namespace MinskNavigationBot.Models;

public class UserVisit
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int PlaceId { get; set; }
    public Place Place { get; set; } = null!;

    public DateTime VisitedAt { get; set; }
}
