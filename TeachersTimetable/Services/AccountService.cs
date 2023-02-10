using MongoDB.Bson;
using MongoDB.Driver;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using User = Telegram.BotAPI.AvailableTypes.User;
using TelegramBot_Timetable_Core.Services;

namespace TeachersTimetable.Services
{
    public interface IAccountService
    {
        Task<TelegramBot_Timetable_Core.Models.User?> CreateAccount(User telegramUser);
        Task<bool> ChangeTeacher(User telegramUser, string? teacher);
        Task UpdateNotificationsStatus(User telegramUser);
    }

    public class AccountService : IAccountService
    {
        private readonly IMongoService _mongoService;
        private readonly IParserService _parserService;
        private readonly IBotService _botService;

        public AccountService(IMongoService mongoService, IParserService parserService, IBotService botService)
        {
            this._mongoService = mongoService;
            this._parserService = parserService;
            this._botService = botService;
        }

        public async Task<TelegramBot_Timetable_Core.Models.User?> CreateAccount(User telegramUser)
        {
            var userCollection = this._mongoService.Database.GetCollection<TelegramBot_Timetable_Core.Models.User>("Users");

            var users = (await userCollection.FindAsync(u => u.UserId == telegramUser.Id)).ToList();
            if (users.Count >= 1) return null;

            var user = new TelegramBot_Timetable_Core.Models.User(telegramUser.Id, telegramUser.Username, telegramUser.FirstName,
                telegramUser.LastName) { Id = ObjectId.GenerateNewId() };

            await userCollection.InsertOneAsync(user);
            return user;
        }

        public async Task<bool> ChangeTeacher(User telegramUser, string? teacherName)
        {
            if (teacherName is null) return false;

            var correctTeacherName = string.Empty;
            foreach (var teacher in this._parserService.Teachers)
            {
                if (!teacher.ToLower().Trim().Contains(teacherName.ToLower().Trim())) continue;
                correctTeacherName = teacher.Trim();
                break;
            }

            if (correctTeacherName == string.Empty)
            {
                this._botService.SendMessage(new SendMessageArgs(telegramUser.Id, $"Преподаватель не найден"));
                return false;
            }

            var userCollection = this._mongoService.Database.GetCollection<TelegramBot_Timetable_Core.Models.User>("Users");
            var user = (await userCollection.FindAsync(u => u.UserId == telegramUser.Id)).ToList().First() ??
                       await CreateAccount(telegramUser);

            user!.Teacher = correctTeacherName;
            var update = Builders<TelegramBot_Timetable_Core.Models.User>.Update.Set(u => u.Teacher, user.Teacher);
            await userCollection.UpdateOneAsync(u => u.UserId == telegramUser.Id, update);

            this._botService.SendMessage(new SendMessageArgs(telegramUser.Id,
                $"Вы успешно подписались на расписание преподавателя {correctTeacherName}"));

            return true;
        }

        public async Task UpdateNotificationsStatus(User telegramUser)
        {
            var userCollection = this._mongoService.Database.GetCollection<TelegramBot_Timetable_Core.Models.User>("Users");
            var user = (await userCollection.FindAsync(u => u.UserId == telegramUser.Id)).ToList().First() ??
                       await CreateAccount(telegramUser);

            if (user is null) return;
            
            if (user.Teacher is null)
            {
                this._botService.SendMessage(new SendMessageArgs(telegramUser.Id,
                    $"Перед оформлением подписки на рассылку необходимо выбрать преподавателя"));
                return;
            }

            user.Notifications = !user.Notifications;
            var update = Builders<TelegramBot_Timetable_Core.Models.User>.Update.Set(u => u.Notifications, user.Notifications);
            await userCollection.UpdateOneAsync(u => u.UserId == telegramUser.Id, update);

            var keyboard = new ReplyKeyboardMarkup
            {
                Keyboard = new[]
                {
                    new[]
                    {
                        new KeyboardButton("Посмотреть расписание на день"),
                    },
                    new[]
                    {
                        new KeyboardButton("Посмотреть расписание на неделю"),
                    },
                    new[]
                    {
                        new KeyboardButton("Сменить преподавателя"),
                    },
                    new[]
                    {
                        user.Notifications ? new KeyboardButton("Отписаться от рассылки") : new KeyboardButton("Подписаться на рассылку")
                    }
                },
                ResizeKeyboard = true,
                InputFieldPlaceholder = "Выберите действие"
            };

            this._botService.SendMessage(new SendMessageArgs(telegramUser.Id, user.Notifications ? 
                $"Вы успешно подписались на расписание преподавателя {user.Teacher}" :
                $"Вы успешно отменили подписку на расписание преподавателя {user.Teacher}")
            {
                ReplyMarkup = keyboard
            });
        }
    }
}