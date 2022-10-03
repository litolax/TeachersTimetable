﻿using TeachersTimetable.Config;
using TeachersTimetable.Models;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableMethods.FormattingOptions;
using Telegram.BotAPI.GettingUpdates;

namespace TeachersTimetable.Services
{
    public interface ICommandsService
    {
        void CommandsValidator(Update update);
    }

    public class CommandsService : ICommandsService
    {
        private readonly IInterfaceService _interfaceService;
        private readonly IAccountService _accountService;
        private readonly IParserService _parserService;
        private readonly IMongoService _mongoService;

        public CommandsService(IInterfaceService interfaceService, IAccountService accountService, IParserService parserService, IMongoService mongoService)
        {
            this._interfaceService = interfaceService;
            this._accountService = accountService;
            this._parserService = parserService;
            this._mongoService = mongoService;
        }

        public async void CommandsValidator(Update update)
        {
            var lastState = await this._mongoService.GetLastState(update.Message.Chat.Id);
            if (lastState is not null && lastState == "changeTeacher")
            {
                await this._accountService.ChangeTeacher(update.Message.From!, update.Message.Text);
                this._mongoService.RemoveState(update.Message.Chat.Id);
            }
            
            
            switch (update.Message.Text)
            {
                case "/start":
                {
                    await this._interfaceService.OpenMainMenu(update);
                    break;
                }
                case "/menu":
                {
                    await this._interfaceService.OpenMainMenu(update);
                    break;
                }
                case "/help":
                {
                    if (update.Message.From is null) return;
                    await this._interfaceService.HelpCommand(update.Message.From);
                    break;
                }
                case "Посмотреть расписание на день":
                {
                    if (update.Message.From is null) return;
                    await this._parserService.SendDayTimetable(update.Message.From);
                    break;
                }
                case "Посмотреть расписание на неделю":
                {
                    if (update.Message.From is null) return;
                    await this._parserService.SendWeekTimetable(update.Message.From);
                    break;
                }
                case "Сменить преподавателя":
                {
                    var config = new Config<MainConfig>();
                    var bot = new BotClient(config.Entries.Token);
                    try
                    {
                        await bot.SendMessageAsync(update.Message.From!.Id, $"Для оформления подписки на преподавателя отправьте его фамилию.");
                        this._mongoService.CreateState(new UserState(update.Message.Chat.Id, "changeTeacher"));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                    break;
                }
                case "Подписаться на рассылку":
                {
                    if (update.Message.From is null) return;
                    await this._accountService.SubscribeNotifications(update.Message.From);
                    break;
                }
                case "Отписаться от рассылки":
                {
                    if (update.Message.From is null) return;
                    await this._accountService.UnSubscribeNotifications(update.Message.From);
                    break;
                }
            }

            if (update.Message.Text!.ToLower().Contains("/sayall") && update.Message.From!.Id == 698346968)
                await this._interfaceService.NotifyAllUsers(update);

            if (update.Message.Text!.ToLower().Contains("/notify") && update.Message.From!.Id == 698346968)
                await this._parserService.SendNewDayTimetables();

        }
    }
}