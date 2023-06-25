using System;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WbStarBot.DataTypes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Compression;
using static WbStarBot.DebugStream;
using static System.Net.WebRequestMethods;

using File = System.IO.File;
using Color = SixLabors.ImageSharp.Color;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.EMMA;

namespace WbStarBot.Wildberries;

public delegate Task NewDataHandler(ClientData data, object? objs);

internal static class WildberriesHandler
{
    private const string baseUrl = "https://statistics-api.wildberries.ru/api/v1/supplier/";
    private static DateTime ordersAlive = DateTime.Now;


    public static long[] baskets = new long[]
    {
            20000000,
            30000000,
            40000000,
            72320000,
            100000000,
            110000000,
            115000000,
            120000000,
    };

    public static async Task<string> GetAccountName(uint nmId)
    {
        HttpResponseMessage message = await new HttpClient().GetAsync($"https://wbx-content-v2.wbstatic.net/sellers/{nmId}.json");
        string content = new StreamReader(message.Content.ReadAsStream()).ReadToEnd();

        return JObject.Parse(content).GetValue("supplierName")?.Value<string>() ?? "Безымянный";
    }

    public static async Task<string?> DataUpdate<T>(ClientData clientData) where T : IData
    {
        DateTime querryDate = DateTime.MinValue;
        string request = "";

        Type type = typeof(T);

        if (type == typeof(OrderData))
        {
            request = "orders";
            querryDate = clientData.dataHandler.lastOrdersUpdate;
        }
        else if (type == typeof(StockData))
        {
            request = "stocks";
            querryDate = clientData.dataHandler.lastStocksUpdate;
        }
        else if (type == typeof(SaleData))
        {
            request = "sales";
            querryDate = clientData.dataHandler.lastSalesUpdate;
        }
        string date = querryDate.ToString("s");

        string url = $"{baseUrl}{request}?dateFrom={date}";

        try
        {
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(clientData.apiKey);

            HttpResponseMessage message = await httpClient.GetAsync(url);
            switch (message.StatusCode)
            {
                case HttpStatusCode.TooManyRequests:
                    throw new WildberriesException(WildberriesException.ExceptionType.data_too_many_request);
                case HttpStatusCode.BadRequest or HttpStatusCode.BadRequest:
                    throw new WildberriesException(WildberriesException.ExceptionType.data_bad_request);
                case HttpStatusCode.Unauthorized:
                    clientData.Unauth = true;
                    throw new WildberriesException(WildberriesException.ExceptionType.Unauthorized);
                case HttpStatusCode.OK:
                    return new StreamReader(message.Content.ReadAsStream()).ReadToEnd();
                default:
                    Console.WriteLine(message.StatusCode.ToString());
                    throw new WildberriesException(WildberriesException.ExceptionType.unk_exception);

            }
        }
        catch (WebException ex)
        {
            throw new WildberriesException(ex);
        }
    }

    public static void ClientsDataUpdate(IEnumerable<ClientData> clients, NewDataHandler? ordersHandler = null, NewDataHandler? salesHandler = null)
    {
        foreach (ClientData data in clients)
        {
            try
            {
                if (data.active && !data.starfall)
                    Task.Run(() => ClientDataUpdate(data, ordersHandler, salesHandler));
            }catch(Exception ex)
            {
                Base.debugStream.Input(ex);
            }
        }
    }

    public static async Task ClientDataUpdate(ClientData clientData, NewDataHandler? ordersHandler = null, NewDataHandler? salesHandler = null)
    {
        try
        {
            Task<string?> task1 = Task.Run<string?>(async () => await DataUpdate<OrderData>(clientData));
            Task<string?> task2 = Task.Run<string?>(async () => await DataUpdate<SaleData>(clientData));

            //Task task2 = Task.Run(async () => await DataUpdate(clientData, clientData.Stocks));

            await task1;
            await task2;

            (OrderData[], SaleData[]) newData = clientData.dataHandler.Update(task1.Result, task2.Result);

            await clientData.dataHandler.ProductInfoLoad();

            if(ordersHandler != null)
            await ordersHandler?.Invoke(clientData, newData.Item1);
            if(salesHandler !=  null)
            await salesHandler?.Invoke(clientData, newData.Item2);

            clientData.dataHandler.Dispose();


            if (newData.Item1.Length > 0)
            {
                ordersAlive = DateTime.Now;
            }
        }
        catch (WildberriesException ex)
        {
            Base.debugStream.Input($"{clientData.Name} {clientData.apiKey} {ex}");
            return;
        }
        catch (Exception ex)
        {
            Base.debugStream.Input(ex);
            return;
        }

        if (clientData.Name is null)
        {
            try
            {
                if (clientData.dataHandler.containers.Count > 0)
                {
                    clientData.Name = await GetAccountName(clientData.dataHandler.containers.ElementAt(0).Key);
                }
            }
            catch (Exception ex)
            {
                Base.debugStream.Input(ex);
                throw ex;
            }
        }
    }

