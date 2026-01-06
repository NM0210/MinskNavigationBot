using MinskNavigationBot.Models;

namespace MinskNavigationBot.Data;

public static class DbSeeder
{
    public static void Seed(BotDbContext db)
    {
        if (db.Places.Any())
            return; // Уже заполнено

        db.Places.AddRange(
            // Центральный район
            new Place
            {
                Name = "Площадь Победы",
                Description = "Центральная площадь Минска",
                Address = "пл. Победы",
                District = "Центральный",
                Latitude = 53.9145,
                Longitude = 27.5706,
                Category = "Достопримечательность"
            },
            new Place
            {
                Name = "Парк Горького",
                Description = "Городской парк отдыха",
                Address = "ул. Фрунзе",
                District = "Центральный",
                Latitude = 53.9076,
                Longitude = 27.5624,
                Category = "Парк"
            },
            new Place
            {
                Name = "Национальный художественный музей",
                Description = "Крупнейший музей изобразительного искусства",
                Address = "ул. Ленина, 20",
                District = "Центральный",
                Latitude = 53.9025,
                Longitude = 27.5611,
                Category = "Музей"
            },
            new Place
            {
                Name = "Октябрьская площадь",
                Description = "Главная площадь Минска",
                Address = "пл. Октябрьская",
                District = "Центральный",
                Latitude = 53.8969,
                Longitude = 27.5478,
                Category = "Достопримечательность"
            },
            new Place
            {
                Name = "Ресторан 'Васильки'",
                Description = "Белорусская кухня",
                Address = "пр. Независимости, 16",
                District = "Центральный",
                Latitude = 53.9042,
                Longitude = 27.5615,
                Category = "Ресторан"
            },
            
            // Первомайский район
            new Place
            {
                Name = "Национальная библиотека Беларуси",
                Description = "Главная библиотека страны",
                Address = "пр. Независимости, 116",
                District = "Первомайский",
                Latitude = 53.9311,
                Longitude = 27.6454,
                Category = "Культура"
            },
            new Place
            {
                Name = "Музей истории Великой Отечественной войны",
                Description = "Национальный музей истории ВОВ",
                Address = "пр. Победителей, 8",
                District = "Первомайский",
                Latitude = 53.9275,
                Longitude = 27.5361,
                Category = "Музей"
            },
            new Place
            {
                Name = "Парк Челюскинцев",
                Description = "Парк культуры и отдыха",
                Address = "пр. Независимости",
                District = "Первомайский",
                Latitude = 53.9367,
                Longitude = 27.6183,
                Category = "Парк"
            },
            new Place
            {
                Name = "Торговый центр 'Столица'",
                Description = "Крупный торговый центр",
                Address = "пр. Независимости, 3",
                District = "Первомайский",
                Latitude = 53.9333,
                Longitude = 27.6500,
                Category = "Торговый центр"
            },
            
            // Советский район
            new Place
            {
                Name = "Парк Победы",
                Description = "Парк на берегу Комсомольского озера",
                Address = "ул. Орловская",
                District = "Советский",
                Latitude = 53.9389,
                Longitude = 27.4856,
                Category = "Парк"
            },
            new Place
            {
                Name = "Минский зоопарк",
                Description = "Зоологический парк",
                Address = "ул. Ташкентская, 40",
                District = "Советский",
                Latitude = 53.9456,
                Longitude = 27.4989,
                Category = "Развлечения"
            },
            new Place
            {
                Name = "Ресторан 'Камяница'",
                Description = "Белорусская кухня в историческом здании",
                Address = "ул. Революционная, 10",
                District = "Советский",
                Latitude = 53.9322,
                Longitude = 27.4922,
                Category = "Ресторан"
            },
            
            // Фрунзенский район
            new Place
            {
                Name = "Парк 50-летия Октября",
                Description = "Парк отдыха",
                Address = "ул. Кальварийская",
                District = "Фрунзенский",
                Latitude = 53.8889,
                Longitude = 27.5111,
                Category = "Парк"
            },
            new Place
            {
                Name = "Торговый центр 'Титан'",
                Description = "Современный торговый центр",
                Address = "ул. Кальварийская, 24",
                District = "Фрунзенский",
                Latitude = 53.8900,
                Longitude = 27.5200,
                Category = "Торговый центр"
            },
            new Place
            {
                Name = "Музей истории Минска",
                Description = "Музей истории города",
                Address = "ул. Революционная, 10А",
                District = "Фрунзенский",
                Latitude = 53.8956,
                Longitude = 27.5056,
                Category = "Музей"
            },
            
            // Ленинский район
            new Place
            {
                Name = "Парк Янки Купалы",
                Description = "Парк имени белорусского поэта",
                Address = "ул. Янки Купалы",
                District = "Ленинский",
                Latitude = 53.9111,
                Longitude = 27.5833,
                Category = "Парк"
            },
            new Place
            {
                Name = "Национальный академический театр оперы и балета",
                Description = "Главный театр оперы и балета",
                Address = "пл. Парижской Коммуны, 1",
                District = "Ленинский",
                Latitude = 53.9089,
                Longitude = 27.5756,
                Category = "Культура"
            },
            new Place
            {
                Name = "Ресторан 'Купаловский'",
                Description = "Традиционная белорусская кухня",
                Address = "пр. Победителей, 2",
                District = "Ленинский",
                Latitude = 53.9100,
                Longitude = 27.5800,
                Category = "Ресторан"
            },
            
            // Заводской район
            new Place
            {
                Name = "Парк Дружбы народов",
                Description = "Парк для отдыха и прогулок",
                Address = "ул. Долгобродская",
                District = "Заводской",
                Latitude = 53.8667,
                Longitude = 27.5333,
                Category = "Парк"
            },
            new Place
            {
                Name = "Торговый центр 'Алми'",
                Description = "Районный торговый центр",
                Address = "ул. Долгобродская, 41",
                District = "Заводской",
                Latitude = 53.8689,
                Longitude = 27.5356,
                Category = "Торговый центр"
            },
            
            // Московский район
            new Place
            {
                Name = "Парк Дружбы",
                Description = "Парк культуры и отдыха",
                Address = "ул. Маяковского",
                District = "Московский",
                Latitude = 53.8556,
                Longitude = 27.5111,
                Category = "Парк"
            },
            new Place
            {
                Name = "Развлекательный центр 'Фристайл'",
                Description = "Кинотеатр и развлечения",
                Address = "пр. Дзержинского, 104",
                District = "Московский",
                Latitude = 53.8578,
                Longitude = 27.5133,
                Category = "Развлечения"
            }
        );

        db.SaveChanges();


    }
}
