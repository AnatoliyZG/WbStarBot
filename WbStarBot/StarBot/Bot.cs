using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.Payments;
using Newtonsoft.Json;
using WbStarBot;
using WbStarBot.DataTypes;
using System.Xml.Linq;
using TinkoffPaymentClientApi;
using TinkoffPaymentClientApi.Enums;
using TinkoffPaymentClientApi.Commands;
using TinkoffPaymentClientApi.ResponseEntity;
using System.IO;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Amazon.S3.Model;
using WbStarBot.Wildberries;

#pragma warning disable CS8618

namespace WbStarBot.Telegram
{
    public delegate BotPage Page(ClientLink client, PageQuery arg);

    public partial class Bot : BaseHandler
    {
        // tg limit is 4096
        public const int MaxMessageLenght = 2048;
        public const int MaxPageLenght = 2048;

        public static Bot instance { get; private set; }
        public static uint payCount
        {
            get => _paycount;
            set
            {
                _paycount = value;
                OutputHandler.PushSettings(value);
            }
        }

        private static uint _paycount = 0;

        public Dictionary<string, Page> pages = new Dictionary<string, Page>()
        {
            {"/my", AccountInfo},
            {"/search", SearchPositionInfo },
            {"/ads", AdsInfo },
            {"/label", LabelInfo },
            {"/report", Report },
            {"/products", ProductsInfo },
            {"/support", Support },
            {"/news", News },
            {"/starfall", StarFallInfo },
            {"/pay", AccountPay },
        };

        public Dictionary<string, ClientData> workerLinks = new Dictionary<string, ClientData>();

        private ITelegramBotClient botClient;
        private static TinkoffPaymentClient paymentClient = new TinkoffPaymentClient(CONSTS.TinkoffTerminal, CONSTS.TinkoffPassword);
        private CancellationTokenSource cancellationToken = new CancellationTokenSource();

        public Bot(string token)
        {
            Console.WriteLine("# Bot starting...");

            botClient = new TelegramBotClient(token);

            botClient.StartReceiving(
                ReciveHandler,
                ErrorHandler,
                new ReceiverOptions { AllowedUpdates = { } },
                cancellationToken.Token
            );

            ClientData.reciveFeedBack = ReciveFeedback;
            _paycount = OutputHandler.PopSettings();

            StartClientsHandler();

            debugStream.Input($"{botClient.GetMeAsync().Result.FirstName} started successfully!", MessageType.system);

            instance = this;
        }


        public async Task ReciveHandler(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                switch (update.Type)
                {
                    case UpdateType.CallbackQuery:
                        new Task(async () => await QueryHandler(update.CallbackQuery!)).Start();
                        break;
                    case UpdateType.Message:
                        Message? message = update.Message;

                        if (message is null || message.PinnedMessage is not null)
                            return;

                        if (message.SuccessfulPayment is not null)
                        {
                            new Task(async () => await PaymentHandler(message.SuccessfulPayment, message)).Start();
                        }
                        else
                        {
                            new Task(async () => await MessageHandler(message)).Start();
                        }
                        break;
                    case UpdateType.PreCheckoutQuery:
                        // new Task(async () => await PreCheckoutHandler(update.PreCheckoutQuery!)).Start();
                        break;
                }
            }
            catch (Exception e)
            {
                debugStream.Input(e);
            }
        }

        public async Task ErrorHandler(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            await Task.Run(() => debugStream.Input(exception.Message));
        }

