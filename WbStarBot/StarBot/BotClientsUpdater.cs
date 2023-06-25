using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WbStarBot.Wildberries;
using WbStarBot.DataTypes;
using System.Collections.Generic;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using WbStarBot.Cloud;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Wordprocessing;

namespace WbStarBot.Telegram
{
    public partial class Bot : BaseHandler, IDisposable
    {


        public void StartClientsHandler()
        {
            new Thread(new ThreadStart(ClientsUpdate)).Start();
            new Thread(new ThreadStart(StarFallUpdate)).Start();
        }

        public void ClientsUpdate()
        {
            while (true)
            {
                Thread.Sleep(900000);
                try
                {
                    WildberriesHandler.ClientsDataUpdate(clientsDatas.Values, OrdersHandler, SalesHandler);
                }
                catch (Exception e)
                {
                    debugStream.Input(e);
                }
            }
        }

        public void StarFallUpdate()
        {
            while (true)
            {
                Thread.Sleep(1800000);
                try
                {
                    foreach (var item in clientsDatas.Values)
                    {
                        if (item.active && item.starfall)
                            Task.Run(() => WildberriesHandler.StarfallUpdate(item, StarFallHandler));
                    }
                }
                catch (Exception e)
                {
                    debugStream.Input(e);
                }
            }
        }

        public void AdsTrackUpdate()
        {
            while (true)
            {
                Thread.Sleep(900000);
                try
                {
                    foreach (KeyValuePair<long, Client> cl in clients)
                    {
                        if (cl.Value.trackAds.Count > 0)
                            Task.Run(() => AdsTrackerHandler(new ClientLink(cl.Key)));
                    }
                }
                catch (Exception e)
                {
                    debugStream.Input(e);
                }
            }
        }

        public async Task StarFallHandler(ClientData data, object? arg)
        {
            try
            {
                (DataContainer container, uint count)? nm = arg as (DataContainer order, uint count)?;

                if (nm == null) return;
                DataContainer container = nm.Value.container;
                OrderData? order = container.orders.lastElement;
                (int canceled, int all) buyout = container.buyout(data.archiveDays);
                (int, float)[] daysBuyout = container.daysBuyout();

                for (int i = 0; i < nm.Value.count; i++)
                {
                    if (order == null) return;

                    ProductInfo info = data.dataHandler.products[container.root.Value];

                    string content = $"";
                    content += $"_{DateTime.Now}_\n\n*{(order.isCancel ? "🚚 Возврат" : "🚛 Заказ")} товара*\n\n";
                    content += $"💵 Сегодня заказов: {daysBuyout[0].Item1} на {daysBuyout[0].Item2}р.\n";

                    content += $"🆔 ID товара: `{order.nmId}`\n";
                    content += $"🏷 {order.Brand} | [{order.supplierArticle}](https://www.wildberries.ru/catalog/{order.nmId}/detail.aspx)\n";
                    content += $"📁 {order.category} | {order.techSize}\n";
                    if (info.valuation != null)
                    {
                        content += $"{string.Join("", "⭐️⭐️⭐️⭐️⭐️".Take((int)Math.Round(info.valuation.Value * 2)))} ({info.valuation})\n";
                    }
                    content += $"💬 Отзывы: {info.feedbackCount}\n";

                    if (!order.isCancel)
                    {
                        content += $"💰 Выручка: {order.price}р.\n";

                        if (info.fee != null)
                        {
                            content += $"📋 Ожидаемая комиссия: {info.fee}%.\n";
                        }
                    }

                    content += $"💸 Вчера заказов: {daysBuyout[1].Item1} на {daysBuyout[1].Item2}р.\n";
                    content += $"💎 Выкупы ({data.archiveDays} дн.): {100 - (int)((float)buyout.canceled / buyout.all * 100f)}% ({buyout.all - buyout.canceled}/{buyout.all})\n";

                    if (container.stock != null)
                    {
                        uint stockbuyout = container.buyoutCount(data.stockDays);
                        content += $"\n📦 Остаток: {container.stock} (на {(stockbuyout > 0 ? container.stock.Value * data.stockDays / stockbuyout : "∞")} дней)";
                        if (container.stock.Value < stockbuyout)
                        {
                            content += $"\n❗️ Товар на исходе ❗️";
                            content += $"\n 🛵 Пополнить на: {stockbuyout - container.stock.Value}";
                        }
                    }
                    content += "\n\n🌟 Отправлено в режиме /starfall";
                    SendNotify(data, content, container.nmId, notify.Orders, i != 0);

                }

            }
            catch (Exception e)
            {
                debugStream.Input(e);
            }
        }

