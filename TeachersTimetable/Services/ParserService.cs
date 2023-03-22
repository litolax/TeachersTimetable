using HtmlAgilityPack;
using MongoDB.Driver;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using SixLabors.ImageSharp.Formats.Png;
using TeachersTimetable.Models;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableMethods.FormattingOptions;
using Telegram.BotAPI.AvailableTypes;
using TelegramBot_Timetable_Core.Config;
using Timer = System.Timers.Timer;
using User = Telegram.BotAPI.AvailableTypes.User;
using TelegramBot_Timetable_Core.Services;

namespace TeachersTimetable.Services;

public interface IParserService
{
    List<string> Teachers { get; }
    Task SendNewDayTimetables(string? teacher, bool all = false);
    Task SendDayTimetable(User telegramUser);
    Task ParseDayTimetables();
    Task ParseWeekTimetables();
    Task SendWeekTimetable(User telegramUser);
}

public class ParserService : IParserService
{
    private readonly IMongoService _mongoService;
    private readonly IBotService _botService;
    private readonly IConfig<MainConfig> _config;

    private const string WeekUrl =
        "https://mgkct.minskedu.gov.by/персоналии/преподавателям/расписание-занятий-на-неделю";

    private const string DayUrl =
        "https://mgkct.minskedu.gov.by/персоналии/преподавателям/расписание-занятий-на-день";

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

    private List<Timetable> TempTimetable { get; set; } = new();
    private List<Timetable> Timetable { get; set; } = new();

    private string LastDayHtmlContent { get; set; }
    private string LastWeekHtmlContent { get; set; }

    private bool _weekParseStarted;
    private bool _dayParseStarted;

    public ParserService(IMongoService mongoService, IBotService botService, IConfig<MainConfig> config)
    {
        this._mongoService = mongoService;
        this._botService = botService;
        this._config = config;

        var parseDayTimer = new Timer(150_000)
        {
            AutoReset = true, Enabled = true
        };
        parseDayTimer.Elapsed += (sender, args) =>
        {
            _ = this.NewDayTimetableCheck()
                .ContinueWith((t) => { Console.WriteLine(t.Exception?.InnerException); },
                    TaskContinuationOptions.OnlyOnFaulted);
        };

        var parseWeekTimer = new Timer(200_000)
        {
            AutoReset = true, Enabled = true
        };
        parseWeekTimer.Elapsed += (sender, args) =>
        {
            _ = this.NewWeekTimetableCheck()
                .ContinueWith((t) => { Console.WriteLine(t.Exception?.InnerException); },
                    TaskContinuationOptions.OnlyOnFaulted);
        };
    }

