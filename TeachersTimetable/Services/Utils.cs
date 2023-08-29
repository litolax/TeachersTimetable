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
        driver.ExecuteScript("arguments[0].style='overflow-y: hidden; overflow-x: hidden;'", all);
        
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
}