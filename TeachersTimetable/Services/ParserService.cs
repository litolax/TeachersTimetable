using System.Reflection;
using HtmlAgilityPack;
using MongoDB.Driver;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using TeachersTimetable.Config;
using TeachersTimetable.Models;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using Timer = System.Timers.Timer;
using User = Telegram.BotAPI.AvailableTypes.User;

namespace TeachersTimetable.Services;

public interface IParserService
{
    List<string> Teachers { get; }
    Task SendNewDayTimetables();
    Task SendDayTimetable(User telegramUser);
    Task ParseDayTimetables();
    Task ParseWeekTimetables();
    Task SendWeekTimetable(User telegramUser);
}

public class ParserService : IParserService
{
    private readonly IMongoService _mongoService;
    
    private const string WeekUrl = "https://mgkct.minskedu.gov.by/персоналии/преподавателям/расписание-занятий-на-неделю";
    
    public List<string> Teachers { get; } = new()
    {
        "Амброжи Н. М.",
        "Ананич В. Д.",
        "Анципов Е. Ю.",
        "Бабер А. И.",
        "Барсукова Е. А.",
        "Барсукова Н. В.",
        "Белобровик А. А.",
        "Берговина А. В.",
        "Богдановская О. Н.",
        "Босянок Г. Ф.",
        "Бровка Д. С.",
        "Вайтович И. М.",
        "Витебская Е. С.",
        "Волошко М. Ф.",
        "Воронько С. В.",
        "Вострикова Т. С.",
        "Галенко Е. Л.",
        "Галицкий М. И.",
        "Герасюк В. В.",
        "Гриневич П. Р.",
        "Громыко Н. К.",
        "Дзевенская Р. И.",
        "Дзевенский Э. М.",
        "Дорц Н. А.",
        "Дудко А. Р.",
        "Жарский В. А.",
        "Жукова Т. Ю.",
        "Зайковская М. И.",
        "Звягина Д. Ч.",
        "Зданович С. А.",
        "Калацкая Т. Е.",
        "Камлюк В. С.",
        "Касперович С. А.",
        "Киселёв В. Д.",
        "Козел А. А.",
        "Козел Г. В.",
        "Колинко Н. Г.",
        "Колышкина Л. Н.",
        "Коропа Е. Н.",
        "Кохно Т. А.",
        "Красовская А. В.",
        "Крыж Е. А.",
        "Кулецкая Ю. Н.",
        "Кульбеда М. П.",
        "Лебедкина Н. В.",
        "Левонюк Е. А.",
        "Леус Ж. В.",
        "Лихачева О. П.",
        "Магаревич Е. А.",
        "Макаренко Е. В.",
        "Мурашко А. В.",
        "Нарбутович К. П.",
        "Немцева Н. А.",
        "Оберган С. А.",
        "Перепелкин А. М.",
        "Петуховский М. С.",
        "Пешкова Г. Д.",
        "Питель В. В.",
        "Плаксин Е. Б.",
        "Поклад Т. И.",
        "Потапчик И. Г.",
        "Потес Р. И.",
        "Потоцкий Д. С.",
        "Прокопович М. Е.",
        "Протасеня А. О.",
        "Пугач А. И.",
        "Пуршнев А. В.",
        "Романович С. Г.",
        "Самарская Н. В.",
        "Самохвал Н. Н.",
        "Северин А. В.",
        "Селицкая О. Ю.",
        "Семенова Л. Н.",
        "Сергун Т. С.",
        "Скобля Я. Э.",
        "Сом И. М.",
        "Сотникова О. А.",
        "Стома Р. Н.",
        "Стрельченя В. М.",
        "Тарасевич А. В.",
        "Тарасова Е. И.",
        "Титов Ю. А.",
        "Тихонович Н. В.",
        "Тишков М. И.",
        "Тозик Е. Ф.",
        "Усикова Л. Н.",
        "Федкевич Д. А.",
        "Федунов В. С.",
        "Фетисова Ю. Б.",
        "Филипцова Е. В.",
        "Харевская Е. Т.",
        "Хомченко И. И.",
        "Чертков М. Д.",
        "Шавейко А. А.",
        "Шеметов И. В.",
        "Щуко О. И.",
        "Эльканович А. Ф.",
    };
    
    public List<Timetable>? Timetables { get; set; } = new();
    
    private string LastDayHtmlContent { get; set; }
    private string LastWeekHtmlContent { get; set; }

    public ParserService(IMongoService mongoService)
    {
        this._mongoService = mongoService;
        
        // var parseDayTimer = new Timer(10000)
        // {
        //     AutoReset = true, Enabled = true
        // };
        // parseDayTimer.Elapsed += async (sender, args) =>
        // {
        //     await this.NewDayTimetableCheck();
        // };
        
        var parseWeekTimer = new Timer(10000)
        {
            AutoReset = true, Enabled = true
        };
        parseWeekTimer.Elapsed += async (sender, args) =>
        {
            await this.NewWeekTimetableCheck();
        };
    }