    public async Task ParseDayTimetables()
    {
        if (this._dayParseStarted) return;
        this._dayParseStarted = true;

        var driver = Utils.CreateChromeDriver();
        driver.Manage().Timeouts().PageLoad = new TimeSpan(0, 0, 20);

        driver.Navigate().GoToUrl(DayUrl);

        var content = driver.FindElement(By.Id("wrapperTables"));

        if (content is null)
        {
            this._dayParseStarted = false;
            driver.Dispose();
            return;
        }

        this.LastDayHtmlContent = content.Text;
        List<TeacherInfo> teacherInfos = new List<TeacherInfo>();
        this.TempTimetable.Clear();

        var teachersAndLessons = content.FindElements(By.XPath(".//div")).ToList();

        foreach (var teacher in this.Teachers)
        {
            try
            {
                for (var i = 1; i < teachersAndLessons.Count; i += 2)
                {
                    if (teachersAndLessons[i - 1].Text.Split('-')[0].Trim() != teacher) continue;
                    TeacherInfo teacherInfo = new TeacherInfo();
                    List<Lesson> lessons = new List<Lesson>();

                    var lessonsElements = teachersAndLessons[i].FindElements(By.XPath(".//table/tbody/tr")).ToList();

                    if (lessonsElements.Count < 1)
                    {
                        teacherInfo.Lessons = lessons;
                        teacherInfo.Name = teacher;
                        teacherInfos.Add(teacherInfo);
                        continue;
                    }

                    var lessonNumbers = lessonsElements[0].FindElements(By.XPath(".//th")).ToList();
                    var lessonNames = lessonsElements[1].FindElements(By.XPath(".//td")).ToList();
                    var lessonCabinets = lessonsElements[2].FindElements(By.XPath(".//td")).ToList();

                    for (int j = 0; j < lessonNumbers.Count; j++)
                    {
                        string cabinet = lessonCabinets.Count < lessonNumbers.Count && lessonCabinets.Count <= j
                            ? "-"
                            : lessonCabinets[j].Text;

                        lessons.Add(new Lesson()
                        {
                            Index = int.Parse(lessonNumbers[j].Text.Replace("№", "")),
                            Cabinet = cabinet,
                            Group = lessonNames[j].Text
                        });
                    }

                    teacherInfo.Name = teacher;
                    teacherInfo.Lessons = lessons;
                    teacherInfos.Add(teacherInfo);
                    break;
                }
            }
            catch (Exception e)
            {
                if (this._config.Entries.Administrators is not { } administrators) continue;
                var adminTelegramId = administrators.FirstOrDefault();
                if (adminTelegramId == default) continue;

                this._botService.SendMessage(new SendMessageArgs(adminTelegramId, e.Message));
                this._botService.SendMessage(new SendMessageArgs(adminTelegramId,
                    "Ошибка дневного расписания в учителе: " + teacher));
            }
        }

        driver.Dispose();

        foreach (var teacherInfo in teacherInfos)
        {
            int count = 0;
            teacherInfo.Lessons.Reverse();
            foreach (var lesson in teacherInfo.Lessons)
            {
                if (lesson.Group.Length < 1) count++;
                else break;
            }

            teacherInfo.Lessons.RemoveRange(0, count);
            teacherInfo.Lessons.Reverse();

            if (teacherInfo.Lessons.Count < 1) continue;

            for (int i = 0; i < teacherInfo.Lessons.First().Index - 1; i++)
            {
                teacherInfo.Lessons.Add(new Lesson()
                {
                    Cabinet = "-",
                    Group = "-",
                    Index = i + 1
                });
            }

            teacherInfo.Lessons = teacherInfo.Lessons.OrderBy(l => l.Index).ToList();
        }

        this.TempTimetable.Add(new()
        {
            TeacherInfos = new List<TeacherInfo>(teacherInfos)
        });
        teacherInfos.Clear();
        await this.ValidateTimetableHashes();
        this._dayParseStarted = false;
    }

    private async Task ValidateTimetableHashes()
    {
        if (this.TempTimetable.Any(e => e.TeacherInfos.Count == 0)) return;
        var tempTimetable = new List<Timetable>(this.TempTimetable);
        this.TempTimetable.Clear();

        if (tempTimetable.Count > this.Timetable.Count)
        {
            this.Timetable.Clear();
            this.Timetable = new List<Timetable>(tempTimetable);
            await this.SendNewDayTimetables(null, true);
            tempTimetable.Clear();
            return;
        }

        for (var i = 0; i < tempTimetable.Count; i++)
        {
            var tempDay = tempTimetable[i];
            var day = this.Timetable[i];

            for (int j = 0; j < tempDay.TeacherInfos.Count; j++)
            {
                var tempTeacher = tempDay.TeacherInfos[j].Name;

                var tempLessons = tempDay.TeacherInfos[j].Lessons;
                var teacherInfo = day.TeacherInfos.FirstOrDefault(g => g.Name == tempDay.TeacherInfos[j].Name);

                if (teacherInfo == default || tempLessons.Count != teacherInfo.Lessons.Count)
                {
                    _ = this.SendNewDayTimetables(tempTeacher);
                    continue;
                }

                for (var h = 0; h < tempLessons.Count; h++)
                {
                    var tempLesson = tempLessons[h];
                    var lesson = teacherInfo.Lessons[h];

                    if (tempLesson.GetHashCode() == lesson.GetHashCode()) continue;
                    _ = this.SendNewDayTimetables(tempTeacher);
                    break;
                }
            }
        }

        this.Timetable.Clear();
        this.Timetable = new List<Timetable>(tempTimetable);
        tempTimetable.Clear();
    }