        private string orderInfo()
        {
            string content = "";

            return content;
        }

        private async void AdsTrackerHandler(ClientLink client)
        {
            int i = 0;
            try
            {
                foreach ((string category, int message) track in client.client.trackAds)
                {
                    string s;
                    try
                    {
                        s = WildberriesHandler.getCategoryCpmList(track.category);
                    }
                    catch
                    {
                        i++;
                        continue;
                    }
                    try
                    {
                        await botClient.EditMessageTextAsync(client.clientId, track.message, s);
                    }
                    catch
                    {
                        await botClient.UnpinChatMessageAsync(client.clientId, track.message);
                        Message msg = await botClient.SendTextMessageAsync(client.clientId, s);
                        await botClient.PinChatMessageAsync(client.clientId, msg.MessageId);
                        client.client.trackAds[i] = (track.category, msg.MessageId);
                    }
                    i++;
                }
            }
            catch (Exception e)
            {
                debugStream.Input(e);
            }
        }

        private async Task OrdersHandler(ClientData data, object? arg)
        {
            try
            {

                OrderData[]? orders = arg as OrderData[] ?? null;


                if (orders == null)
                    return;

                bool silent = false;

                foreach (OrderData order in orders)
                {
                    uint nmId = order.nmId.Value;

                    DataContainer container = data.dataHandler.containers[nmId];

                    ProductInfo info = data.dataHandler.products[container.root.Value];
                    (int canceled, int all) buyout = container.buyout(data.archiveDays);
                    (int, float)[] daysBuyout = container.daysBuyout();


                    string content = $"";
                    content += $"_{order.lastChangeDate}_\n\n*{(order.isCancel ? "🚚 Возврат" : "🚛 Заказ")} товара*\n\n";

                    content += $"💵 Сегодня заказов: {daysBuyout[0].Item1} на {daysBuyout[0].Item2}р.\n";
                    content += $"🆔 ID товара: `{order.nmId}`\n";
                    content += $"🏷 {order.Brand} | [{order.supplierArticle}](https://www.wildberries.ru/catalog/{order.nmId}/detail.aspx)\n";
                    content += $"📁 {order.category} | {order.techSize}\n";
                    if (info.valuation != null)
                    {
                        content += $"{string.Join("", "⭐️⭐️⭐️⭐️⭐️".Take((int)Math.Round(info.valuation.Value * 2)))} ({info.valuation})\n";
                    }
                    content += $"💬 Отзывы: {info.feedbackCount}\n";

                    if (!order.isCancel)
                    {
                        content += $"💰 Выручка: {order.price}р.\n";

                        if (info.fee != null)
                        {
                            content += $"📋 Ожидаемая комиссия: {info.fee}%.\n";
                        }
                    }

                    content += $"💸 Вчера заказов: {daysBuyout[1].Item1} на {daysBuyout[1].Item2}р.\n";
                    content += $"💎 Выкупы ({data.archiveDays} дн.): {100 - (int)((float)buyout.canceled / buyout.all * 100f)}% ({buyout.all - buyout.canceled}/{buyout.all})\n";

                    // content += $"{(order.isCancel ? "🚚" : "🚛")} Статус: {(order.isCancel ? "Возврат" : "В пути")}\n";
                    content += $"🌐 {order.warehouseName} → {order.oblast}\n";

                    if (container.stock != null)
                    {
                        uint stockbuyout = container.buyoutCount(data.stockDays);
                        content += $"\n📦 Остаток: {container.stock} (на {(stockbuyout > 0 ? container.stock.Value * data.stockDays / stockbuyout : "∞")} дней)";
                        if (container.stock.Value < stockbuyout)
                        {
                            content += $"\n❗️ Товар на исходе ❗️";
                            content += $"\n 🛵 Пополнить на: {stockbuyout - container.stock.Value}";

                        }
                    }
                    SendNotify(data, content, nmId, notify.Orders, silent);

                    if(!silent) { silent = true; }
                }
            }
            catch (Exception e)
            {
                debugStream.Input(e);
            }
        }