    public async Task ParseDayTimetables()
    {
        var timetablesCollection = this._mongoService.Database.GetCollection<Timetable>("DayTimetables");
        var dbTables = (await timetablesCollection.FindAsync(table => true)).ToList();
        var url = "http://mgke.minsk.edu.by/ru/main.aspx?guid=3821";
        var web = new HtmlWeb();
        var doc = web.Load(url);
        this.LastDayHtmlContent = doc.DocumentNode.InnerHtml;
        var tables = doc.DocumentNode.SelectNodes("//table");
        this.Timetables = new List<Timetable>();
        if (tables is null)
        {
            this.Timetables = null;
            return;
        }

        try
        {
            foreach (var table in tables)
            {
                var teachersAndLessons = new Dictionary<string, List<Lesson>>();
                var t = table.SelectNodes("./tbody/tr");
                if (t is null)
                {
                    t = table.SelectNodes("./thead/tr");
                    if (t is null) continue;
                }

                for (int i = 3; i < t.Count; i++)
                {
                    List<Lesson> lessons = new List<Lesson>();
                    int number = 1;
                    for (int j = 5; j < t[i].ChildNodes.Count; j += 4)
                    {
                        if (t[i].ChildNodes[j].InnerText.Contains("&nbsp;"))
                        {
                            lessons.Add(new Lesson()
                            {
                                Group = "-",
                                Cabinet = "-",
                                Index = number,
                            });
                            number++;
                            continue;
                        }

                        lessons.Add(new Lesson()
                        {
                            Group = t[i].ChildNodes[j].InnerText.Contains("&nbsp;")
                                ? "-"
                                : t[i].ChildNodes[j].InnerText,
                            Cabinet = t[i].ChildNodes[j + 2].InnerText.Contains("&nbsp;")
                                ? "-"
                                : t[i].ChildNodes[j + 2].InnerText,
                            Index = number,
                        });
                        number++;
                    }

                    int count = 0;
                    lessons.Reverse();
                    foreach (var lesson in lessons)
                    {
                        if (lesson.Cabinet == "-" && lesson.Group == "-") count++;
                        else break;
                    }

                    lessons.RemoveRange(0, count);
                    lessons.Reverse();
                    
                    teachersAndLessons.Add(t[i].ChildNodes[3].InnerText.Trim(), lessons);
                }

                this.Timetables.Add(new Timetable()
                {
                    Date = table.ChildNodes[1].ChildNodes[1].ChildNodes[5].InnerText.Trim(),
                    Table = new List<Dictionary<string, List<Lesson>>>()
                    {
                        teachersAndLessons
                    }
                });
            }
        }
        catch
        {
            //
        }


        bool hasNewTimetables = false;
        this.Timetables.ForEach(t =>
        {
            if (!dbTables.Exists(table => table.Date == t.Date))
            {
                timetablesCollection.InsertOneAsync(t);
                hasNewTimetables = true;
            }
        });

        if (hasNewTimetables)
        {
            await this.SendNewDayTimetables();
        }
    }