    public async Task SendNewDayTimetables(string? teacher, bool all = false)
    {
        Console.WriteLine("Изменилось дневное расписание для: " + (all ? "Всех" : teacher));
        
        var userCollection = this._mongoService.Database.GetCollection<Models.User>("Users");
        var users = (await userCollection.FindAsync(u => all || u.Teacher == teacher)).ToList();

        foreach (var user in users.Where(user => user.Notifications && user.Teacher is not null))
        {
            if (this.Timetable.Count < 1)
            {
                this._botService.SendMessage(new SendMessageArgs(user.UserId, $"У {user.Teacher} нет пар"));
                continue;
            }

            var tasks = new List<Task>();

            foreach (var day in this.Timetable)
            {
                var message = day.Date + "\n";

                foreach (var teacherInfo in day.TeacherInfos.Where(teacherInfo =>
                             user.Teacher != null && user.Teacher == teacherInfo.Name))
                {
                    if (teacherInfo.Lessons.Count < 1)
                    {
                        message = $"У {teacherInfo.Name} нет пар";
                        continue;
                    }

                    message = this.CreateDayTimetableMessage(teacherInfo);
                }

                tasks.Add(this._botService.SendMessageAsync(new SendMessageArgs(user.UserId,
                    message.Length < 1 ? "У выбранного преподавателя нет пар" : message)
                {
                    ParseMode = ParseMode.Markdown
                }));
            }

            await Task.WhenAll(tasks);
        }
    }

    public async Task SendDayTimetable(User telegramUser)
    {
        var userCollection = this._mongoService.Database.GetCollection<Models.User>("Users");
        var user = (await userCollection.FindAsync(u => u.UserId == telegramUser.Id)).ToList().First();
        if (user is null) return;

        if (user.Teacher is null)
        {
            await this._botService.SendMessageAsync(new SendMessageArgs(user.UserId, "Вы еще не выбрали преподавателя"));
            return;
        }

        if (this.Timetable.Count < 1)
        {
            await this._botService.SendMessageAsync(new SendMessageArgs(user.UserId, $"У {user.Teacher} нет пар"));
            return;
        }

        foreach (var day in this.Timetable)
        {
            string message = day.Date + "\n";

            foreach (var teacherInfo in day.TeacherInfos.Where(teacherInfo => user.Teacher == teacherInfo.Name))
            {
                if (teacherInfo.Lessons.Count < 1)
                {
                    message = $"У {teacherInfo.Name} нет пар";
                    continue;
                }

                message = this.CreateDayTimetableMessage(teacherInfo);
            }

            await this._botService.SendMessageAsync(new SendMessageArgs(user.UserId,
                message.Length < 1 ? "У выбранного преподавателя нет пар" : message)
            {
                ParseMode = ParseMode.Markdown
            });
        }
    }
    
    private string CreateDayTimetableMessage(TeacherInfo teacherInfo)
    {
        string message = string.Empty;

        message += $"Преподаватель: *{teacherInfo.Name}*\n\n";

        foreach (var lesson in teacherInfo.Lessons)
        {
            var lessonName = Utils.HtmlTagsFix(lesson.Group).Replace('\n', ' ');
            var cabinet = Utils.HtmlTagsFix(lesson.Cabinet).Replace('\n', ' ');

            message +=
                $"*Пара: №{lesson.Index}*\n" +
                $"{(lessonName.Length < 2 ? "Предмет: -" : $"{lessonName}")}\n" +
                $"{(cabinet.Length < 2 ? "Каб: -" : $"Каб: {cabinet}")}\n\n";
        }

        return message;
    }