        private async Task SalesHandler(ClientData data, object? arg)
        {
            try
            {
                SaleData[]? sales = arg as SaleData[] ?? null;

                if (sales == null)
                    return;
                bool silent = false;
                foreach (SaleData sale in sales)
                {
                    if (sale.isCancel) continue;
                    uint nmId = sale.nmId.Value;

                    DataContainer container = data.dataHandler.containers[nmId];

                    ProductInfo info = data.dataHandler.products[container.root.Value];

                    string content = $"";
                    content += $"_{sale.lastChangeDate}_\n\n💫 *Выкуп товара*\n\n";
                    content += $"🆔 ID товара: `{sale.nmId}`\n";
                    content += $"🏷 {sale.Brand} | [{sale.supplierArticle}](https://www.wildberries.ru/catalog/{sale.nmId}/detail.aspx)\n";
                    content += $"📁 {sale.category} | {sale.techSize}\n";
                    //    content += $"🛠 Сборка: {order.barcode}\n";

                    if (info.valuation != null)
                    {
                        content += $"{string.Join("", "⭐️⭐️⭐️⭐️⭐️".Take((int)Math.Round(info.valuation.Value * 2)))} ({info.valuation})\n";
                    }
                    content += $"💬 Отзывы: {info.feedbackCount}\n";

                    if (!sale.isCancel)
                    {
                        content += $"💰 К перечислению: {sale.forPay}р.\n";
                    }

                    (int canceled, int all) buyout = container.buyout(data.archiveDays);
                    (int, float)[] daysBuyout = container.daysBuyout();

                    content += $"💵 Сегодня заказов:{daysBuyout[0].Item1} на {daysBuyout[0].Item2}р.\n";
                    content += $"💸 Вчера заказов:{daysBuyout[1].Item1} на {daysBuyout[1].Item2}р.\n";
                    content += $"💎 Выкупы ({data.archiveDays} дн.): {100 - (int)((float)buyout.canceled / buyout.all * 100f)}% ({buyout.all - buyout.canceled}/{buyout.all})\n";

                    // content += $"{(sale.isCancel ? "🚚" : "🚛")} Статус: {(sale.isCancel ? "Возврат" : "Продажа")}\n";
                    if (container.stock != null)
                    {
                        uint stockbuyout = container.buyoutCount(data.stockDays);
                        content += $"\n📦 Остаток: {container.stock} (на {(stockbuyout > 0 ? container.stock.Value * data.stockDays / stockbuyout : "∞")} дней)";
                        if (container.stock.Value < stockbuyout)
                        {
                            content += $"\n❗️ Товар на исходе ❗️";
                            content += $"\n 🛵 Пополнить на: {stockbuyout - container.stock.Value}";

                        }
                    }

                    if (info.searchPosition.currentPosition != null)
                    {
                        content += $"\n\n🔍 Позиции в поиске:\n\n";
                        content += $"{(info.searchPosition.searchUpper ? "⬆️" : "⬇️")} [{sale.subject}](https://www.wildberries.ru/catalog/0/search.aspx?sort=popular&search={string.Join('+', sale.subject.Split())}): ({info.searchPosition.page}-{info.searchPosition.pagePositon})\n";
                    }
                    SendNotify(data, content, nmId, notify.Sells, silent);
                    if (!silent) { silent = true; }

                }
            }
            catch (Exception e)
            {
                debugStream.Input(e);
            }
        }
        public void Dispose()
        {
            cancellationToken.Cancel();
        }
    }
}

