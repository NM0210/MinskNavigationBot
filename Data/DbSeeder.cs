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
                Latitude = 53.908522,
                Longitude = 27.574821,
                Category = "Достопримечательность",
                ImageUrl = "площадь_победы.jpg" // Имя файла из папки Photos
            },
            new Place
            {
                Name = "Парк Горького",
                Description = "Городской парк отдыха",
                Address = "ул. Фрунзе",
                District = "Центральный",
                Latitude = 53.903133,
                Longitude = 27.573285,
                Category = "Парк",
                ImageUrl = "парк_горького.jpg" // Имя файла из папки Photos
            },
            new Place
            {
                Name = "Национальный художественный музей",
                Description = "Крупнейший музей изобразительного искусства",
                Address = "ул. Ленина, 20",
                District = "Центральный",
                Latitude = 53.898475,
                Longitude = 27.560780,
                Category = "Музей",
                ImageUrl = "нац_худож_музей.png" // Имя файла из папки Photos
            },
            new Place
            {
                Name = "Октябрьская площадь",
                Description = "Главная площадь Минска",
                Address = "пл. Октябрьская",
                District = "Центральный",
                Latitude = 53.902554,
                Longitude = 27.561508,
                Category = "Достопримечательность",
                ImageUrl = "октябрьская_площадь.jpg" // Имя файла из папки Photos
            },
            new Place
            {
                Name = "Ресторан 'Васильки'",
                Description = "Белорусская кухня",
                Address = "пр. Независимости, 16",
                District = "Центральный",
                Latitude = 53.898548,
                Longitude = 27.555489,
                Category = "Ресторан",
                ImageUrl = "васильки.jpg" // Имя файла из папки Photos
            },
            
            // Первомайский район
            new Place
            {
                Name = "Национальная библиотека Беларуси",
                Description = "Главная библиотека страны",
                Address = "пр. Независимости, 116",
                District = "Первомайский",
                Latitude = 53.931783,
                Longitude = 27.645678,
                Category = "Культура",
                ImageUrl = "нац_библиотека.jpg"
            },
            new Place
            {
                Name = "Музей истории Великой Отечественной войны",
                Description = "Национальный музей истории ВОВ",
                Address = "пр. Победителей, 8",
                District = "Первомайский",
                Latitude = 53.916413,
                Longitude = 27.537925,
                Category = "Музей",
                ImageUrl = "музей_вов.jpg"
            },
            new Place
            {
                Name = "Парк Челюскинцев",
                Description = "Парк культуры и отдыха",
                Address = "пр. Независимости",
                District = "Первомайский",
                Latitude = 53.922055,
                Longitude = 27.617000,
                Category = "Парк",
                ImageUrl = "парк_челюскинцев.jpg"
            },
            new Place
            {
                Name = "Торговый центр 'Столица'",
                Description = "Крупный торговый центр",
                Address = "пр. Независимости, 3",
                District = "Первомайский",
                Latitude = 53.895469,
                Longitude = 27.548170,
                Category = "Торговый центр",
                ImageUrl = "тц_столица.jpg"
            },
            
            // Советский район
            new Place
            {
                Name = "Парк Победы",
                Description = "Парк на берегу Комсомольского озера",
                Address = "ул. Орловская",
                District = "Советский",
                Latitude = 53.917660,
                Longitude = 27.539205,
                Category = "Парк",
                ImageUrl = "парк_победы.jpg"
            },
            new Place
            {
                Name = "Минский зоопарк",
                Description = "Зоологический парк",
                Address = "ул. Ташкентская, 40",
                District = "Советский",
                Latitude = 53.850021,
                Longitude = 27.634711,
                Category = "Развлечения",
                ImageUrl = "минск_зоопарк.jpg"
            },
            new Place
            {
                Name = "Ресторан 'Камяница'",
                Description = "Белорусская кухня в историческом здании",
                Address = "ул. Революционная, 10",
                District = "Советский",
                Latitude = 53.900458,
                Longitude = 27.574949,
                Category = "Ресторан",
                ImageUrl = "камяница.jpg"
            },
            
            // Фрунзенский район
            new Place
            {
                Name = "Парк 50-летия Октября",
                Description = "Парк отдыха",
                Address = "ул. Кальварийская",
                District = "Фрунзенский",
                Latitude = 55.684820,
                Longitude = 37.501954,
                Category = "Парк",
                ImageUrl = "парк_50лет_октября.jpg"
            },
            new Place
            {
                Name = "Торговый центр 'Титан'",
                Description = "Современный торговый центр",
                Address = "ул. Кальварийская, 24",
                District = "Фрунзенский",
                Latitude = 53.861058,
                Longitude = 27.479425,
                Category = "Торговый центр",
                ImageUrl = "тц_титан.jpg"
            },
            new Place
            {
                Name = "Музей истории Минска",
                Description = "Музей истории города",
                Address = "ул. Революционная, 10А",
                District = "Фрунзенский",
                Latitude = 53.904578,
                Longitude = 27.549492,
                Category = "Музей",
                ImageUrl = "музей_истории_минска.jpg"
            },
            
            // Ленинский район
            new Place
            {
                Name = "Парк Янки Купалы",
                Description = "Парк имени белорусского поэта",
                Address = "ул. Янки Купалы",
                District = "Ленинский",
                Latitude = 53.906629,
                Longitude = 27.567064,
                Category = "Парк",
                ImageUrl = "парк_янки_купалы.jpg"
            },

            new Place
            {
                Name = "Ресторан 'Купаловский'",
                Description = "Традиционная белорусская кухня",
                Address = "пр. Победителей, 2",
                District = "Ленинский",
                Latitude = 53.907095,
                Longitude = 27.552632, 
                Category = "Ресторан",
                ImageUrl = "ресторан_купаловский.jpg"
            },
            
            // Заводской район
            new Place
            {
                Name = "Парк Дружбы народов",
                Description = "Парк для отдыха и прогулок",
                Address = "ул. Долгобродская",
                District = "Заводской",
                Latitude = 53.933110,
                Longitude = 27.570060,
                Category = "Парк",
                ImageUrl = "парк_дружбы_народов.jpg"
            },
            new Place
            {
                Name = "Торговый центр 'Алми'",
                Description = "Районный торговый центр",
                Address = "ул. Долгобродская, 41",
                District = "Заводской",
                Latitude = 53.886143,
                Longitude = 27.619808,
                Category = "Торговый центр",
                ImageUrl = "алми.jpg"
            },
            
            // Московский район
            new Place
            {
                Name = "Развлекательный центр 'Фристайл'",
                Description = "Кинотеатр и развлечения",
                Address = "пр. Дзержинского, 104",
                District = "Московский",
                Latitude = 53.861915,
                Longitude = 27.480363,
                Category = "Развлечения",
                ImageUrl = "фристайл.jpg"
            }
        );

        db.SaveChanges();


    }
}
