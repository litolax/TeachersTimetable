using TeachersTimetable.Models;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using TelegramBot_Timetable_Core.Config;
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
            var lastState = await this._mongoService.GetLastState(message.Chat.Id);
            
            if (lastState is not null && lastState == "changeTeacher")
            {
                await this._accountService.ChangeTeacher(message.From!, message.Text);
                this._mongoService.RemoveState(message.Chat.Id);
            }


            switch (message.Text)
            {
                case "/start":
                {
                    await this._interfaceService.OpenMainMenu(null); //update
                    break;
                }
                case "/menu":
                {
                    await this._interfaceService.OpenMainMenu(null); //update
                    break;
                }
                case "/help":
                {
                    if (message.From is null) return;
                    this._botService.SendMessage(new SendMessageArgs(message.From.Id, 
                        $"Вы пользуетесь ботом, который поможет узнать Вам актуальное расписание преподавателей МГКЦТ.\nСоздатель @litolax"));
                    break;
                }
                case "Посмотреть расписание на день":
                {
                    if (message.From is null) return;
                    await this._parserService.SendDayTimetable(message.From);
                    break;
                }
                case "Посмотреть расписание на неделю":
                {
                    if (message.From is null) return;
                    await this._parserService.SendWeekTimetable(message.From);
                    break;
                }
                case "Сменить преподавателя":
                {
                    this._botService.SendMessage(new SendMessageArgs(message.From.Id,
                        $"Для оформления подписки на преподавателя отправьте его фамилию."));
                    
                    this._mongoService.CreateState(new UserState(message.Chat.Id, "changeTeacher"));

                    break;
                }
                case "Подписаться на рассылку":
                {
                    if (message.From is null) return;
                    await this._accountService.SubscribeNotifications(message.From);
                    break;
                }
                case "Отписаться от рассылки":
                {
                    if (message.From is null) return;
                    await this._accountService.UnSubscribeNotifications(message.From);
                    break;
                }
            }

            if (message.Text!.ToLower().Contains("/sayall") && message.From!.Id == 698346968)
                await this._interfaceService.NotifyAllUsers(null); //update

            if (message.Text!.ToLower().Contains("/notify") && message.From!.Id == 698346968)
                await this._parserService.SendNewDayTimetables();
        }
    }
}