    public static async Task<string> getItemLabel(uint numId)
    {
        string name = await GetItemName(numId);
        (uint? stock, uint? root) vl = GetItemHead(numId);
        ProductInfo info = new ProductInfo();
        GetProductInfo(info, vl.root.Value);
        return $"📘 Товар: [{name}](https://www.wildberries.ru/catalog/{numId}/detail.aspx)\n\n🆔 ID товара: `{numId}`\n⭐️ Рейтинг: {info.valuation}\n💬 Отзывы: {info.feedbackCount}\n📦 Остаток: {vl.stock}\n\n⚠️ Данная функция была добавлена недавно и еще неоднократно получит обновление.";
    }

    public static async Task<string> getCategoryItems(string category, uint numId)
    {
        Task<string> moscow = GetPos(category, numId, -1029256, -102269, -2162196, -1257218);
        Task<string> saratov = GetPos(category, numId, -1075831, -72193, -2725551, 12358461);
        Task<string> ekaterin = GetPos(category, numId, -1113276, -79379, -1104258, -5803327);
        Task<string> novosibirsk = GetPos(category, numId, -1221148, -140294, -1751445, -364763);
        Task<string> habarovsk = GetPos(category, numId, -1221185, -151223, -1782064, -1785054);


        await moscow;
        await ekaterin;
        await saratov;
        await novosibirsk;
        await habarovsk;

        return $"👀 Позиция товара в поиске:\n\nℹ️ Запрос: `{numId} {category}`\n\nМосква: {moscow.Result}\n\nСаратов: {saratov.Result}\n\nЕкатеринбург: {ekaterin.Result}\n\nНовосибирск: {novosibirsk.Result}\n\nХабаровск: {habarovsk.Result}";
    }

    public static async Task<string> GetPos(string category, uint numId, int x = -1029256, int y = -102269, int w = -2162196, int z = -1257218)
    {
        int? Position = await GetPosNum(category, numId, x, y, w, z);

        if (Position != null)
        {
            return $"{Position.Value / 100 + 1} страница, {Position.Value % 100} карточка";
        }
        return "не ранжируется на первых 50 стр.";
    }

    public static async Task<int?> GetPosNum(string category, uint numId, int x = -1029256, int y = -102269, int w = -2162196, int z = -1257218)
    {
        try
        {
            for (byte page = 0; page <= 30; page++)
            {
                byte position = 1;

                string link = $"https://search.wb.ru/exactmatch/ru/common/v4/search?appType=1&dest={x},{y},{w},{z}&emp=0&lang=ru&locale=ru&page={page + 1}&pricemarginCoeff=1.0&reg=0&resultset=catalog&sort=popular&suppressSpellcheck=false&query={category}";
                HttpContent client = (await (new HttpClient().GetAsync(link))).Content;
                string ps = new StreamReader(client.ReadAsStream()).ReadToEnd();
                JObject data = JObject.Parse(ps);
                var token = data.GetValue("data")?.Value<JObject>()?.GetValue("products")?.Values<JObject>() ?? null;

                if (token != null)
                {
                    foreach (JObject obj in token)
                    {
                        if (numId == obj.GetValue("id").Value<long>())
                        {
                            return page * 100 + position;
                        }

                        position++;
                    }
                }
                else
                {
                    break;
                }
            }
        } catch (Exception e)
        {
            Console.WriteLine($"Get position error: ({category}, {numId}, {x}, {y}, {w}, {z}).\n{e}");
            Base.debugStream.Input($"Get position error: ({category}, {numId}, {x}, {y}, {w}, {z}).\n{e}", MessageType.error);
        }
        return null;
    }

