using MongoDB.Driver;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using TeachersTimetable.Config;
using TeachersTimetable.Models;
using Telegram.BotAPI.AvailableMethods;
using TelegramBot_Timetable_Core.Config;
using TelegramBot_Timetable_Core.Services;
using Size = System.Drawing.Size;
using Timer = System.Timers.Timer;

namespace TeachersTimetable.Services;

public interface IParseService
{
    string[] Teachers { get; }
    static List<Timetable> Timetable { get; set; }
    Task UpdateTimetableTick();
}

public class ParseService : IParseService
{
    private readonly IMongoService _mongoService;
    private readonly IBotService _botService;
    private readonly IFirefoxService _firefoxService;
    private readonly IDistributionService _distributionService;
    private DateTime?[]? _weekInterval;
    private List<string> _thHeaders;

    private const string WeekUrl =
        "https://mgkct.minskedu.gov.by/персоналии/преподавателям/расписание-занятий-на-неделю";

    private const string DayUrl =
        "https://mgkct.minskedu.gov.by/персоналии/преподавателям/расписание-занятий-на-день";

    private const int DriverTimeout = 2000;

    public string[] Teachers { get; init; }
    public static List<Timetable> Timetable { get; set; } = new();
    private static string LastDayHtmlContent { get; set; }
    private static string LastWeekHtmlContent { get; set; }

    public ParseService(IMongoService mongoService, IBotService botService, IFirefoxService firefoxService,
        IDistributionService distributionService, IConfig<TeachersConfig> teachers)
    {
        this._mongoService = mongoService;
        this._botService = botService;
        this._firefoxService = firefoxService;
        this._distributionService = distributionService;
        this.Teachers = teachers.Entries.Teachers;
        Console.WriteLine("Teachers: " + teachers.Entries.Teachers.Length);
        if (!Directory.Exists("./cachedImages")) Directory.CreateDirectory("./cachedImages");

        var parseTimer = new Timer(1_000_000)
        {
            AutoReset = true, Enabled = true
        };

        parseTimer.Elapsed += async (_, _) =>
        {
            try
            {
                await this.UpdateTimetableTick();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        };
    }

    private async Task ParseDay()
    {
        Console.WriteLine("Start day parse");

        var teacherInfos = new List<TeacherInfo>();
        var (service, options, delay) = this._firefoxService.Create();
        var day = string.Empty;
        using (FirefoxDriver driver = new FirefoxDriver(service, options, delay))
        {
            driver.Manage().Timeouts().PageLoad.Add(TimeSpan.FromMinutes(2));
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));

            driver.Navigate().GoToUrl(DayUrl);
            Thread.Sleep(DriverTimeout);

            var content = driver.FindElement(By.XPath("//*[@id=\"wrapperTables\"]"));
            wait.Until(d => content.Displayed);
            if (content is null) return;

            var teachersAndLessons = content.FindElements(By.XPath(".//div")).ToList();
            var teacher = string.Empty;
            try
            {
                if (teachersAndLessons.Count > 0)
                {
                    day = teachersAndLessons[0].Text.Split('-')[1].Trim();
                    var tempDay =
                        _thHeaders.FirstOrDefault(th =>
                            th.Contains(day, StringComparison.InvariantCultureIgnoreCase)) ??
                        day;
                    var daytime = Utils.ParseDateTime(tempDay.Split(", ")[1].Trim());
                    if (daytime?.DayOfWeek is DayOfWeek.Saturday &&
                        !Utils.IsDateBelongsToInterval(daytime, _weekInterval))
                    {
                        Console.WriteLine("End parse day(next saturday)");
                        await this._botService.SendAdminMessageAsync(new SendMessageArgs(0,
                            "Detected next Saturday!" + tempDay));
                        return;
                    }

                    day = tempDay;
                }

                for (var i = 1; i < teachersAndLessons.Count; i += 2)
                {
                    var parsedTeacherName = teachersAndLessons[i - 1].Text.Split('-')[0].Trim();
                    teacher = this.Teachers.FirstOrDefault(t => t == parsedTeacherName);
                    if (teacher is null) continue;

                    var teacherInfo = new TeacherInfo();
                    var lessons = new List<Lesson>();
                    var lessonsElements =
                        teachersAndLessons[i].FindElements(By.XPath(".//table/tbody/tr")).ToList();

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

                    for (var j = 0; j < lessonNumbers.Count; j++)
                    {
                        var cabinet = lessonCabinets.Count < lessonNumbers.Count && lessonCabinets.Count <= j
                            ? "-"
                            : lessonCabinets[j].Text;

                        lessons.Add(new Lesson()
                        {
                            Index = int.Parse(lessonNumbers[j].Text.Replace("№", "")),
                            Cabinet = cabinet,
                            Group = lessonNames[j].Text.Replace("*", string.Empty)
                        });
                    }

                    teacherInfo.Name = teacher;
                    teacherInfo.Lessons = lessons;
                    teacherInfos.Add(teacherInfo);
                }
            }
            catch (Exception e)
            {
                _ = this._botService.SendAdminMessageAsync(new SendMessageArgs(0, e.Message));
                _ = this._botService.SendAdminMessageAsync(new SendMessageArgs(0,
                    "Ошибка дневного расписания у преподавателя: " + teacher));
            }
        }