        private async Task QueryHandler(CallbackQuery query)
        {
            try
            {
                long senderId = query.Message!.Chat.Id;

                if (!clients.ContainsKey(senderId))
                    return;
                clients[senderId].messageCallback = null;
                debugStream.Input(query.Data!, MessageType.callback);

                string[] args = query.Data!.Split();

                BotPage page = await Task.Run(() => pages[args[0]].Invoke(new ClientLink(senderId), new PageQuery(query.Data)));


                switch (page.properties)
                {
                    case BotPage.actionProp.edit:
                        if (page.text == null) return;
                        await botClient.EditMessageTextAsync(query.Message.Chat, query.Message.MessageId, page.text, replyMarkup: (page.markup ?? null) as InlineKeyboardMarkup, parseMode: page.parseMode);
                        break;
                    case BotPage.actionProp.delete:
                        await botClient.DeleteMessageAsync(query.Message.Chat, query.Message.MessageId);
                        break;
                    case BotPage.actionProp.answer_message:
                        await botClient.AnswerCallbackQueryAsync(query.Id, page.text);
                        break;
                    case BotPage.actionProp.answer_message_alert:
                        await botClient.AnswerCallbackQueryAsync(query.Id, page.text, true);
                        break;
                    case BotPage.actionProp.answer_with_back:
                        await botClient.AnswerCallbackQueryAsync(query.Id, page.text);
                        BotPage backPage = await Task.Run(() => pages[args[0]].Invoke(new ClientLink(senderId), new PageQuery(string.Join(' ', args.SkipLast(1).ToArray()))));
                        await botClient.EditMessageTextAsync(query.Message.Chat, query.Message.MessageId, backPage.text, replyMarkup: (backPage.markup ?? null) as InlineKeyboardMarkup, parseMode: page.parseMode);
                        break;
                    case BotPage.actionProp.answer_with_delete:
                        await botClient.AnswerCallbackQueryAsync(query.Id, page.text);
                        await botClient.DeleteMessageAsync(query.Message.Chat, query.Message.MessageId);
                        break;
                    case BotPage.actionProp.reply:
                        await botClient.SendTextMessageAsync(query.Message.Chat, page.text, replyToMessageId: page.replyMessage, parseMode: page.parseMode, replyMarkup: page.markup);
                        await botClient.DeleteMessageAsync(query.Message.Chat, query.Message.MessageId);
                        break;
                }
            }
            catch (Exception e)
            {
                debugStream.Input(e);
            }
        }

