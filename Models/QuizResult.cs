namespace MinskNavigationBot.Models;

public class QuizResult
{
    public int Id { get; set; }
    
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public DateTime CompletedAt { get; set; }
}

