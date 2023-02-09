using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using TelegramBot_Timetable_Core;
using TelegramBot_Timetable_Core.Models;
using TelegramBot_Timetable_Core.Services;

namespace TeachersTimetable.Services
{
    public interface ICommandsService
    {
    }

    public class CommandsService : ICommandsService
    {
        private readonly IInterfaceService _interfaceService;
        private readonly IAccountService _accountService;
        private readonly IParserService _parserService;
        private readonly IMongoService _mongoService;
        private readonly IBotService _botService;

        public CommandsService(IInterfaceService interfaceService, IAccountService accountService,
            IParserService parserService, IMongoService mongoService, IBotService botService)
        {
            Core.OnMessageReceive += OnMessageReceive;

            this._interfaceService = interfaceService;
            this._accountService = accountService;
            this._parserService = parserService;
            this._mongoService = mongoService;
            this._botService = botService;
        }

        private async void OnMessageReceive(Message message)
        {
            if (message.From is not { } sender) return;
            var messageText = message.Text;

            var lastState = await this._mongoService.GetLastState(message.Chat.Id);

            if (lastState is not null && lastState == "changeTeacher")
            {
                await this._accountService.ChangeTeacher(sender, messageText);
                this._mongoService.RemoveState(message.Chat.Id);
            }

            switch (messageText)
            {
                case "/start":
                case "/menu":
                {
                    await this._interfaceService.OpenMainMenu(message);
                    break;
                }
                case "/help":
                {
                    this._botService.SendMessage(new SendMessageArgs(sender.Id,
                        $"Вы пользуетесь ботом, который поможет узнать Вам актуальное расписание преподавателей МГКЦТ.\nСоздатель @litolax"));
                    break;
                }
                case "/tos":
                {
                    this._botService.SendMessage(new SendMessageArgs(sender.Id,
                        $"Используя бота вы подтверждаете, что автор не несет за вас и ваши действия никакой ответственности"));
                    break;
                }
                case "Посмотреть расписание на день":
                {
                    await this._parserService.SendDayTimetable(sender);
                    break;
                }
                case "Посмотреть расписание на неделю":
                {
                    await this._parserService.SendWeekTimetable(sender);
                    break;
                }
                case "Сменить преподавателя":
                {
                    this._botService.SendMessage(new SendMessageArgs(sender.Id,
                        $"Для оформления подписки на преподавателя отправьте его фамилию."));

                    this._mongoService.CreateState(new UserState(message.Chat.Id, "changeTeacher"));

                    break;
                }
                case "Подписаться на рассылку":
                {
                    await this._accountService.SubscribeNotifications(sender);
                    break;
                }
                case "Отписаться от рассылки":
                {
                    await this._accountService.UnSubscribeNotifications(sender);
                    break;
                }
            }

            try
            {
                if (sender.Id != 698346968) return;

                if (messageText is not null)
                {
                    var lowerMessageText = messageText.ToLower();
                    
                    if (lowerMessageText.Contains("/notify"))
                        await this._parserService.SendNewDayTimetables();
                }
                
                await this._interfaceService.NotifyAllUsers(message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}