    public async Task ParseWeekTimetables()
    {
        if (this._weekParseStarted) return;
        this._weekParseStarted = true;

        var web = new HtmlWeb();
        var doc = web.Load(WeekUrl);

        var content = doc.DocumentNode.SelectNodes("//div/div/div/div/div/div").FirstOrDefault();
        if (content != default) this.LastWeekHtmlContent = content.InnerText;

        var teachers = doc.DocumentNode.SelectNodes("//h2");
        if (teachers is null)
        {
            this._weekParseStarted = false;
            return;
        }

        try
        {
            var newDate = doc.DocumentNode.SelectNodes("//h3")[0].InnerText.Trim();
            var dateDbCollection = this._mongoService.Database.GetCollection<Timetable>("WeekTimetables");
            var dbTables = (await dateDbCollection.FindAsync(d => true)).ToList();

            var driver = Utils.CreateChromeDriver();

            foreach (var teacher in this.Teachers)
            {
                try
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
                catch (Exception e)
                {
                    var entriesAdministrators = this._config.Entries.Administrators;
                    if (entriesAdministrators != null)
                    {
                        var adminTelegramId = entriesAdministrators.FirstOrDefault();
                        if (adminTelegramId == default) continue;

                        this._botService.SendMessage(new SendMessageArgs(adminTelegramId, e.Message));
                        this._botService.SendMessage(new SendMessageArgs(adminTelegramId,
                            "Ошибка в преподавателе: " + teacher));
                    }
                }
            }

            driver.Dispose();

            if (!dbTables.Exists(table => table.Date.Trim() == newDate))
            {
                await dateDbCollection.InsertOneAsync(new Timetable() { Date = newDate });
            }

            //await this.SendNotificationsAboutWeekTimetable();
            this._weekParseStarted = false;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            this._weekParseStarted = false;
        }
    }

    public async Task SendWeekTimetable(User telegramUser)
    {
        var userCollection = this._mongoService.Database.GetCollection<Models.User>("Users");
        var user = (await userCollection.FindAsync(u => u.UserId == telegramUser.Id)).ToList().First();
        if (user is null) return;

        if (user.Teacher is null)
        {
            await this._botService.SendMessageAsync(new SendMessageArgs(user.UserId,
                "Вы еще не выбрали преподавателя"));
            return;
        }

        Image? image;
        try
        {
            image = await Image.LoadAsync($"./photo/{user.Teacher}.png");
        }
        catch
        {
            await this._botService.SendMessageAsync(new SendMessageArgs(user.UserId,
                "Увы, данный преподаватель не найдена"));
            return;
        }

        using (var ms = new MemoryStream())
        {
            await image.SaveAsync(ms, new PngEncoder());

            await this._botService.SendPhotoAsync(new SendPhotoArgs(user.UserId,
                new InputFile(ms.ToArray(), $"./photo/{user.Teacher}.png")));
        }
    }

    public async Task SendNotificationsAboutWeekTimetable()
    {
        var userCollection = this._mongoService.Database.GetCollection<Models.User>("Users");
        var users = (await userCollection.FindAsync(u => true)).ToList();
        if (users is null) return;

        var tasks = (from user in users
            where user.Teacher is not null && user.Notifications
            select
                this._botService.SendMessageAsync(new SendMessageArgs(user.UserId,
                    "Обновлена страница расписания на неделю"))).Cast<Task>().ToList();

        await Task.WhenAll(tasks);
    }

    private Task NewDayTimetableCheck()
    {
        var driver = Utils.CreateChromeDriver();
        driver.Manage().Timeouts().PageLoad = new TimeSpan(0, 0, 20);

        driver.Navigate().GoToUrl(DayUrl);

        var content = driver.FindElement(By.Id("wrapperTables")).Text;

        driver.Dispose();

        if (this.LastDayHtmlContent == content) return Task.CompletedTask;

        _ = this.ParseDayTimetables().ContinueWith((t) => { Console.WriteLine(t.Exception?.InnerException); },
            TaskContinuationOptions.OnlyOnFaulted);

        return Task.FromResult(Task.CompletedTask);
    }

    private Task NewWeekTimetableCheck()
    {
        var web = new HtmlWeb();
        var doc = web.Load(WeekUrl);

        var content = doc.DocumentNode.SelectNodes("//div/div/div/div/div/div").FirstOrDefault();
        if (content == default) return Task.CompletedTask;;

        if (this.LastWeekHtmlContent == content.InnerText) return Task.CompletedTask;

        _ = this.ParseWeekTimetables().ContinueWith((t) => { Console.WriteLine(t.Exception?.InnerException); },
            TaskContinuationOptions.OnlyOnFaulted);
        
        return Task.CompletedTask;
    }
}