        var notificationUsersList = new List<User>();
        var teacherUpdatedList = new List<string>();
        foreach (var teacherInfo in teacherInfos)
        {
            var count = 0;
            teacherInfo.Lessons.Reverse();
            foreach (var lesson in teacherInfo.Lessons)
            {
                if (lesson.Group.Length < 1) count++;
                else break;
            }

            teacherInfo.Lessons.RemoveRange(0, count);
            teacherInfo.Lessons.Reverse();

            var teacherInfoFromTimetable =
                Timetable.LastOrDefault()?.TeacherInfos.FirstOrDefault(t => t.Name == teacherInfo.Name);
            if (teacherInfo.Lessons.Count < 1)
            {
                if (teacherInfoFromTimetable?.Lessons is not null && teacherInfoFromTimetable.Lessons.Count > 0)
                    notificationUsersList.AddRange((await this._mongoService.Database.GetCollection<User>("Users")
                            .FindAsync(u =>
                                u.Teachers != null && u.Notifications && u.Teachers.Contains(teacherInfo.Name)))
                        .ToList());
                continue;
            }

            for (var i = 0; i < teacherInfo.Lessons.First().Index - 1; i++)
            {
                teacherInfo.Lessons.Add(new Lesson()
                {
                    Cabinet = "-",
                    Group = "-",
                    Index = i + 1
                });
            }

            teacherInfo.Lessons = teacherInfo.Lessons.OrderBy(l => l.Index).ToList();

            if (teacherInfoFromTimetable is null || teacherInfoFromTimetable.Equals(teacherInfo)) continue;
            teacherUpdatedList.Add(teacherInfo.Name);
            try
            {
                notificationUsersList.AddRange((await this._mongoService.Database.GetCollection<User>("Users")
                        .FindAsync(u => u.Teachers != null && u.Notifications && u.Teachers.Contains(teacherInfo.Name)))
                    .ToList());
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        if (teacherUpdatedList.Count != 0)
            _ = this._botService.SendAdminMessageAsync(new SendMessageArgs(0,
                $"There's been a schedule change with the teachers: {string.Join(',', teacherUpdatedList)}"));
        teacherUpdatedList.Clear();
        Timetable.Clear();
        Timetable.Add(new()
        {
            Date = day,
            TeacherInfos = new List<TeacherInfo>(teacherInfos)
        });
        teacherInfos.Clear();
        Console.WriteLine("End parse day");
        if (notificationUsersList.Count == 0) return;
        _ = Task.Run(() =>
        {
            try
            {
                foreach (var user in notificationUsersList)
                {
                    _ = this._distributionService.SendDayTimetable(user);
                }

                this._botService.SendAdminMessageAsync(new SendMessageArgs(0,
                    $"{day}:{notificationUsersList.Count} notifications sent"));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        });
    }

    private async Task ParseWeek()
    {
        Console.WriteLine("Start week parse");

        var (service, options, delay) = this._firefoxService.Create();
        using (FirefoxDriver driver = new FirefoxDriver(service, options, delay))
        {
            driver.Manage().Timeouts().PageLoad.Add(TimeSpan.FromMinutes(2));
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));

            driver.Navigate().GoToUrl(WeekUrl);

            var element = driver.FindElement(By.XPath("/html/body/div[1]/div[2]/div/div[2]/div[1]/div"));
            wait.Until(d => element.Displayed);
            Utils.ModifyUnnecessaryElementsOnWebsite(driver);

            if (element == default) return;
            var h2 =
                driver.FindElements(
                    By.XPath("/html/body/div[1]/div[2]/div/div[2]/div[1]/div/h2"));

            var h3 =
                driver.FindElements(
                    By.XPath("/html/body/div[1]/div[2]/div/div[2]/div[1]/div/h3"));
            var weekIntervalStr = h3[0].Text;
            var weekInterval = Utils.ParseDateTimeWeekInterval(weekIntervalStr);
            if (_weekInterval is null || !string.IsNullOrEmpty(weekIntervalStr) && _weekInterval != weekInterval &&
                _weekInterval[1] is not null && DateTime.Today == _weekInterval[1])
            {
                _weekInterval = weekInterval;
                Console.WriteLine("New interval is " + weekIntervalStr);
                await this._botService.SendAdminMessageAsync(new SendMessageArgs(0,
                    "New interval is " + weekIntervalStr));
            }

            var tempThHeaders =
                driver.FindElement(
                        By.XPath("/html/body/div[1]/div[2]/div/div[2]/div[1]/div/div[1]/table/tbody/tr[1]"))
                    .FindElements(By.TagName("th"));
            _thHeaders = new List<string>();
            foreach (var thHeader in tempThHeaders)
            {
                _thHeaders.Add(new string(thHeader.Text));
            }

            var table = driver.FindElements(By.XPath("/html/body/div[1]/div[2]/div/div[2]/div[1]/div/div"));
            Utils.HideTeacherElements(driver, h3);
            Utils.HideTeacherElements(driver, h2);
            Utils.HideTeacherElements(driver, table);

            for (var i = 0; i < h2.Count; i++)
            {
                var teacherH2 = h2[i];
                var parsedTeacher = string.Empty;
                var list = new List<IWebElement> { teacherH2, h3[i], table[i] };
                var teacher = string.Empty;
                try
                {
                    Utils.ShowTeacherElements(driver, list);
                    parsedTeacher = teacherH2.Text.Split('-')[1].Trim();
                    teacher = this.Teachers.First(t => t == parsedTeacher);
                    var actions = new Actions(driver);
                    actions.MoveToElement(element).Perform();
                    driver.Manage().Window.Size =
                        new Size(1920, driver.FindElement(By.ClassName("main")).Size.Height - 30);
                    var screenshot = (driver as ITakesScreenshot).GetScreenshot();
                    var image = Image.Load(screenshot.AsByteArray);
                    image.Mutate(x => x.Resize((int)(image.Width / 1.5), (int)(image.Height / 1.5)));
                    await image.SaveAsync($"./cachedImages/{teacher}.png");
                }
                catch (Exception e)
                {
                    await this._botService.SendAdminMessageAsync(new SendMessageArgs(0, e.Message));
                    await this._botService.SendAdminMessageAsync(new SendMessageArgs(0,
                        $"Ошибка в преподавателе: {teacher}  (parsed:{parsedTeacher})"));
                }
                finally
                {
                    Utils.HideTeacherElements(driver, list);
                }
            }
        }

        Console.WriteLine("End week parse");
    }

