using System.Globalization;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using TeachersTimetable.Models;
using Size = System.Drawing.Size;

namespace TeachersTimetable.Services;

public static class Utils
{
    public static string HtmlTagsFix(string input)
    {
        return Regex.Replace(input, "<[^>]+>|&nbsp;", "").Trim();
    }

    public static void ModifyUnnecessaryElementsOnWebsite(FirefoxDriver driver)
    {
        var container = driver.FindElement(By.ClassName("main"));
        driver.ExecuteScript("arguments[0].style='width: 100%; border-top: none'", container);

        var header = driver.FindElement(By.Id("header"));
        driver.ExecuteScript("arguments[0].style='display: none'", header);

        var footer = driver.FindElement(By.Id("footer"));
        driver.ExecuteScript("arguments[0].style='display: none'", footer);

        var breadcrumbs = driver.FindElement(By.ClassName("breadcrumbs"));
        driver.ExecuteScript("arguments[0].style='display: none'", breadcrumbs);

        var pageShareButtons = driver.FindElement(By.ClassName("page_share_buttons"));
        driver.ExecuteScript("arguments[0].style='display: none'", pageShareButtons);

        var all = driver.FindElement(By.CssSelector("*"));
        driver.ExecuteScript("arguments[0].style='overflow-y: hidden; overflow-x: hidden'", all);


        driver.ExecuteScript("arguments[0].style='display : none'", driver.FindElement(By.TagName("h1")));
        driver.Manage().Window.Size = new Size(1920, container.Size.Height - 30);
    }

    public static string CreateDayTimetableMessage(TeacherInfo teacherInfo)
    {
        var message = string.Empty;

        message += $"Преподаватель: *{teacherInfo.Name}*\n\n";

        foreach (var lesson in teacherInfo.Lessons)
        {
            var lessonName = HtmlTagsFix(lesson.Group).Replace('\n', ' ');
            var cabinet = HtmlTagsFix(lesson.Cabinet).Replace('\n', ' ');

            message +=
                $"*Пара: №{lesson.Index}*\n" +
                $"{(lessonName.Length < 2 ? "Предмет: -" : $"{lessonName}")}\n" +
                $"{(cabinet.Length < 2 ? "Каб: -" : $"Каб: {cabinet}")}\n\n";
        }

        return message;
    }


    public static void HideTeacherElements(FirefoxDriver driver, IEnumerable<IWebElement> elements)
    {
        foreach (var element in elements)
        {
            driver.ExecuteScript("arguments[0].style='display: none;'", element);
        }
    }


    public static void ShowTeacherElements(FirefoxDriver driver, IEnumerable<IWebElement> elements)
    {
        foreach (var element in elements)
        {
            driver.ExecuteScript("arguments[0].style='display: block;'", element);
        }
    }

    public static DateTime?[]? ParseDateTimeWeekInterval(string interval)
    {
        var weekInterval = new DateTime?[2];
        var days = interval.Split('-');
        if (days.Length != 2) return null;
        for (var i = 0; i < days.Length; i++)
        {
            weekInterval[i] = ParseDateTime(days[i]);
            if (weekInterval[i] is null) return null;
        }

        return weekInterval;
    }

    public static bool IsDateBelongsToInterval(DateTime? date, DateTime?[]? interval) => date is not null &&
        interval is not null && date.Value.Date >= interval?[0]?.Date && date.Value.Date <= interval[1]?.Date;

    public static DateTime? ParseDateTime(string? date, string? format = "dd.MM.yyyy")
    {
        if (date is not null && DateTime.TryParseExact(date.Trim(), format, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dayTime)) return dayTime;
        return null;
    }
    
    public static string GetTeachersString(string?[] teachers) => string.Join(", ", teachers);
}