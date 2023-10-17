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
        Task<Models.User?> CreateAccount(User telegramUser);
        Task<bool> ChangeTeacher(User telegramUser, string? teacher);
        Task UpdateNotificationsStatus(User telegramUser);
    }

    public class AccountService : IAccountService
    {
        private readonly IMongoService _mongoService;
        private readonly IParseService _parseService;
        private readonly IBotService _botService;

        public AccountService(IMongoService mongoService, IParseService parseService, IBotService botService)
        {
            this._mongoService = mongoService;
            this._parseService = parseService;
            this._botService = botService;
        }

        public async Task<Models.User?> CreateAccount(User telegramUser)
        {
            var userCollection = this._mongoService.Database.GetCollection<Models.User>("Users");

            var users = (await userCollection.FindAsync(u => u.UserId == telegramUser.Id)).ToList();
            if (users.Count >= 1) return null;

            var user = new Models.User(telegramUser.Id, telegramUser.Username, telegramUser.FirstName,
                telegramUser.LastName) { Id = ObjectId.GenerateNewId() };

            await userCollection.InsertOneAsync(user);
            return user;
        }

        public async Task<bool> ChangeTeacher(User telegramUser, string? teacherName)
        {
            if (teacherName is null) return false;
            var teacherNames = teacherName.Split(',', ';', StringSplitOptions.RemoveEmptyEntries);
            teacherNames = teacherNames.Length > 5 ? teacherNames[..5] : teacherNames;
            for (var i = 0; i < teacherNames.Length; i++)
            {
                teacherNames[i] = teacherNames[i].Trim();
            }

            var correctTeacherNames = this._parseService.Teachers.Where(g => teacherNames.Any(teacher =>
                g.ToLower().Trim().Contains(teacher.ToLower().Trim()))).ToArray();

            if (correctTeacherNames is null || correctTeacherNames.Length == 0)
            {
                await this._botService.SendMessageAsync(new SendMessageArgs(telegramUser.Id,
                    $"Преподовател{(teacherNames.Length == 0 ? 'ь' : 'и')} {Utils.GetTeachersString(teacherNames)} не найден{(teacherNames.Length == 0 ? string.Empty : "ы")}"));
                return false;
            }

            var userCollection = this._mongoService.Database.GetCollection<Models.User>("Users");
            var user = (await userCollection.FindAsync(u => u.UserId == telegramUser.Id)).ToList().First() ??
                       await this.CreateAccount(telegramUser);

            user!.Teachers = correctTeacherNames;
            var update = Builders<Models.User>.Update.Set(u => u.Teachers, user.Teachers);
            await userCollection.UpdateOneAsync(u => u.UserId == telegramUser.Id, update);

            await this._botService.SendMessageAsync(new SendMessageArgs(telegramUser.Id, correctTeacherNames.Length == 1
                ? $"Вы успешно подписались на расписание преподавателя {correctTeacherNames[0]}"
                : $"Вы успешно подписались на расписание преподавателей: {Utils.GetTeachersString(correctTeacherNames)}"
            ));

            return true;
        }

        public async Task UpdateNotificationsStatus(User telegramUser)
        {
            var userCollection = this._mongoService.Database.GetCollection<Models.User>("Users");
            var user = (await userCollection.FindAsync(u => u.UserId == telegramUser.Id)).ToList().First() ??
                       await this.CreateAccount(telegramUser);

            if (user is null) return;

            if (user.Teachers is null)
            {
                this._botService.SendMessage(new SendMessageArgs(telegramUser.Id,
                    $"Перед оформлением подписки на рассылку необходимо выбрать преподавателя   "));
                return;
            }

            user.Notifications = !user.Notifications;
            var update = Builders<Models.User>.Update.Set(u => u.Notifications, user.Notifications);
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
                        user.Notifications
                            ? new KeyboardButton("Отписаться от рассылки")
                            : new KeyboardButton("Подписаться на рассылку")
                    }
                },
                ResizeKeyboard = true,
                InputFieldPlaceholder = "Выберите действие"
            };

            this._botService.SendMessage(new SendMessageArgs(telegramUser.Id,
                user.Notifications
                    ? $"Вы успешно подписались на расписание преподавателя {user.Teachers}"
                    : $"Вы успешно отменили подписку на расписание преподавателя {user.Teachers}")
            {
                ReplyMarkup = keyboard
            });
        }
    }
}