    public async Task UpdateTimetableTick()
    {
        try
        {
            Console.WriteLine("Start update tick");
            bool parseDay = false, parseWeek = false;
            var (service, options, delay) = this._firefoxService.Create();
            using (FirefoxDriver driver = new FirefoxDriver(service, options, delay))
            {
                //Day
                driver.Manage().Timeouts().PageLoad.Add(TimeSpan.FromMinutes(2));
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));

                driver.Navigate().GoToUrl(DayUrl);
                Thread.Sleep(DriverTimeout);

                var contentElement = driver.FindElement(By.XPath("//*[@id=\"wrapperTables\"]"));
                wait.Until(d => contentElement.Displayed);
                var emptyContent = driver.FindElements(By.XPath(".//div")).ToList().Count < 5;

                if (!emptyContent && LastDayHtmlContent != contentElement.Text)
                {
                    parseDay = true;
                    LastDayHtmlContent = contentElement.Text;
                }

                driver.Navigate().GoToUrl(WeekUrl);
                Thread.Sleep(DriverTimeout);

                var content = driver.FindElement(By.ClassName("entry"));
                wait.Until(d => content.Displayed);
                if (content != default && LastWeekHtmlContent != content.Text)
                {
                    parseWeek = true;
                    LastWeekHtmlContent = content.Text;
                }
            }

            if (parseWeek)
            {
                await this._botService.SendAdminMessageAsync(new SendMessageArgs(0, "Start parse week"));
                await this.ParseWeek();
                await this._botService.SendAdminMessageAsync(new SendMessageArgs(0, "End parse week"));
            }

            if (parseDay)
            {
                await this._botService.SendAdminMessageAsync(new SendMessageArgs(0, "Start parse day"));
                await this.ParseDay();
                await this._botService.SendAdminMessageAsync(new SendMessageArgs(0, "End parse day"));
            }

            Console.WriteLine("End update tick");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}