    public static string getCategoryCpmList(string category)
    {
        string content = $"🔎 Категория: {category}\n\n";

        try
        {
            string link = $"https://catalog-ads.wildberries.ru/api/v5/search?keyword={category}";
            HttpContent client = new HttpClient().GetAsync(link).Result.Content;
            JObject data = JObject.Parse(new StreamReader(client.ReadAsStream()).ReadToEnd());
            var token = data.GetValue("adverts")?.Values<JObject>() ?? null;

            int ind = 0;
            foreach (JObject obj in token)
            {
                content += $"{ind + 1} место: {obj.GetValue("cpm")?.Value<int>()} руб.\n";

                ind++;

                if (ind >= 5)
                    break;
            }
        } catch (Exception e)
        {
            throw new Exception("❌ Не удалось получить cpm данной категории!\n Возможно, сервера WB немного шалят и все получиться, если вы повторите попытку еще раз.");
        }
        return content;
    }

    public static async Task<string> GetItemName(uint numId)
    {
        int basket = get_basket(numId);

        while (true)
        {
            try
            {
                string addr = $"https://basket-0{basket}.wb.ru/vol";
                HttpResponseMessage msg = await (new HttpClient().GetAsync($"{addr}{numId / 100000}/part{numId / 1000}/{numId}/info/ru/card.json"));
                JObject data = JObject.Parse(new StreamReader(msg.Content.ReadAsStream()).ReadToEnd());
                string? token = data.GetValue("imt_name")?.Value<string>() ?? null;

                if (token == null)
                    return "Без названия";

                return token!;

            }
            catch
            {
                if (basket >= 9)
                    return "Без названия";
                Base.debugStream.Input($"Skipped {basket} basket for {numId}", MessageType.warning);
                basket++;
            }
        }
    }
    public static bool GetItemImage(uint numId)
    {
        if (File.Exists($"{OutputHandler.productImageDir}/{numId}.jpeg"))
            return true;

        using (WebClient client = new WebClient())
        {
            Image? bmp1 = getBmp(client, 1);
            Image? bmp2 = getBmp(client, 2);
            Image? bmp3 = getBmp(client, 3);

            if (bmp1 != null)
            {
                int wd = bmp1.Width;
                int hg = bmp1.Height;

                using (Image<Rgba32> img = new Image<Rgba32>(246 * 3, 328, Color.Black))
                {
                    img.Mutate(c => c.DrawImage(bmp1, new Point(0, 0), 1));
                    if (bmp2 != null)
                    {
                        img.Mutate(c => c.DrawImage(bmp2, new Point(wd, 0), 1));

                        if (bmp3 != null)
                        {
                            img.Mutate(c => c.DrawImage(bmp3, new Point(wd * 2, 0), 1));
                        }
                    }

                    img.SaveAsJpeg($"{OutputHandler.productImageDir}/{numId}.jpeg");
                }
                bmp1?.Dispose();
                bmp2?.Dispose();
                bmp3?.Dispose();
            }
            else
            {
                try
                {
                    using (Image<Rgba32> img = new Image<Rgba32>(246 * 3, 328, Color.Black))
                    {
                        img.SaveAsJpeg($"{OutputHandler.productImageDir}/{numId}.jpeg");
                    }
                }
                catch (Exception e)
                {
                    Base.debugStream.Input(e);
                    return false;
                }
            }

            return true;
        }

        Image? getBmp(WebClient client, int index)
        {
            int basket = get_basket(numId);

            while (true)
            {
                try
                {
                    string addr = $"https://basket-0{basket}.wb.ru/vol";

                    return Image.Load(client.OpenRead($"{addr}{numId / 100000}/part{numId / 1000}/{numId}/images/c246x328/{index}.jpg"));
                }
                catch
                {
                    if (basket >= 9)
                        return null;
                    basket++;
                }
            }
        }
    }

    public static void StarfallUpdate(ClientData client, NewDataHandler newOrders)
    {
        foreach (DataContainer item in client.dataHandler.containers.Values)
        {
            (uint? stock, uint? root) inf = GetItemHead(item.nmId);

            if (inf.stock != null && inf.stock != 0)
            {
                if (item.stock != null && item.stock != 0 && item.stock > inf.stock)
                {
                    if (item.root == null)
                    {
                        item.root = inf.root;
                    }
                    (DataContainer cont, uint count)? argument = (item, item.stock.Value - inf.stock.Value);
                    newOrders.Invoke(client, argument);
                }
                item.stock = inf.stock;
            }

        }

    }

