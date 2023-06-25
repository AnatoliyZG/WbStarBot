using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using WbStarBot.Wildberries;
using WbStarBot.Telegram.Extensions;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.Payments;
using System.Security.Cryptography;

#pragma warning disable CS8981

namespace WbStarBot.Telegram
{
    public partial class Bot : BaseHandler
    {
        public static BotPage AccountPay(ClientLink client, PageQuery arg)
        {
            switch (arg[0])
            {
                case null:
                    (string, string)[] accs = new (string, string)[client.client!.clientDatas.Length < 9 ? client.client!.clientDatas.Length : 9];

                    for (int i = 0; i < accs.Length; i++)
                    {
                        accs[i] = (client.client!.clientDatas[i][i], $"/my {i} pay");
                    }

                    return ($"🆔: `{client.clientId}`\n\n👤 Поставщики: {client.client.clientDatas.Length}", accs.Markup());
            }
            return new BotPage(null);
        }

        public static BotPage AccountInfo(ClientLink client, PageQuery arg)
        {
            switch (arg[0])
            {
                case null:
                    (string, string)[] accs = new (string, string)[client.client!.clientDatas.Length < 9 ? client.client!.clientDatas.Length + 1 : client.client!.clientDatas.Length];

                 //   accs[0] = ("👷🏽‍♂️ Тех. поддержка", "https://t.me/WbStarSupport");

                    for (int i = 0; i < client.client!.clientDatas.Length; i++)
                    {
                        accs[i] = (client.client!.clientDatas[i][i], $"/my {i}");
                    }
                    if (client.client!.clientDatas.Length < 9)
                        accs[^1] = ("+ Добавить", "/my add");

                    return ($"🆔: `{client.clientId}`\n\n👤 Поставщики: {client.client.clientDatas.Length}", accs.Markup());
                case "add":
                    client.client!.messageCallback = ConnectApi;
                    return (answers[answer.enter_api], null);
                default:
                    int dataId = int.Parse(arg.query![0]);
                    ClientData currentClient = client.client!.clientDatas[dataId];

                    switch (arg[1])
                    {
                        case null:
                            string accInfo = currentClient[dataId];
                            bool isAdmin = currentClient.Admin == client.clientId;
                            accInfo += $"\n{(isAdmin ? "👑" : "🧢")} Ваш статус: {(isAdmin ? "Админ" : "Пользователь")}";
                            accInfo += $"\n\n🔖 Стоимость подписки: {CONSTS.WeekCost * 4}₽/мес.";

                            (string, string)[] actions =
                            {
                                ("❌ Удалить ИП", arg.ReplyCallback("delete")),
                                arg.BackButton,
                            };


                            List<(string, string)[]> markup = new List<(string, string)[]>(){
                                button("💰 Баланс", arg.ReplyCallback("pay")),
                                button($"{(currentClient.recivers[client] == notify.none ? "🔕" : "🔔")} Уведомления", arg.ReplyCallback("notify")),
                                button("🎟 Промокод", arg.ReplyCallback("promo")),
                                button($"📦 Резерв склада {currentClient.stockDays} дн.", arg.ReplyCallback("stock")),
                            };

                            if (isAdmin)
                            {
                                markup.Add(new (string, string)[] { ("⚙️ Настройки", arg.ReplyCallback("settings")) });
                            }
                            markup.Add(actions);


                            if (!currentClient.active)
                            {
                                if (currentClient.balance == 0)
                                {
                                    accInfo += $"\n\n{answers[answer.account_not_active]}";
                                }

                            }

                            accInfo += $"\n\n👀 Кол-во пользователей: {currentClient.users.Length}";

                            if (currentClient.starfall)
                            {
                                accInfo += "\n\n🌟 Работает в режиме /starfall";
                            }

                            return (accInfo, markup.ToArray().Markup());
                        case "promo":
                            client.client.messageCallback = (a, b) => ActivePromo(a, currentClient, b);
                            return ("🎟 Введите промокод:", arg.BackButton.Markup());
                        case "delete":
                            (string, string)[] acts =
                            {
                                ("Подтвердить", arg.ChangeCallback("deleteConf")),
                                arg.BackButton,
                            };

                            return ("⚠️ *Внимание!*\n\nУдаление аккаунта не удалит его полностью, а лишь с вашего устройства. Вы утратите возможность просмотра информации об ИП и перестанете получать какие-либо уведомления от этого аккаунта.\n\n❗️ Если вы являетесь администратором данного ИП, то права перейдут следующему пользователю. В данном случае настоятельно рекомендуем удалить всех пользователей или выбрать самостоятельно следующего администратора!", acts.Markup());
                        case "deleteConf":
                            client.client.apiKeys.Remove(currentClient.apiKey);
                            currentClient.recivers.Remove(client.clientId);
                            return AccountInfo(client, new PageQuery("/my"));
                        case "stock":
                            switch (arg[2])
                            {
                                case null:
                                    (string, string)[] buttons =
                                    {
                                        ("5 дней", arg.ReplyCallback("5")),
                                        ("7 дней", arg.ReplyCallback("7")),
                                        ("14 дней", arg.ReplyCallback("14")),
                                        ("30 дней", arg.ReplyCallback("30")),
                                        ("Другой период", arg.ReplyCallback("any")),
                                        arg.BackButton,
                                    };
                                    return ($"📦 *Резерв склада:*\nТекущий период: {currentClient.stockDays} дн.\n\nℹ️ Выбранный период определяет расчет остатков ваших товаров. Как только товар начнет заканчивать, бот уведомит Вас!", buttons.Markup());
                                case "any":
                                    client.client.messageCallback = (a, b) => SelectStockDayArchive(a, currentClient, b);

                                    return ($"📦 Введите период резерва (1-30):", arg.BackButton.Markup());
                                default:
                                    byte days = 14;
                                    byte.TryParse(arg[2], out days);
                                    currentClient.stockDays = days;
                                    return AccountInfo(client, arg.BackButton.callback);//;

                            }
                            return (null, null);
                        case "notify":
                            (string, string)[] notifyMarkup ={
                                ($"{switchSmile(notify.Feedback)} Отзывы", arg.ReplyCallback("feedback")),
                                ($"{switchSmile(notify.Orders)} Заказы", arg.ReplyCallback("orders")),
                                ($"{switchSmile(notify.Sells)} Продажи", arg.ReplyCallback("sells")),
                              //  ($"{switchSmile(notify.WeekReport)} Недельные отчеты", arg.ReplyCallback("week")),

                                arg.BackButton,
                            };

                            string switchSmile(notify notify)
                            {
                                if (currentClient.recivers[client].HasFlag(notify)) return "🔉";
                                return "🔇";
                            }

                            switch (arg[2])
                            {
                                case null:
                                    return ("Ваши уведомления:", notifyMarkup.Markup());

                                case "feedback":
                                    currentClient.recivers[client] ^= notify.Feedback;
                                    return AccountInfo(client, arg.BackButton.Item2);
                                case "orders":
                                    currentClient.recivers[client] ^= notify.Orders;
                                    return AccountInfo(client, arg.BackButton.Item2);
                                case "sells":
                                    currentClient.recivers[client] ^= notify.Sells;
                                    return AccountInfo(client, arg.BackButton.Item2);
                            //    case "week":
                            //        currentClient.recivers[client] ^= notify.WeekReport;
                            //        return AccountInfo(client, arg.BackButton.Item2);
                            }


                            return ("", null);
                        case "settings":
                            switch (arg[2])
                            {
                                case null:
                                    string api = currentClient.apiKey.Replace(".", "\\.").Replace("_", "\\_").Replace("*", "\\*").Replace("-", "\\-");
                                    (string, string)[] buttons =
                                    {
                                        ("🔑 Изменить Api ключ", arg.ReplyCallback("change")),
                                        ("👨🏻‍💻 Настройка сотрудников",  arg.ReplyCallback("workers")),
                                        ("🌟 Режим звездопад:",arg.ReplyCallback("mode")),
                                        arg.BackButton,
                                    };

                                    return new BotPage($"👑 *Данная страница доступна только для администратора ИП\\!*\n\n🔑 Api ключ \\(статистика\\)\\: ||{api}||", buttons.Markup()) { parseMode = ParseMode.MarkdownV2, };
                                case "change":
                                    client.client.messageCallback = (a, b) => ChangeApi(a, b, currentClient);
                                    return (answers[answer.enter_api], arg.BackButton.Markup());
                                case "mode":
                                    (string, string)[] btns =
                                    {
                                        (currentClient.starfall ? "🟢 Включен" : "🔴 Выключен", arg.ChangeCallback("switchMode")),
                                        arg.BackButton,
                                    };
                                    return ("🌟 *Режим звездопад:*\n\nℹ️ Данный режим обеспечивает работу уведомлений во время неполадок со стороны Wildberries. Когда сервера WB перестают выдавать списки заказов, данный режим самостоятельно анализирует заказы на основе остатков товара.\n\n🔰 В таком формате бот не всегда корректно обрабатывает информацию о заказах из-за остутсвия возможности определения пополнений складов и логистических данных, так что в статистику заказы из режима \"Звездопад\" не попадают пока сервера WB не восстановят работу и информация о заказах не подтвердиться.\n\n🔔 Вы так же можете отключить работу режима во избежание ошибочных уведомлений.", btns.Markup());
                                case "switchMode":
                                    currentClient.starfall = !currentClient.starfall;
                                    goto case "mode";
                                case "workers":
                                    switch (arg[3])
                                    {
                                        case null:
                                            (string, string)[] wbuttons = new (string, string)[currentClient.recivers.Count + 1];

                                            for (int i = 1; i < currentClient.recivers.Count; i++)
                                            {
                                                wbuttons[i - 1] = ($"🧢 {Base.clients[currentClient.users[i]].userName}", arg.ReplyCallback(i.ToString()));
                                            }

                                            wbuttons[^2] = ("➕ Пригласить пользователя", arg.ReplyCallback("l_create"));
                                            wbuttons[^1] = arg.BackButton;

                                            return ($"👨🏻‍💻 *Настройка сотрудников:*\n\n👀 Кол-во сотрудников (Помимо Вас): {currentClient.users.Length - 1}\n\nℹ️ Тут Вы можете удалять или создать код-приглашение для новых сотрудников.", wbuttons.Markup());
                                        case "l_create":

                                            string code = "";

                                            if (Bot.instance.workerLinks.ContainsValue(currentClient))
                                            {
                                                code = Bot.instance.workerLinks.First(a => a.Value == currentClient).Key;
                                            }
                                            else
                                            {
                                                byte[] rgb = new byte[12];
                                                RNGCryptoServiceProvider rngCrypt = new RNGCryptoServiceProvider();
                                                rngCrypt.GetBytes(rgb);

                                                code = Convert.ToBase64String(rgb);
                                                instance.workerLinks.Add(code, currentClient);
                                                new Thread(new ThreadStart(() =>
                                                {
                                                    Thread.Sleep(10800000);
                                                    instance.workerLinks.Remove(code);
                                                })).Start();
                                            }

                                            return ($"🔐 Код приглашения: `{code}`\n\nℹ️ Отправьте этот код любому пользователю, чтобы он ввел его при добавлении нового продавца вместо Api ключа статистики.\n\n⚠️Код действителен 3 часа!", arg.BackButton.Markup());

                                        default:
                                            int worker = int.Parse(arg[3]);
                                            Client user = Base.clients[currentClient.users[worker]];

                                            switch (arg[4])
                                            {
                                                case null:
                                                    (string, string)[] w2buttons =
                                                    {
                                                        ("❌ Удалить сотрудника", arg.ReplyCallback("delete")),
                                                        ("🔮 Назначить админом", arg.ReplyCallback("admin")),
                                                        arg.BackButton,
                                                    };
                                                    return ($"🧢 {user.userName.Replace("_","")}\n\n🆔 {currentClient.users[worker]}", w2buttons.Markup());
                                                case "delete":
                                                    user.apiKeys.Remove(currentClient.apiKey);
                                                    currentClient.recivers.Remove(currentClient.users[worker]);
                                                    return AccountInfo(client, new PageQuery(new PageQuery(arg.BackButton.callback).BackButton.callback));
                                               case "admin":
                                                    (string, string)[] admButton =
                                                    {
                                                        ("Я согласен", arg.ChangeCallback("root")),
                                                        arg.BackButton,
                                                    };
                                                    return ($"⚠️ Вы уверены, что хотите сделать пользователя {user.userName} администратором?\n\n‼️ Передав права админа, вы сами станете обычным пользователем и редактирование прав доступа перестанет быть доступно для вас!\n\nℹ️ Отменить действие невозможно и вернуть Вам ваши права сможет только назначенный админ. Нажимая на кнопку \'Я согласен\', вы принимаете ответсвенность за все риски, связанные с передачей прав управления аккаунтом!", admButton.Markup());
                                                case "root":
                                                    currentClient.Admin = currentClient.users[worker];
                                                    return AccountInfo(client, new PageQuery($"/my {dataId}"));

                                            }
                                            break;
                                    }


                                    return (null, null);

                            }
                            return (null, null);

                        case "pay":
                            if (currentClient.phone is null)
                            {
                                instance.botClient.SendTextMessageAsync(client.clientId,
                                    $"{currentClient.Name}\n\n❗️ В целях безопасности нам необходим ваш номер телефона.\n\n🔐 Бот надежно хранит ваши номера и не использует их для отправки смс или спам рассылок!\n\n👇 *Для пердоставления номера нажмите на кнопку снизу.* 👇",
                                    replyMarkup: new ReplyKeyboardMarkup(new KeyboardButton("Отправить номер") { RequestContact = true })
                                    {
                                        OneTimeKeyboard = true,
                                        ResizeKeyboard = true,
                                    }, parseMode: ParseMode.Markdown)
                                    .Wait();

                                return new BotPage("") { properties = BotPage.actionProp.delete };
                            }

                            switch (arg[2])
                            {
                                case null:
                                    (string, string)[][] payValue =
                                    {
                                        new (string, string)[]{ ("120₽", arg.ReplyCallback("120")) },
                                        new (string, string)[]{ ("360₽ (15 дней бонус)", arg.ReplyCallback("360")) },
                                        new (string, string)[]{ ("720₽ (30 дней бонус)", arg.ReplyCallback("720")) },
                                        new (string, string)[]{ ("Другая сумма", arg.ReplyCallback("any")) },
                                        new (string, string)[]{ ("📈 Пополниеня", arg.ReplyCallback("payments")),("📉 Списания", arg.ReplyCallback("spendings"))},
                                        new (string, string)[]{ arg.BackButton },
                                    };
                                    return ($"{currentClient.ShowBalance}\n\n🔖 Стоимость подписки: {CONSTS.WeekCost * 4}₽/мес.\n\n{answers[answer.pay_info]}\n\n🔰 Ваш бонус к пополнению: 0%", payValue.Markup());
                                case "payments":
                                    string transactions = string.Join<Transaction>("\n\n", currentClient.transactions.OrderByDescending(a => a.date));

                                    return ("Пополнения:\n" + transactions, arg.BackButton.Markup());
                                case "spendings":
                                    string pays = string.Join<Pay>("\n\n", currentClient.pays.OrderByDescending(a => a.date));

                                    return ("Списания:\n\n" + pays, arg.BackButton.Markup());
                                case "any":
                                    client.client.messageCallback = (a, b) => SelectPaySumm(a, arg, b, currentClient);
                                    return ($"⬇️ Введите желаюмую сумму пополнения.\n\n🔰 Ваш бонус к пополнению: 0%\n\n⚠️ Минимальная сумма пополнения: {CONSTS.MinimumPayValue} руб.", arg.BackButton.Markup());
                                case "check":
                                    if (arg[3] != null)
                                    {
                                        return CheckPayment(client, arg, currentClient);
                                    }
                                    return AccountInfo(client, arg.ChangeCallback("payments"));
                                default:
                                    uint cost = uint.Parse(arg[2]!);
                                    SendMessage(SendInvoce(client, arg, cost, currentClient), client);
                                    return AccountInfo(client, arg.BackButton.callback);//;
                            }

                        default: throw new Exception($"No implemented command. {arg.callback}");
                    }
            }
        }

