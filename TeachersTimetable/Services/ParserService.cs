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
    Task SendNewDayTimetables(string? teacher, bool firstStart, bool all = false);
    Task SendDayTimetable(User telegramUser);
    Task ParseDayTimetables(bool firstStart = false);
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
        "Астрейко Е. Ю.",
        "Бабер А. И.",
        "Барсукова Е. А.",
        "Барсукова Н. В.",
        "Белобровик А. А.",
        "Берговина А. В.",
        "Богдановская О. Н.",
        "Босянок Г. Ф.",
        "Бровка Д. С.",
        "Будник Е. А.",
        "Вайтович И. М.",
        "Витебская Е. С.",
        "Волошин В. В.",
        "Воронько С. В.",
        "Вострикова Т. С.",
        "Гаврилович Д. И.",
        "Галенко Е. Л.",
        "Галицкий М. И.",
        "Герасюк В. В.",
        "Гриневич П. Р.",
        "Громыко Н. К.",
        "Дзевенская Р. И.",
        "Дорц Н. А.",
        "Дудко А. Р.",
        "Жарский В. А.",
        "Жартун А. С.",
        "Жукова Т. Ю.",
        "Зайковская М. И.",
        "Звягина Д. Ч.",
        "Зеленкевич Е. А.",
        "Калацкая Т. Е.",
        "Камлюк В. С.",
        "Касперович С. А.",
        "Киселёв В. Д.",
        "Кислюк В. Е.",
        "Козел А. А.",
        "Козел Г. В.",
        "Колинко Н. Г.",
        "Кондратин И. Н.",
        "Кохно Т. А.",
        "Красовская А. В.",
        "Кулецкая Ю. Н.",
        "Кульбеда М. П.",
        "Лебедкина Н. В.",
        "Левонюк Е. А.",
        "Леус Ж. В.",
        "Липень А. В.",
        "Лихачева О. П.",
        "Лозовик И. В.",
        "Магаревич Е. А.",
        "Макаренко Е. В.",
        "Мурашко А. В.",
        "Немцева Н. А.",
        "Оскерко В. С.",
        "Паршаков Е. Д.",
        "Перепелкин А. М.",
        "Пешкова Г. Д.",
        "Плаксин Е. Б.",
        "Поклад Т. И.",
        "Полуйко А. М.",
        "Попеня Е. Э.",
        "Потапчик И. Г.",
        "Прокопович М. Е.",
        "Протасеня А. О.",
        "Пугач А. И.",
        "Пуршнев А. В.",
        "Русинская С. А.",
        "Сабанов А. А.",
        "Самарская Н. В.",
        "Самохвал Н. Н.",
        "Северин А. В.",
        "Семенова Л. Н.",
        "Сергун Т. С.",
        "Скобля Я. Э.",
        "Сотникова О. А.",
        "Стома Р. Н.",
        "Стушкевич Е. П.",
        "Тарасевич А. В.",
        "Тарасова Е. И.",
        "Тихонович Н. В.",
        "Усикова Л. Н.",
        "Федкевич Д. А.",
        "Фетисова Ю. Б.",
        "Филипцова Е. В.",
        "Харевская Е. Т.",
        "Хомченко И. И.",
        "Чертков М. Д.",
        "Шавейко А. А.",
        "Шеметов И. В.",
        "Щербич Е. В.",
        "Щуко О. И.",
    };

    private static List<Timetable> TempTimetable { get; set; } = new();
    private static List<Timetable> Timetable { get; set; } = new();
    private static Dictionary<string, Image> Images { get; set; } = new();

    private static string LastDayHtmlContent { get; set; }
    private static string LastWeekHtmlContent { get; set; }

    private static bool _weekParseStarted;
    private static bool _dayParseStarted;

    public ParserService(IMongoService mongoService, IBotService botService, IConfig<MainConfig> config)
    {
        this._mongoService = mongoService;
        this._botService = botService;
        this._config = config;

        var parseTimer = new Timer(1_000_000)
        {
            AutoReset = true, Enabled = true
        };

        parseTimer.Elapsed += async (sender, args) =>
        {
            try
            {
                await this.NewTimetableCheck();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        };
    }

    public async Task ParseDayTimetables(bool firstStart = false)
    {
        Console.WriteLine("Начато дневное расписание");

        var driver = ChromeService.Driver;
        
        driver.Navigate().GoToUrl(DayUrl);

        var content = driver.FindElement(By.Id("wrapperTables"));

        if (content is null)
        {
            return;
        }

        LastDayHtmlContent = content.Text;
        List<TeacherInfo> teacherInfos = new List<TeacherInfo>();
        TempTimetable.Clear();

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

        // this._tempTimetable.Add(new()
        // {
        //     TeacherInfos = new List<TeacherInfo>(teacherInfos)
        // });
        Timetable.Clear();
        Timetable.Add(new()
        {
            TeacherInfos = new List<TeacherInfo>(teacherInfos)
        });
        teacherInfos.Clear();
        //await this.ValidateTimetableHashes(firstStart);
        Console.WriteLine("Завершено дневное расписание");
    }

    // private async Task ValidateTimetableHashes(bool firstStart)
    // {
    //     if (this._tempTimetable.Any(e => e.TeacherInfos.Count == 0)) return;
    //     
    //     if (this._tempTimetable.Count > this._timetable.Count)
    //     {
    //         this._timetable.Clear();
    //         this._timetable = new List<Timetable>(this._tempTimetable);
    //         await this.SendNewDayTimetables(null, firstStart, true);
    //         this._tempTimetable.Clear();
    //         return;
    //     }
    //
    //     List<string> changedTeachers = new();
    //     for (var i = 0; i < this._tempTimetable.Count; i++)
    //     {
    //         var tempDay = this._tempTimetable[i];
    //         var day = this._timetable[i];
    //
    //         for (int j = 0; j < tempDay.TeacherInfos.Count; j++)
    //         {
    //             var tempTeacher = tempDay.TeacherInfos[j].Name;
    //
    //             var tempLessons = tempDay.TeacherInfos[j].Lessons;
    //             var teacherInfo = day.TeacherInfos.FirstOrDefault(g => g.Name == tempDay.TeacherInfos[j].Name);
    //
    //             if (teacherInfo == default || tempLessons.Count != teacherInfo.Lessons.Count)
    //             {
    //                 changedTeachers.Add(tempTeacher);
    //                 continue;
    //             }
    //
    //             for (var h = 0; h < tempLessons.Count; h++)
    //             {
    //                 var tempLesson = tempLessons[h];
    //                 var lesson = teacherInfo.Lessons[h];
    //
    //                 if (tempLesson.GetHashCode() == lesson.GetHashCode()) continue;
    //                 changedTeachers.Add(tempTeacher);
    //                 break;
    //             }
    //         }
    //     }
    //
    //     this._timetable.Clear();
    //     this._timetable = new List<Timetable>(this._tempTimetable);
    //     this._tempTimetable.Clear();
    //     
    //     changedTeachers.ForEach(teacher =>
    //     {
    //         _ = this.SendNewDayTimetables(teacher.ToString(), firstStart);
    //     });
    // }

    public async Task SendNewDayTimetables(string? teacher, bool firstStart, bool all = false)
    {
        if (firstStart) return;

        if (this._config.Entries.Administrators is { } administrators)
        {
            var adminTelegramId = administrators.FirstOrDefault();
            if (adminTelegramId != default)
            {
                this._botService.SendMessage(new SendMessageArgs(adminTelegramId,
                    "Изменилось дневное расписание учителей для: " + (all ? "Всех" : teacher)));
            }
        }

        var userCollection = this._mongoService.Database.GetCollection<Models.User>("Users");
        var users = (await userCollection.FindAsync(u => all || u.Teacher == teacher)).ToList();

        foreach (var user in users.Where(user => user.Notifications && user.Teacher is not null))
        {
            if (Timetable.Count < 1)
            {
                this._botService.SendMessage(new SendMessageArgs(user.UserId, $"У {user.Teacher} нет пар"));
                continue;
            }

            var tasks = new List<Task>();

            foreach (var day in Timetable)
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
                    message.Trim().Length <= 1 ? "У выбранного преподавателя нет пар" : message)
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
            await this._botService.SendMessageAsync(new SendMessageArgs(user.UserId,
                "Вы еще не выбрали преподавателя"));
            return;
        }

        if (Timetable.Count < 1)
        {
            await this._botService.SendMessageAsync(new SendMessageArgs(user.UserId, $"У {user.Teacher} нет пар"));
            return;
        }

        foreach (var day in Timetable)
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
                message.Trim().Length <= 1 ? "У выбранного преподавателя нет пар" : message)
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
        Console.WriteLine("Начато недельное расписание");

        var driver = ChromeService.Driver;
        driver.Navigate().GoToUrl(WeekUrl);

        var content = driver.FindElement(By.ClassName("entry")).Text;
        if (content != default) LastWeekHtmlContent = content;

        try
        {
            foreach (var teacher in this.Teachers)
            {
                try
                {
                    driver.Navigate().GoToUrl($"{WeekUrl}?teacher={teacher.Replace(" ", "+")}");

                    Utils.ModifyUnnecessaryElementsOnWebsite(ref driver);

                    var element = driver.FindElement(By.TagName("h2"));
                    if (element == default) continue;

                    var actions = new Actions(driver);
                    actions.MoveToElement(element).Perform();

                    var screenshot = (driver as ITakesScreenshot).GetScreenshot();
                    if (Images.TryGetValue(teacher, out var oldImage))
                    {
                        oldImage.Dispose();
                        Images.Remove(teacher);
                    }

                    var image = Image.Load(screenshot.AsByteArray);
                    image.Mutate(x => x.Resize((int)(image.Width / 1.5), (int)(image.Height / 1.5)));

                    Images.Add(teacher, image);
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
            
            // if (!dbTables.Exists(table => table.Date.Trim() == newDate))
            // {
            //     await dateDbCollection.InsertOneAsync(new Timetable() { Date = newDate });
            // }

            //await this.SendNotificationsAboutWeekTimetable();
            Console.WriteLine("Завершено недельное расписание");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
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

        Images.TryGetValue(user.Teacher, out var image);

        if (image is null)
        {
            this._botService.SendMessage(new SendMessageArgs(user.UserId, "Увы, данный преподаватель не найден"));
            return;
        }

        using (var ms = new MemoryStream())
        {
            await image.SaveAsync(ms, new PngEncoder());

            this._botService.SendPhoto(new SendPhotoArgs(user.UserId,
                new InputFile(ms.ToArray(), $"Teacher - {user.Teacher}")));

            await ms.DisposeAsync();
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

    private async Task NewTimetableCheck()
    {
        bool parseDay = false, parseWeek = false;
        var driver = ChromeService.Driver;

        //Day
        driver.Navigate().GoToUrl(DayUrl);
        var contentElement = driver.FindElement(By.Id("wrapperTables"));
        bool emptyContent = driver.FindElements(By.XPath(".//div")).ToList().Count < 5;


        if (!emptyContent && LastDayHtmlContent != contentElement.Text)
        {
            parseDay = true;
        }

        driver.Navigate().GoToUrl(WeekUrl);

        var content = driver.FindElement(By.ClassName("entry")).Text;

        if (content != default && LastWeekHtmlContent != content)
        {
            parseWeek = true;
        }

        try
        {
            if (parseDay) await this.ParseDayTimetables();
            if (parseWeek) await this.ParseWeekTimetables();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
