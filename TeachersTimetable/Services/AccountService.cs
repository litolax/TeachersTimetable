using MongoDB.Bson;
using MongoDB.Driver;
using TeachersTimetable.Config;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using User = Telegram.BotAPI.AvailableTypes.User;

namespace TeachersTimetable.Services
{
    public interface IAccountService
    {
        Task<Models.User?> CreateAccount(User telegramUser);
        Task ChangeTeacher(User telegramUser, string teacher);
        Task SubscribeNotifications(User telegramUser);
        Task UnSubscribeNotifications(User telegramUser);
    }

    public class AccountService : IAccountService
    {
        private readonly IMongoService _mongoService;
        private readonly IParserService _parserService;

        public AccountService(IMongoService mongoService, IParserService parserService)
        {
            this._mongoService = mongoService;
            this._parserService = parserService;
        }

        public async Task<Models.User?> CreateAccount(User telegramUser)
        {
            var userCollection = this._mongoService.Database.GetCollection<Models.User>("Users");

            var users = (await userCollection.FindAsync(u => u.UserId == telegramUser.Id)).ToList();
            if (users.Count >= 1) return null;
            
            var user = new Models.User(telegramUser.Id, telegramUser.Username, telegramUser.FirstName,
                telegramUser.LastName) {Id = ObjectId.GenerateNewId()};
         
            
            await userCollection.InsertOneAsync(user);
            return user;
        }

        public async Task ChangeTeacher(User telegramUser, string teacherName)
        {
            var config = new Config<MainConfig>();
            var bot = new BotClient(config.Entries.Token);
            
            string correctTeacherName = string.Empty;
            foreach (var teacher in this._parserService.Teachers)
            {
                if (!teacher.ToLower().Trim().Contains(teacherName.ToLower().Trim())) continue;
                correctTeacherName = teacher.Trim();
                break;
            }

            if (correctTeacherName == string.Empty)
            {
                try
                {
                    await bot.SendMessageAsync(telegramUser.Id, $"Преподаватель не найден");
                    return;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            var userCollection = this._mongoService.Database.GetCollection<Models.User>("Users");
            var user = (await userCollection.FindAsync(u => u.UserId == telegramUser.Id)).ToList().First() ?? await CreateAccount(telegramUser);

            user!.Teacher = correctTeacherName;
            var update = Builders<Models.User>.Update.Set(u => u.Teacher, user.Teacher);
            await userCollection.UpdateOneAsync(u => u.UserId == telegramUser.Id, update);
            
            try
            {
                await bot.SendMessageAsync(telegramUser.Id, $"Вы успешно подписались на расписание преподавателя {correctTeacherName}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public async Task SubscribeNotifications(User telegramUser)
        {
            var config = new Config<MainConfig>();
            var bot = new BotClient(config.Entries.Token);

            var userCollection = this._mongoService.Database.GetCollection<Models.User>("Users");
            var user = (await userCollection.FindAsync(u => u.UserId == telegramUser.Id)).ToList().First() ?? await CreateAccount(telegramUser);
            
            if (user is null) return;
            if (user.Teacher is null)
            {
                try
                {
                    await bot.SendMessageAsync(telegramUser.Id, $"Перед оформлением подписки на рассылку необходимо выбрать преподавателя");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                return;
            }
            
            var update = Builders<Models.User>.Update.Set(u => u.Notifications, true);
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
                       new KeyboardButton("Отписаться от рассылки") 
                    }
                },
                ResizeKeyboard = true,
                InputFieldPlaceholder = "Выберите действие"
            };
            
            try
            {
                await bot.SendMessageAsync(telegramUser.Id, $"Вы успешно подписались на расписание преподавателя {user.Teacher}", replyMarkup: keyboard);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        
        public async Task UnSubscribeNotifications(User telegramUser)
        {
            var config = new Config<MainConfig>();
            var bot = new BotClient(config.Entries.Token);

            var userCollection = this._mongoService.Database.GetCollection<Models.User>("Users");
            var user = (await userCollection.FindAsync(u => u.UserId == telegramUser.Id)).ToList().First() ?? await CreateAccount(telegramUser);
            
            if (user is null) return;

            var update = Builders<Models.User>.Update.Set(u => u.Notifications, false);
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
                        new KeyboardButton("Подписаться на рассылку")   
                    }
                },
                ResizeKeyboard = true,
                InputFieldPlaceholder = "Выберите действие"
            };
            
            try
            {
                await bot.SendMessageAsync(telegramUser.Id, $"Вы успешно отменили подписку на расписание", replyMarkup: keyboard);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}