        public static async Task<string?> SelectPaySumm(ClientLink client, PageQuery arg, string message, ClientData currentClient)
        {
            uint sum = 0;

            if (!uint.TryParse(message, out sum))
            {
                throw new MessageException(answers[answer.error_summ]);
            }

            if (sum < CONSTS.MinimumPayValue)
            {
                throw new MessageException(answers[answer.pay_too_low]);
            }

            SendMessage(SendInvoce(client, arg, sum, currentClient), client.clientId);
            return null;
        }
        public static async Task<string?> SelectStockDayArchive(ClientLink client, ClientData currentData, string message)
        {
            byte days = 0;

            if (byte.TryParse(message, out days))
            {
                if (days >= 1 && days <= 30)
                {
                    currentData.stockDays = days;
                    return "✅ Период успешно установлен!";
                }
            }

            throw new MessageException($"❌ Указан неверный формат периода.\n\nСообщение должно содержать цифру в диапазоне от 1, до 30!");
        }

        public static async Task<string?> ActivePromo(ClientLink client, ClientData currentData, string message)
        {
            if (CONSTS.promo != message.ToLower())
            {
                throw new MessageException(answers[answer.promocode_fail]);
            }
            if (message == currentData.promocode)
            {
                throw new MessageException(answers[answer.promocode_already_use], true);
            }
            currentData.promocode = message;
            currentData.AddBonusBalance(CONSTS.promoBonusBalance);

            return answers[answer.promocode_succes];
        }
        public static async Task<string?> ChangeApi(ClientLink client, string message, ClientData currentData)
        {
            if (message.Length != 149 || message.Contains(' '))
            {
                throw new MessageException(answers[answer.error_api]);
            }

            if (instance.clientsDatas.ContainsKey(message))
            {
                return "❌ Данный аккаунт уже зарегистрирован в системе StarBot";
            }

            Bot.instance.SendMessageAsync("🔎 Подождите. Подключаем Ваш api...", null, client.clientId);

            string previousKey = currentData.apiKey;
            foreach (long reciver in currentData.recivers.Keys)
            {
                for (int i = 0; i < Base.clients[reciver].apiKeys.Count; i++)
                {
                    if (Base.clients[reciver].apiKeys[i] == previousKey)
                    {
                        Base.clients[reciver].apiKeys[i] = message;
                        break;
                    }
                }
            }
            Base.clientsData.Remove(previousKey);
            Base.clientsData.Add(message, currentData);
            currentData.apiKey = message;

            return answers[answer.data_successfuly];
        }