        private async Task PaymentHandler(SuccessfulPayment payment, Message message)
        {

            try
            {
                ClientData clientData = clientsDatas[payment.InvoicePayload];

                uint amount = (uint)payment.TotalAmount / 100;

                if (amount == 360)
                {
                    clientData.AddBonusBalance(60);
                }
                else if (amount == 720)
                {
                    clientData.AddBonusBalance(120);
                }

                clientData.AddTransferBalance(amount);

                await SendMessageAsync(answers[answer.pay_succes], null, message.Chat.Id);

                if (!clientData.active)
                {
                    ClientLink cl = message.Chat.Id;

                    for (int i = 0; i < cl.client!.clientDatas.Length; i++)
                    {
                        if (clientData == cl.client!.clientDatas[i])
                        {
                            SendMessage(AccountInfo(cl, new PageQuery($"/my {i}")), message.Chat.Id);
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                debugStream.Input(e);
            }
        }

        private async Task MessageHandler(Message update)
        {
            try
            {
                string message = update.Text ?? "";
                long senderId = update.Chat.Id;

                await botClient.DeleteMessageAsync(senderId, update.MessageId);

                if (update.Contact != null)
                {
                    string? reply = update.ReplyToMessage?.Text;
                    string? clientData = reply?.Split("\n")[0];
                    int accId = 0;
                    foreach (ClientData data in clients[senderId].clientDatas)
                    {
                        if (clientData == data.phone || data.Name == clientData)
                        {
                            data.phone = update.Contact.PhoneNumber;

                            await botClient.SendTextMessageAsync(senderId, $"✅ Номер успешно привязан к аккаунту {clientData}.",
                                replyMarkup: new ReplyKeyboardRemove());

                            SendMessage(await Task.Run(() => pages["/my"].Invoke(new ClientLink(senderId), new PageQuery($"/my {accId} pay"))), senderId);
                            return;
                        }
                        accId++;
                    }
                }

                Client? sender = null;
                clients.TryGetValue(senderId, out sender);

                if (message == "/start")
                {
                    if (sender == null)
                    {
                        sender = new Client(update.From?.Username);
                        clients.Add(senderId, sender);
                        await SendMessageAsync(answers[answer.start_message], null, senderId);
                    }

                    if (sender.userName == null)
                    {
                        sender.userName = update.From?.Username;
                    }

                    if (sender.clientDatas.Length == 0)
                    {
                        sender.messageCallback = ConnectApi;

                        sendMsg(answer.enter_api);
                    }
                    else
                    {
                        sendMsg(answer.acc_exists);
                    }
                }
                else if (message.StartsWith('/'))
                {
                    if (pages.ContainsKey(message))
                    {
                        if (sender != null && sender.clientDatas.Length > 0)
                        {
                            SendMessage(await Task.Run(() => pages[message].Invoke(new ClientLink(senderId), new PageQuery(message))), senderId);
                        }
                        else
                        {
                            sendMsg(answer.start);
                        }
                    }
                    else
                    {
                        sendMsg(answer.unk_command);
                    }
                }
                else
                {
                    if (sender != null)
                    {
                        if (sender.messageCallback is not null)
                        {
                            try
                            {
                                string? callback = await sender.messageCallback!.Invoke(new ClientLink(senderId), message);

                                if (callback != null)
                                    await SendMessageAsync(callback, null, senderId);

                                sender.messageCallback = null;
                            }
                            catch (MessageException ex)
                            {
                                await SendMessageAsync(ex.Message, null, senderId);

                                if (ex.nullableCallback)
                                {
                                    sender.messageCallback = null;
                                }
                            }
                            catch (Exception ex)
                            {
                                debugStream.Input(ex);
                            }
                        }
                        else
                        {
                            sendMsg(answer.unk_command);
                        }

                    }
                    else
                    {
                        sendMsg(answer.start);
                    }
                }
                debugStream.Input($"{senderId}: {message}");

                async void sendMsg(answer ans)
                {
                    await SendMessageAsync(answers[ans], null, senderId);
                }
            }
            catch (Exception e)
            {
                debugStream.Input(e);
            }
        }

        public async Task SendMessageAsync(string text, IReplyMarkup? markup, ParseMode parseMode, bool silent, params long[] recivers)
        {
            if (text.Length > MaxMessageLenght)
            {
                text = text.Remove(MaxMessageLenght);
            }

            await Task.Factory.StartNew(() =>
            {
                try
                {
                    for (int i = 0; i < recivers.Length; i++)
                    {
                        int taskNum = i;
                        Task.Run(() => botClient.SendTextMessageAsync(recivers[taskNum], text, replyMarkup: markup, parseMode: parseMode, disableNotification: silent));
                    }
                }
                catch (Exception e)
                {
                    debugStream.Input($"Send message error:{e.Message} \nMessage text: {text}", MessageType.error);
                }
            });
        }

        public async Task SendMessageAsync(string text, IReplyMarkup? markup, params long[] recivers)
        {
            await SendMessageAsync(text, markup, ParseMode.Markdown, false, recivers);
        }
        public async Task SendMessageAsync(string text, IReplyMarkup? markup, bool silent, params long[] recivers)
        {
            await SendMessageAsync(text, markup, ParseMode.Markdown, silent, recivers);
        }

        public static BotPage SendInvoce(ClientLink clientLink, PageQuery pageQuery, uint summ, ClientData currentClient)
        {
            if (summ < CONSTS.MinimumPayValue)
                return new BotPage("") { properties = BotPage.actionProp.delete };

            try
            {
                PaymentResponse response = paymentClient.Init(new Init($"{clientLink.clientId} {clientLink.client.paysCount} {payCount++}", summ * 100));

                if (response.Success)
                {
                    return new BotPage(answers[answer.pay_start], new InlineKeyboardMarkup(new InlineKeyboardButton[]
                    {
                    new InlineKeyboardButton("Перейти к оплате") { Url = response.PaymentURL },
                    new InlineKeyboardButton("Проверить оплату") { CallbackData = pageQuery.ChangeCallback($"check {response.PaymentId}")},
                    }));
                }

                Task.Factory.StartNew(() =>
                {
                    Thread.Sleep(600000);

                    CheckPayment(clientLink, pageQuery.ChangeCallback($"check {response.PaymentId}"), currentClient);
                });

                return new BotPage(answers[answer.pay_failed], null);
            }
            catch (Exception e)
            {
                Base.debugStream.Input(e);
            }
            return new BotPage("") { properties = BotPage.actionProp.delete };

            //await botClient.SendInvoiceAsync(senderId, "Пополнение аккаунта", $"Оплата личного счета {account.Name}.", account.apiKey, CONSTS.PaymentWallet, "RUB", new LabeledPrice[] { new LabeledPrice("Оплатить", summ * 100) });
        }

        public static BotPage CheckPayment(ClientLink client, PageQuery arg, ClientData data)
        {
            string? paymentId = arg[3];
            Console.WriteLine(paymentId);
            GetStateResponse response = paymentClient.GetState(new GetState(paymentId!));

            if (response.Success)
            {
                if (response.Status == EStatusResponse.Confirmed)
                {
                    foreach (Transaction transaction in data.transactions)
                    {
                        if (transaction.orderId == response.OrderId)
                        {
                            return new BotPage(answers[answer.pay_succes]) { properties = BotPage.actionProp.answer_with_delete };
                        }
                    }

                    data.AddTransferBalance(response.Amount / 100);
                    data.transactions.Add(new Transaction(response.OrderId, response.Amount / 100));
                    return new BotPage(answers[answer.pay_succes]) { properties = BotPage.actionProp.answer_with_back };
                }
                return new BotPage($"⌛️ Платеж все еще обрабатывается.\nТекущий статус платежа: {response.Status}.\nЕсли деньги были списаны, а статус платежа не изменяется в течении длительного времени, то обратитесь в тех. поддержку!") { properties = BotPage.actionProp.answer_message_alert };
            }

            return new BotPage($"❌ Ошибка платежа: {response.Details}.\nЕсли деньги были списаны и не возвращены банком в течении 10 минут, то обратитесь в тех. поддержку!") { properties = BotPage.actionProp.answer_message_alert };
        }

        public static void SendMessage(string text, IReplyMarkup? markup, params long[] recivers)
        {
            Task.Run(() => instance.SendMessageAsync(text, markup, recivers));
        }
        public static void SendMessage(string text, IReplyMarkup? markup, bool silent, params long[] recivers)
        {
            Task.Run(() => instance.SendMessageAsync(text, markup, silent, recivers));
        }
        public static void SendMessage(BotPage botPage, params long[] recivers)
        {
            Task.Run(() => instance.SendMessageAsync(botPage.text, botPage.markup, botPage.parseMode, false, recivers));
        }

        public static void ReciveFeedback(ClientData data, ProductInfo info, uint nmId, string text, int? vl)
        {
            string stars = "";
            if (vl != null)
            {
                for (int i = 0; i < vl; i++)
                {
                    stars += "⭐️";
                }
            }
            string link = $"[{nmId}](https://www.wildberries.ru/catalog/{nmId}/detail.aspx)";
            SendMessage($"💬 Новый отзыв!\n{info.name}\n🆔 {link}\n\n{stars}\n{text}", null, data.recivers.Where(a => a.Value.HasFlag(notify.Feedback)).Select(a => a.Key).ToArray());
        }

        public static async void SendNotify( ClientData data, string text, uint nmId, notify notify, bool silent)
        {
            if (text.Length > MaxMessageLenght)
            {
                _ = text.Remove(MaxMessageLenght);
            }

            string photoPath = $"{OutputHandler.productImageDir}/{nmId}.jpeg";
            FileStream fs = null;

            if (WildberriesHandler.GetItemImage(nmId))
            {
                fs = new FileStream(photoPath, FileMode.Open, FileAccess.Read);
            }

            try
            {
                foreach (long reciver in data.users)
                {
                    if (!instance.clients.ContainsKey(reciver))
                    {
                        instance.clients.Remove(reciver);
                        continue;
                    }
                    if (!data.recivers[reciver].HasFlag(notify)) continue;

                    string dataName = "";

                    try
                    {
                        dataName = data[instance.clients[reciver].getClientDataId(data)];
                    }
                    catch { }


                    if(fs != null)
                    {
                        fs.Seek(0, SeekOrigin.Begin);
                        InputOnlineFile photo = new InputOnlineFile(fs);
                        await instance.botClient.SendPhotoAsync(reciver, photo, $"\n{dataName} {text}", ParseMode.Markdown, disableNotification: silent);
                    }
                    else
                    {
                        SendMessage($"\n{dataName} {text}", null, silent, reciver);
                    }
                }
            }
            catch (Exception e) 
            {
                Base.debugStream.Input(e);
            }
            finally
            {
                if(fs != null)
                {
                    fs.Close();
                    fs.Dispose();
                }
            }
        }
    }
}

