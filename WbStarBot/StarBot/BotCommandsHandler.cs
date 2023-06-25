using System;
using System.Linq;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.Payments;
using WbStarBot.Wildberries;
using WbStarBot.Telegram.Extensions;
using WbStarBot.DataTypes;
using DocumentFormat.OpenXml.EMMA;
using System.ComponentModel;

namespace WbStarBot.Telegram
{
    public partial class Bot : BaseHandler
    {
        public static BotPage ProductsInfo(ClientLink client, PageQuery arg)
        {
            switch (arg[0])
            {
                case null:
                    if (client.client!.clientDatas.Length > 1)
                    {
                        (string, string)[] datas = new (string, string)[client.client.clientDatas.Length];

                        for (int i = 0; i < datas.Length; i++)
                        {
                            datas[i] = (client.client.clientDatas[i][i], arg.ReplyCallback(i.ToString()));
                        }

                        return (answers[answer.products_select_api], datas.Markup());
                    }
                    else return ProductsInfo(client, arg.ReplyCallback("0"));
                default:
                    ClientData current = client.client!.clientDatas[int.Parse(arg[0]!)];
                    switch (arg[1])
                    {
                        case null: return ProductsInfo(client, arg.ReplyCallback("1"));
                        default:
                            int filter = int.Parse(arg[1]!);

                            switch (arg[2])
                            {
                                case null: return ProductsInfo(client, arg.ReplyCallback("0"));
                                default:
                                    DataContainer[] containers = current.dataHandler.containers.Values.Where(a => a.orders.Count > 0).ToArray();

                                    if(filter != 0)
                                    {
                                        if(filter == 1)
                                        {
                                            containers = containers.OrderByDescending(a => a.buyoutProcent(90)).ToArray();
                                        }else if(filter == -1)
                                        {
                                            containers = containers.OrderByDescending(a => a.buyoutProcent(90)).ToArray();
                                        }
                                        else if (filter == 2)
                                        {
                                            containers = containers.OrderByDescending(a => a.stock).ToArray();
                                        }
                                        else if (filter == -2)
                                        {
                                            containers = containers.OrderBy(a => a.stock).ToArray();
                                        }
                                        else if (filter == 3)
                                        {
                                            containers = containers.OrderByDescending(a => a.orders.periodData(current.archiveDays, false)).ToArray();
                                        }
                                        else if (filter == -3)
                                        {
                                            containers = containers.OrderBy(a => a.orders.periodData(current.archiveDays, false)).ToArray();
                                        }
                                        else if (filter == 4)
                                        {
                                            containers = containers.OrderByDescending(a => a.orders.periodData(current.archiveDays, true)).ToArray();
                                        }
                                        else if (filter == -4)
                                        {
                                            containers = containers.OrderBy(a => a.orders.periodData(current.archiveDays, true)).ToArray();
                                        }
                                    }


                                    int ord = int.Parse(arg[2]!);
                                    int page = ord * 2;
                                    int maxPage = containers.Length;
                                    int mx = (maxPage + 1) / 2;

                                    if (page < 0)
                                    {
                                        page = maxPage - 1;
                                        ord = mx - 1;
                                    }
                                    else if (page >= maxPage)
                                    {
                                        page = 0;
                                        ord = 0;
                                    }

                                    string text = $"Страница {ord + 1}/{mx}\n\n{getOrderText(page)}{getOrderText(page + 1)}";

                                    (string, string)[][] buttons =
                                    {
                                        new (string, string)[]{
                                            ("← Пред", arg.ChangeCallback($"{ord-1}")),
                                            ("След →", arg.ChangeCallback($"{ord+1}"))
                                        },
                                        new (string, string)[]{
                                            c_filter(1, "💎 Выкупы (%)")
                                        },
                                        new (string, string)[]{
                                            c_filter(3, $"🚛 Заказы ({current.archiveDays}д)")
                                        },
                                        new (string, string)[]{
                                            c_filter(4, "🚚 Возвраты")
                                        },
                                        new (string, string)[]{
                                            c_filter(2, "📦 Остатки")
                                        },
                                    };



                                    return new BotPage(text, buttons.Markup()) { parseMode = ParseMode.Html};

                                    (string, string) c_filter(int f, string nm)
                                    {
                                        string cl = f.ToString();
                                        

                                        if(filter == f)
                                        {
                                            nm = $"{nm} ⬆️";
                                            cl = (-f).ToString();
                                        }else if(filter == -f)
                                        {
                                            nm = $"{nm} ⬇️";
                                        }
                                        return (nm, $"{arg.page} {arg[0]} {cl} {arg[2]}");
                                    }

                                    string getOrderText(int prod)
                                    {
                                        if (prod < 0 || prod >= maxPage)
                                            return "";

                                        DataContainer orders = containers[prod];
                                        ProductInfo info = null;

                                        current.dataHandler.products.TryGetValue(orders.root!.Value, out info);

                                        if (info == null)
                                            return "";

                                        string content = "";
                                        int orderCount = 0;
                                        int backCount = 0;
                                        int income = 0;

                                        OrderData data = orders.orders.lastElement;

                                        foreach (OrderData order in orders.orders.orderedData)
                                        {
                                            if (DateTime.Now.Subtract(order.date).TotalDays > current.archiveDays) break;

                                            if (order.isCancel) backCount++;
                                            else
                                            {
                                                orderCount++;
                                                income += order.price;
                                            }
                                        }
                                        content += $"<b><i>{info.name}</i></b>\n";
                                        content += $"🆔 ID товара: <code>{orders.nmId}</code>\n";
                                        content += $"🏷 {data.Brand} | <a href=\"https://www.wildberries.ru/catalog/{orders.nmId}/detail.aspx\">{(data.supplierArticle != null && data.supplierArticle.Length > 0 ? data.supplierArticle : "article")}</a>\n";
                                        content += $"📁 {data.category} | {data.techSize}\n";
                                        content += $"💬 {info.valuation} {string.Join("", "⭐️⭐️⭐️⭐️⭐️".Take((int)Math.Round(info.valuation.Value * 2)))}\n";
                                        (int canceled, int all) buyout = orders.buyout(90);

                                        if (Math.Abs(filter) == 1)
                                            content += "<b>";
                                        if (buyout.all > 0)
                                        {
                                            content += $"💎 Выкупы (3 мес.): {100 - (int)((float)buyout.canceled / buyout.all * 100f)}% ({buyout.all - buyout.canceled}/{buyout.all})\n";
                                        }
                                        else
                                        {
                                            content += $"💎 Выкупы (3 мес.): 0\n";
                                        }
                                        if (Math.Abs(filter) == 1)
                                            content += "</b>";
                                        if (Math.Abs(filter) == 3)
                                            content += "<b>";
                                        content += $"🚛 Заказы ({current.archiveDays} дн): {orderCount}\n";
                                        if (Math.Abs(filter) == 3)
                                            content += "</b>";
                                        if (Math.Abs(filter) == 4)
                                            content += "<b>";
                                        content += $"🚚 Возвраты ({current.archiveDays} дн): {backCount}\n";
                                        if (Math.Abs(filter) == 4)
                                            content += "</b>";
                                        content += $"💰 Выручка ({current.archiveDays} дн): {income}\n";
                                        if (info.fee != null)
                                        {
                                            content += $"📋 Комиссия категории товара: {info.fee}%.\n";
                                        }
                                        if (orders.stock != null)
                                        {
                                            uint stockbuyout = orders.buyoutCount(current.stockDays);
                                            if (Math.Abs(filter) == 2)
                                                content += "<b>";
                                            content += $"📦 Остаток: {orders.stock} (на {(stockbuyout > 0 ? (orders.stock.Value * current.stockDays / stockbuyout) : "∞")} дней)\n";
                                            if (Math.Abs(filter) == 2)
                                                content += "</b>";
                                            if (orders.stock.Value < stockbuyout)
                                            {
                                                content += $"❗️ Товар на исходе ❗️\n";
                                                content += $"\n 🛵 Пополнить на: {stockbuyout - orders.stock.Value}";
                                            }
                                        }
                                        

                                        return content + "\n";
                                    }
                            }
                    }
            }
        }
        public static BotPage LabelInfo(ClientLink client, PageQuery arg)
        {
            client.client!.messageCallback = GetProductLabelCallback;
            return (answers[answer.label_hint], null);
        }
        public static BotPage AdsInfo(ClientLink client, PageQuery arg)
        {
            switch (arg[0])
            {
                case null:

                    return ("Отслеживание рекламных ставок:", new (string, string)[]
                    {
                        ("👁‍🗨 Узнать рекламную ставку", arg.ReplyCallback("0")),
                        ("➕ Добавить отслеживание", arg.ReplyCallback("1")),
                        ("📘 Редактировать список РК", arg.ReplyCallback("edit")),
                    }.Markup());
                case "edit":
                    switch (arg[1])
                    {
                        case null:
                            (string, string)[] markup = new (string, string)[client.client.trackAds.Count + 2];
                            for (int i = 0; i < client.client.trackAds.Count; i++)
                            {
                                markup[i] = (client.client.trackAds[i].product, arg.ReplyCallback(i.ToString()));
                            }
                            markup[^2] = ("🔄 Перезакрепить соощения", arg.ReplyCallback("refresh"));
                            markup[^1] = arg.BackButton;
                            return ("*Отслеживаемые рекламные ставки:*\n\nℹ️ Стоимость рекламных ставок обновляется каждые 15 минут. Вы можете просматривать отслеживаемые ставки в закрепленных сообщениях, либо, выбрав интересующую ставку в списке отслеживаний.", markup.Markup());
                        case "refresh":
                            Task.Run(async () => await Bot.instance.botClient.UnpinAllChatMessages(client.clientId));
                            foreach (var trPos in client.client.trackAds)
                            {
                                try
                                {
                                    Bot.instance.botClient.PinChatMessageAsync(client.clientId, trPos.messageId);
                                }
                                catch (Exception e)
                                {
                                    Base.debugStream.Input(e);
                                }
                            }
                            arg = new PageQuery(arg.BackButton.callback);
                            return (null, null);
                        default:
                            int pos = int.Parse(arg[1]);

                            if (arg[2] == "delete")
                            {
                                if (pos < client.client.trackAds.Count)
                                {
                                    instance.botClient.UnpinChatMessageAsync(client.clientId, client.client.trackAds[pos].messageId);
                                    client.client.trackAds.RemoveAt(pos);
                                    return AdsInfo(client, arg.page + " edit");
                                }
                                return new BotPage("") { properties = BotPage.actionProp.delete };
                            }

                            (string, int) adPos = client.client.trackAds[pos];

                            return new BotPage($"🚩 Категория: {adPos.Item1}", new (string, string)[]
                            {
                                    ("Удалить отслеживание",arg.ReplyCallback("delete")),
                                    arg.BackButton,
                            }.Markup())
                            { replyMessage = adPos.Item2, properties = BotPage.actionProp.reply };

                            return new BotPage("") { properties = BotPage.actionProp.delete };
                    }
                default:
                    if (arg[1] == "1" && client.client.trackAds.Count >= 9)
                    {
                        return ("❗️ Достигнут лимит по отслеживанию РК (9).", arg.BackButton.Markup());
                    }

                    client.client!.messageCallback = (a, b) => GetAdsInfoCallback(a, b, arg[0]);
                    return (answers[answer.ads_position_hint], null);
            }
        }
        public static BotPage SearchPositionInfo(ClientLink client, PageQuery arg)
        {
            client.client!.messageCallback = GetProductPositionCallback;
            return (answers[answer.search_request_hint], null);
        }
        public static async Task<string?> GetProductLabelCallback(ClientLink client, string message)
        {
            try
            {
                uint numId = 0;

                if (!uint.TryParse(message, out numId))
                {
                    throw new MessageException(answers[answer.label_bad_request]);
                }

                return await WildberriesHandler.getItemLabel(numId);

            }
            catch (Exception e)
            {
                throw new MessageException(answers[answer.label_error]);
            }
        }


