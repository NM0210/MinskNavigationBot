namespace MinskNavigationBot.Models;

public class Achievement
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Icon { get; set; } = null!;
    public AchievementType Type { get; set; }
    public int RequiredValue { get; set; } // Количество для получения (например, 5 мест, 3 категории и т.д.)
}

public enum AchievementType
{
    QuizCompleted,           // Пройден квиз
    PlacesVisited,            // Посещено N мест
    CategoryExplorer,         // Посещено N мест одной категории
    DistrictExplorer,         // Посещено N мест одного района
    FirstVisit,               // Первое посещение
    ReminderMaster            // Установлено N напоминаний
}