    public async Task ParseWeekTimetables()
    {
        var web = new HtmlWeb();
        var doc = web.Load(WeekUrl);
        
        this.LastWeekHtmlContent = doc.DocumentNode.InnerHtml;

        var teachers = doc.DocumentNode.SelectNodes("//h2");
        if (teachers is null) return;

        try
        {
            var newDate = doc.DocumentNode.SelectNodes("//h3")[0].InnerText.Trim();
            var dateDbCollection = this._mongoService.Database.GetCollection<Timetable>("WeekTimetables");
            var dbTables = (await dateDbCollection.FindAsync(d => true)).ToList();

            var options = new ChromeOptions();
            
            options.AddArgument("headless");
            options.AddArgument("--no-sandbox");
            options.AddArguments("--disable-dev-shm-usage");
            
            var driver = new ChromeDriver(options);

            foreach (var teacher in this.Teachers)
            {
                var filePath = $"./photo/{teacher}.png";

                driver.Navigate().GoToUrl($"{WeekUrl}?teacher={teacher.Replace(" ", "+")}");

                Utils.ModifyUnnecessaryElementsOnWebsite(ref driver);

                var element = driver.FindElements(By.TagName("h2")).FirstOrDefault();
                if (element == default) continue;

                var actions = new Actions(driver);
                actions.MoveToElement(element).Perform();

                var screenshot = (driver as ITakesScreenshot).GetScreenshot();
                screenshot.SaveAsFile(filePath, ScreenshotImageFormat.Png);

                var image = await Image.LoadAsync(filePath);

                image.Mutate(x => x.Resize((int)(image.Width / 1.5), (int)(image.Height / 1.5)));
                await image.SaveAsPngAsync(filePath);
            }
        
            driver.Close();
            driver.Quit();
           
            if (!dbTables.Exists(table => table.Date.Trim() == newDate))
            {
                await dateDbCollection.InsertOneAsync(new Timetable() {Date = newDate});
                // await this.SendNotificationsAboutWeekTimetable();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task SendNewDayTimetables()
    {
        var config = new Config<MainConfig>();
        var bot = new BotClient(config.Entries.Token);

        var userCollection = this._mongoService.Database.GetCollection<Models.User>("Users");
        var users = (await userCollection.FindAsync(u => true)).ToList();

        foreach (var user in users)
        {
            if (!user.Notifications || user.Teacher is null) continue;
            if (this.Timetables is null)
            {
                try
                {
                    await bot.SendMessageAsync(user.UserId, $"У преподавателя {user.Teacher} нет пар");
                    return;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            foreach (var timetable in this.Timetables)
            {
                var message = timetable.Date + "\n\n";
                foreach (var dictionary in timetable.Table)
                {
                    dictionary.TryGetValue(user.Teacher, out var lessons);
                    if (lessons is null)
                    {
                        message = $"У преподавателя {user.Teacher} нет пар на {timetable.Date}";
                        continue;
                    }
                    message += $"Преподаватель: {user.Teacher}\n";
                    foreach (var lesson in lessons)
                    {
                        message +=
                            $"Пара №{lesson.Index}\nГруппа: {lesson.Group.Trim()}\nКабинет: {lesson.Cabinet.Trim()}\n\n";
                    }
                }

                try
                {
                    await bot.SendMessageAsync(user.UserId, message);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
    }

    public async Task SendDayTimetable(User telegramUser)
    {
        var config = new Config<MainConfig>();
        var bot = new BotClient(config.Entries.Token);

        var userCollection = this._mongoService.Database.GetCollection<Models.User>("Users");
        var user = (await userCollection.FindAsync(u => u.UserId == telegramUser.Id)).ToList().First();
        if (user is null) return;
        
        //todo спилить
        await bot.SendMessageAsync(user.UserId, $"Дневное расписание временно недоступно");
        return;

        if (user.Teacher is null)
        {
            try
            {
                await bot.SendMessageAsync(user.UserId, "Вы еще не выбрали преподавателя");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return;
        }
        
        if (this.Timetables is null)
        {
            try
            {
                await bot.SendMessageAsync(user.UserId, $"У преподавателя {user.Teacher} нет пар");
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        foreach (var timetable in this.Timetables)
        {
            var message = timetable.Date + "\n\n";
            foreach (var dictionary in timetable.Table)
            {
                dictionary.TryGetValue(user.Teacher, out var lessons);
                if (lessons is null)
                {
                    message = $"У преподавателя {user.Teacher} нет пар на {timetable.Date}";
                    continue;
                }
                message += $"Преподаватель: {user.Teacher}\n";
                foreach (var lesson in lessons)
                {
                    message +=
                        $"Пара №{lesson.Index}\nГруппа: {lesson.Group.Trim()}\nКабинет: {lesson.Cabinet.Trim()}\n\n";
                }
            }

            try
            {
                await bot.SendMessageAsync(user.UserId, message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

    public async Task SendWeekTimetable(User telegramUser)
    {
        var config = new Config<MainConfig>();
        var bot = new BotClient(config.Entries.Token);

        var userCollection = this._mongoService.Database.GetCollection<Models.User>("Users");
        var user = (await userCollection.FindAsync(u => u.UserId == telegramUser.Id)).ToList().First();
        if (user is null) return;

        if (user.Teacher is null)
        {
            try
            {
                await bot.SendMessageAsync(user.UserId, "Вы еще не выбрали преподавателя");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return;
        }

        Image? image;
        try
        {
            image = await Image.LoadAsync($"./photo/{user.Teacher}.png");
        }
        catch
        {
            await bot.SendMessageAsync(user.UserId, "Увы, данный преподаватель не найдена");
            return;
        }

        using (var ms = new MemoryStream())
        {
            await image.SaveAsync(ms, new PngEncoder());

            try
            {
                await bot.SendPhotoAsync(user.UserId, new InputFile(ms.ToArray(), $"./photo/{user.Teacher}.png"));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
    
    public async Task SendNotificationsAboutWeekTimetable()
    {
        var config = new Config<MainConfig>();
        var bot = new BotClient(config.Entries.Token);

        var userCollection = this._mongoService.Database.GetCollection<Models.User>("Users");
        var users = (await userCollection.FindAsync(u => true)).ToList();
        if (users is null) return;

        foreach (var user in users)
        {
            if (user.Teacher is null || !user.Notifications) continue;
            
            try
            {
                await bot.SendMessageAsync(user.UserId, "Обновлена страница расписания на неделю");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
    
    // private async Task NewDayTimetableCheck()
    // {
    //     var url = "http://mgke.minsk.edu.by/ru/main.aspx?guid=3821";
    //     var web = new HtmlWeb();
    //     var doc = web.Load(url);
    //     if (this.LastDayHtmlContent == doc.DocumentNode.InnerHtml) return;
    //
    //     await this.ParseDayTimetables();
    // }
    
    private async Task NewWeekTimetableCheck()
    {
        var web = new HtmlWeb();
        var doc = web.Load(WeekUrl);
        if (this.LastWeekHtmlContent == doc.DocumentNode.InnerHtml) return;

        await this.ParseWeekTimetables();
        await this.SendNotificationsAboutWeekTimetable();
    }
}