        public static async Task<string?> GetProductPositionCallback(ClientLink client, string message)
        {
            string[] args = message.Split();

            if (args.Length <= 1)
            {
                throw new MessageException(answers[answer.bad_search_request]);
            }

            string search_item = args[0];
            string search_request = string.Join(' ', args.Skip(1));

            await instance.botClient.EditMessageTextAsync(
                client.clientId,
                instance.botClient.SendTextMessageAsync(
                    client.clientId,
                    answers[answer.search_position_proccess]).Result.MessageId,
                await WildberriesHandler.getCategoryItems(search_request, uint.Parse(search_item)),
                ParseMode.Markdown);

            return null;
        }
        public static BotPage Support(ClientLink client, PageQuery arg)
        {

            return (answers[answer.support_message], ("Задать вопрос", "https://t.me/WbStarSupport").Markup());
        }
        public static BotPage News(ClientLink client, PageQuery arg)
            => (answers[answer.news_message], new (string, string)[] {
                            ("📰 Канал новостей", "https://t.me/WBstarbotinfo"),
                            ("☀️ Чат обсуждения", "https://t.me/+LuDRoJtbqhk4MWFi"),
            }.Markup());


        public static BotPage Report(ClientLink client, PageQuery arg)
        {
            switch (arg[0])
            {
                case null:
                    if (client.client!.clientDatas.Length > 1)
                    {
                        (string, string)[] datas = new (string, string)[client.client.clientDatas.Length];

                        for (int i = 0; i < datas.Length; i++)
                        {
                            datas[i] = (client.client.clientDatas[i][i], arg.ReplyCallback(i.ToString()));
                        }

                        return (answers[answer.products_select_api], datas.Markup());
                    }
                    return Report(client, arg.ReplyCallback("0"));
                default:
                    int id = int.Parse(arg[0]);
                    ClientData current = client.client.clientDatas[id];
                    string content = $"*Сводка\n\n{current[id]}*\n\n";
                    content += $"🛍 Всего товаров: {current.dataHandler.containers.Count}";
                    content += $"\n📦 Суммарный остаток: {current.dataHandler.containers.Where((a) => a.Value.stock != null).Select((a, b) => (int)a.Value.stock!.Value).Sum()}";

                    int onToday = orders(24);
                    int onBack = cancels(24);
                    int onSell = sells(24);

                    content += $"\n\n*Сегодня:*\n🚛 Заказы: {onToday}\n🚚 Возварты: {onBack}\n🛒 Продажи: {onSell}";
                    content += $"\n\n*Вчера:*\n🚛 Заказы: {orders(48) - onToday}\n🚚 Возварты: {cancels(48) - onBack}\n🛒 Продажи: {sells(48) - onSell}";
                    content += $"\n\n*За неделю:*\n🚛 Заказы: {orders(168)}\n🚚 Возварты: {cancels(168)}\n🛒 Продажи: {sells(168)}";
                    content += $"\n\n*За месяц:*\n🚛 Заказы: {orders(730)}\n🚚 Возварты: {cancels(730)}\n🛒 Продажи: {sells(730)}";

                    int orders(int hours) => current.dataHandler.containers.Values.Select(a => a.orders.orderedData).Select(a => a.TakeWhile(a => DateTime.Now.Subtract(a.date).TotalHours <= hours)).Select(a => a.Count()).Sum();
                    int cancels(int hours) => current.dataHandler.containers.Values.Select(a => a.orders.orderedData).Select(a => a.TakeWhile(a => DateTime.Now.Subtract(a.date).TotalHours <= hours)).Select(a => a.Where(a => a.isCancel)).Select(a => a.Count()).Sum();
                    int sells(int hours) => current.dataHandler.containers.Values.Select(a => a.sales.orderedData).Select(a => a.TakeWhile(a => DateTime.Now.Subtract(a.date).TotalHours <= hours)).Select(a => a.Count()).Sum();

                    return (content, client.client.clientDatas.Length > 1 ? arg.BackButton.Markup() : null);
            }
        }
        public static BotPage StarFallInfo(ClientLink client, PageQuery arg) => new BotPage("🌟 *Режим звездопад:*\n\nℹ️ Данный режим обеспечивает работу уведомлений во время неполадок со стороны Wildberries. Когда сервера WB перестают выдавать списки заказов, данный режим самостоятельно анализирует заказы на основе остатков товара.\n\n🔰 В таком формате бот не всегда корректно обрабатывает информацию о заказах из-за остутсвия возможности определения пополнений складов и логистических данных, так что в статистику заказы из режима \"Звездопад\" не попадают пока сервера WB не восстановят работу и информация о заказах не подтвердиться.\n\n🔔 Использование режима может быть включено Администратором ИП в настройках личного кабинета (/my).");
        public static async Task<string?> GetAdsInfoCallback(ClientLink client, string message, string type)
        {
            if (message.Length < 2)
            {
                throw new MessageException(answers[answer.bad_ads_request]);
            }

            Message msg = await instance.botClient.SendTextMessageAsync(client.clientId, answers[answer.ads_position_procces]);

            try
            {
                await instance.botClient.EditMessageTextAsync(client.clientId, msg.MessageId, WildberriesHandler.getCategoryCpmList(message), ParseMode.Markdown);
            }
            catch
            {
                await instance.botClient.EditMessageTextAsync(client.clientId, msg.MessageId, answers[answer.ads_position_fail], ParseMode.Markdown);
            }
            if (type == "1")
            {
                client.client.trackAds.Add((message, msg.MessageId));
                await instance.botClient.PinChatMessageAsync((long)client, msg.MessageId, true);
            }
            return null;
        }
    }
}