    public static (uint? stock, uint? root) GetItemHead(uint numId)
    {
        uint? stock = 0;
        uint? root = 0;

        try
        {
            string link = $"https://card.wb.ru/cards/detail?pricemarginCoeff=1.0&appType=1&locale=ru&lang=ru&curr=rub&dest=-1059500,-72639,-3826860,-5551776&nm={numId}";
            HttpContent client = new HttpClient().GetAsync(link).Result.Content;

            string result = new StreamReader(client.ReadAsStream()).ReadToEnd();

            if (result == null || result.Length == 0)
                return (null, null);

            JObject data = JObject.Parse(result);

            var token = data.GetValue("data")?.Value<JObject>()?.GetValue("products")?.Values<JObject>() ?? null;

            if (token != null)
            {
                foreach (JObject obj in token)
                {
                    uint count = 0;

                    var sizes = obj?.GetValue("sizes")?.Values<JObject>() ?? null;

                    root = obj?.GetValue("root")?.Value<uint>() ?? null;


                    if (sizes != null)
                    {
                        foreach (var size in sizes)
                        {
                            returnCount(size.GetValue("stocks")?.Values<JObject>() ?? null);
                        }
                    }
                    else
                    {
                        returnCount(obj?.GetValue("stocks")?.Values<JObject>() ?? null);
                    }

                    void returnCount(IEnumerable<JObject?> stocks)
                    {
                        if (stocks != null)
                        {
                            foreach (JObject stock in stocks)
                            {
                                count += stock.GetValue("qty").Value<uint>();
                            }
                        }
                    }
                    stock = count;
                }
            }
        }
        catch (Exception e)
        {
            Base.debugStream.Input(e);
        }

        return (stock, root);
    }

    public static void GetProductInfo(ProductInfo productInfo, uint root, Action<ProductInfo, string, int?> newFeedBack = null)
    {
        if (productInfo == null)
        {
            Base.debugStream.Input("Product info cannot be null!", MessageType.error);
            return;
        }
#line 408
        string feedLink = $"https://feedbacks1.wb.ru/feedbacks/v1/{root}";

        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(feedLink);
        request.Method = WebRequestMethods.Http.Get;
        request.KeepAlive = true;
        request.Referer = "https://www.wildberries.ru/";
        request.Accept = "*/*";
        request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli;
        request.Host = "feedbacks1.wb.ru";
        request.UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.1 Safari/605.1.15";
        request.Headers.Add("Accept-Language", "ru");

        HttpWebResponse webResponse = (HttpWebResponse)request.GetResponse();

        string fcontent = new StreamReader(webResponse.GetResponseStream()).ReadToEnd();

        JObject feedData = JObject.Parse(fcontent);
        if (feedData != null)
        {
            float valuation = 0;
            float.TryParse(feedData?.GetValue("valuation")?.Value<string>() ?? "0", out valuation);
            int? feedbackCount = feedData?.GetValue("feedbackCount")?.Value<int>();

            productInfo.valuation = (float)valuation;

            if (feedbackCount != null && productInfo.feedbackCount != feedbackCount && newFeedBack != null)
            {
                IEnumerable<JObject?> feedbacks = null;
                if (feedData.HasValues)
                {
                    JToken jToken = null;
                    feedData?.TryGetValue("feedbacks", out jToken);

                    if (jToken.HasValues)
                        feedbacks = jToken.Values<JObject?>();
                }
                int? couter = feedbackCount - (productInfo.feedbackCount ?? 0);

                if (couter != null && couter > 0 && feedbacks != null)
                {
                    foreach (JObject fb in feedbacks)
                    {
                        DateTime? dt = fb.GetValue("createdDate")?.Value<DateTime>();

                        if (dt == null) continue;

                        if (dt > productInfo.lastUpd)
                        {
                            couter--;

                            string? text = fb.GetValue("text")?.Value<string>();
                            int? vl = fb.GetValue("productValuation")?.Value<int>();

                            if (text is not null)
                                newFeedBack?.Invoke(productInfo, text, vl);

                            if (couter <= 0)
                                break;
                        }
                    }
                }
            }
            productInfo.feedbackCount = feedbackCount;

            productInfo.lastUpd = DateTime.UtcNow;
        }
    }


    private static int get_basket(uint numId)
    {
        for (int i = 0; i < baskets.Length; i++)
        {
            if (numId < baskets[i])
            {
                return i + 1;
            }
        }
        return 9;
    }
}