        public static async Task<string?> ConnectApi(ClientLink client, string message)
        {
            if(message.Length == 16)
            {
                if (instance.workerLinks.ContainsKey(message))
                {
                    ClientData data = instance.workerLinks[message];
                    if (client.client!.clientDatas.Contains(data))
                    {
                        return answers[answer.data_already_has_reciver];
                    }
                    data.recivers.Add(client.clientId, notify.All);
                    client.client?.AddApis(data.apiKey);
                    return answers[answer.data_successfuly];
                }
            }

            if (message.Length != 149 || message.Contains(' '))
            {
                throw new MessageException(answers[answer.error_api]);
            }

            ClientData? clientData = null;

            instance.clientsDatas.TryGetValue(message, out clientData);

            MessageCallback callback = client.client.messageCallback;
            client.client.messageCallback = null;

            Bot.instance.SendMessageAsync("🔎 Подождите. Подключаем Ваш api...", null, client.clientId);

            if (clientData is not null)
            {
                if (client.client?.clientDatas.Contains(clientData) ?? false)
                {
                    return answers[answer.data_already_has_reciver];
                }
                if (!clientData.users.Contains(client.clientId))
                    clientData.recivers.Add(client.clientId, notify.All);
            }
            else
            {
                try
                {
                    clientData = new ClientData(message, client.clientId);
                    try
                    {
                        await WildberriesHandler.ClientDataUpdate(clientData);
                        clientData.AddBonusBalance((uint)CONSTS.WeekCost * 3);
                        instance.clientsDatas.Add(message, clientData);
                    }
                    catch(Exception ex)
                    {
                        WildberriesException? wbExeption = ex as WildberriesException;
                        if (wbExeption != null)
                        {
                            switch (wbExeption.exceptionType)
                            {
                                case WildberriesException.ExceptionType.data_bad_request:
                                    throw new MessageException(answers[answer.data_bad_request]);
                                case WildberriesException.ExceptionType.data_too_many_request:
                                    throw new MessageException(answers[answer.data_too_many_requests]);
                                default:
                                    throw new MessageException(answers[answer.data_failed]);
                            }
                        }
                        else
                        {
                            throw new MessageException(answers[answer.data_failed]);
                        }
                    }
                }
                catch (MessageException exc)
                {
                    client.client.messageCallback = callback;
                    throw exc;
                }
            }

            client.client?.AddApis(message);

            OutputHandler.SaveClientsData();

            if (clientData.active)
            {
                return answers[answer.data_successfuly];
            }

            int currentData = client.client!.clientDatas.Length - 1;
            SendMessage(AccountInfo(client, new PageQuery("/my", currentData.ToString())), client.clientId);

            return null;
        }

        private static (string, string)[] button(string text, string arg) => new (string, string)[] { (text, arg) };
    }
}

