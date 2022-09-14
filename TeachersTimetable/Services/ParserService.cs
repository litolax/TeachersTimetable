using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using HtmlAgilityPack;
using MongoDB.Driver;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.Extensions;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Png;
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
    List<string> Teachers { get; set; }
    Task SendNewDayTimetables();
    Task SendDayTimetable(User telegramUser);
    Task ParseDayTimetables();
    Task ParseWeekTimetables();
    Task SendWeekTimetable(User telegramUser);
}

public class ParserService : IParserService
{
    private readonly IMongoService _mongoService;
    public List<string> Teachers { get; set; } = new();
    public List<Timetable>? Timetables { get; set; } = new();

    public ParserService(IMongoService mongoService)
    {
        this._mongoService = mongoService;
        var parseDayTimer = new Timer(300000)
        {
            AutoReset = true, Enabled = true
        };
        parseDayTimer.Elapsed += async (sender, args) =>
        {
            await this.ParseDayTimetables();
        };
        
        var parseWeekTimer = new Timer(2000000)
        {
            AutoReset = true, Enabled = true
        };
        parseDayTimer.Elapsed += async (sender, args) =>
        {
            await this.ParseWeekTimetables();
        };
    }

    public async Task ParseDayTimetables()
    {
        var timetablesCollection = this._mongoService.Database.GetCollection<Timetable>("DayTimetables");
        var dbTables = (await timetablesCollection.FindAsync(table => true)).ToList();
        var url = "http://mgke.minsk.edu.by/ru/main.aspx?guid=3821";
        var web = new HtmlWeb();
        var doc = web.Load(url);
        var tables = doc.DocumentNode.SelectNodes("//table");
        this.Teachers = new List<string>();
        this.Timetables = new List<Timetable>();
        if (tables is null)
        {
            this.Timetables = null;
            return;
        } 
        foreach (var table in tables)
        {
            var teachersAndLessons = new Dictionary<string, List<Lesson>>();
            var t = table.SelectNodes("./tbody/tr");
            if (t is null)
            {
                t = table.SelectNodes("./thead/tr");
                if (t is null) continue;
            }
            for (int i = 0; i < t.Count; i++)
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


                this.Teachers.Add(t[i].ChildNodes[3].InnerText.Trim());
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
        var url = "http://mgke.minsk.edu.by/ru/main.aspx?guid=3811";
        var web = new HtmlWeb();
        var doc = web.Load(url);

        var teachers = doc.DocumentNode.SelectNodes("//h2");
        if (teachers is null) return;
        
        var newDate = doc.DocumentNode.SelectNodes("//h3")[0];
        var dateDbCollection = this._mongoService.Database.GetCollection<Timetable>("WeekTimetables");
        var dbTables = (await dateDbCollection.FindAsync(d => true)).ToList();

        ChromeOptions options = new ChromeOptions();
        options.AddArgument("headless");
        options.AddArgument("--no-sandbox");
        var driver = new ChromeDriver(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), options);
        driver.Manage().Window.Size = new Size(1100, 1100);

        driver.Navigate().GoToUrl("http://mgke.minsk.edu.by/ru/main.aspx?guid=3811");

        var elements = driver.FindElements(By.TagName("h2"));

        for (int i = 0; i < elements.Count; i++)
        {
            Actions actions = new Actions(driver);
            if (i + 1 < elements.Count) actions.MoveToElement(elements[i + 1]).Perform();
            else
            {
                actions.MoveToElement(elements[i]).Perform();
                for (int j = 0; j < 100; j++)
                {
                    actions.SendKeys(Keys.Down);
                }
            }

            actions.Perform();

            var screenshot = (driver as ITakesScreenshot).GetScreenshot();
            screenshot.SaveAsFile($"./photo/{teachers[i].ChildNodes[0].InnerHtml.Remove(0, 16)}.png",
                ScreenshotImageFormat.Png);
        }

        driver.Close();
        driver.Quit();
        
        if (!dbTables.Exists(table => table.Date.Trim() == newDate.InnerText.Trim()))
        {
            await dateDbCollection.InsertOneAsync(new Timetable() {Date = newDate.InnerText.Trim()});
            await this.SendNotificationsAboutWeekTimetable();
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


        var image = await SixLabors.ImageSharp.Image.LoadAsync($"./photo/{user.Teacher}.png");
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
                await bot.SendMessageAsync(user.UserId, "Обновлено расписание на неделю");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}