using MongoDB.Driver;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableMethods.FormattingOptions;
using Telegram.BotAPI.AvailableTypes;
using TelegramBot_Timetable_Core.Services;
using File = System.IO.File;

namespace TeachersTimetable.Services;

public interface IDistributionService
{
    Task SendWeek(User telegramUser);
    Task SendDayTimetable(User telegramUser);
    Task SendDayTimetable(Models.User? user);
}

public class DistributionService : IDistributionService
{
    private readonly IBotService _botService;
    private readonly IMongoService _mongoService;

    public DistributionService(IBotService botService, IMongoService mongoService)
    {
        this._botService = botService;
        this._mongoService = mongoService;
    }

    public async Task SendWeek(User telegramUser)
    {
        var userCollection = this._mongoService.Database.GetCollection<Models.User>("Users");
        var user = (await userCollection.FindAsync(u => u.UserId == telegramUser.Id)).ToList().First();
        if (user is null) return;

        if (user.Teacher is null || !File.Exists($"./cachedImages/{user.Teacher}.png"))
        {
            await this._botService.SendMessageAsync(new SendMessageArgs(user.UserId,
                "Вы еще не выбрали преподавателя"));
            return;
        }

        var image = await Image.LoadAsync($"./cachedImages/{user.Teacher}.png");

        if (image is not { })
        {
            await this._botService.SendMessageAsync(new SendMessageArgs(user.UserId,
                "Увы, данный преподаватель не найден"));
            return;
        }

        using var ms = new MemoryStream();
        await image.SaveAsPngAsync(ms);

        await this._botService.SendPhotoAsync(new SendPhotoArgs(user.UserId,
            new InputFile(ms.ToArray(), $"Teacher - {user.Teacher}")));
    }

    public async Task SendDayTimetable(User telegramUser)
    {
        var userCollection = this._mongoService.Database.GetCollection<Models.User>("Users");
        var user = (await userCollection.FindAsync(u => u.UserId == telegramUser.Id)).ToList().First();
        await this.SendDayTimetable(user);
    }

    public async Task SendDayTimetable(Models.User? user)
    {
        if (user is null) return;

        if (user.Teacher is null)
        {
            await this._botService.SendMessageAsync(new SendMessageArgs(user.UserId,
                "Вы еще не выбрали преподавателя"));
            return;
        }

        if (ParseService.Timetable.Count < 1)
        {
            await this._botService.SendMessageAsync(new SendMessageArgs(user.UserId, $"У {user.Teacher} нет пар"));
            return;
        }

        foreach (var day in ParseService.Timetable)
        {
            var message = string.Empty;

            foreach (var teacherInfo in day.TeacherInfos.Where(teacherInfo => user.Teacher == teacherInfo.Name))
            {
                if (teacherInfo.Lessons.Count < 1)
                {
                    message = $"У {teacherInfo.Name} нет пар";
                    continue;
                }

                message = $"День - {day.Date}\n" + Utils.CreateDayTimetableMessage(teacherInfo);
            }

            await this._botService.SendMessageAsync(new SendMessageArgs(user.UserId,
                message.Trim().Length <= 1 ? "У выбранного преподавателя нет пар" : message)
            {
                ParseMode = ParseMode.Markdown
            });
        }
    }
}