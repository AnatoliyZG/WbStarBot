using System;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using WbStarBot.DataTypes;
using WbStarBot.Wildberries;

namespace WbStarBot
{

    public class ClientData
    {
        public delegate void ReciveFeedBack(ClientData data, ProductInfo info, uint nmId, string text, int? vl);

        [JsonIgnore]
        public static ReciveFeedBack reciveFeedBack;

        public string? Name = null;
        public string? phone = null;
        public string apiKey;

        public string this[int index] => $"{(active ? smiles[index] : "❌")} {Name ?? "Безымянный"}";

        private static string[] smiles = { "🍒", "🥝", "🍓", "🍎", "🍏", "🌽", "🍅", "🍌", "🍉" };

        public bool Unauth = false;

        [JsonIgnore]
        public double balance
        {
            get => (bonusBl + transferBl);
            set
            {
                bonusBl = value;

                if (bonusBl < 0)
                {
                    transferBl += bonusBl;
                    bonusBl = 0;
                }
            }
        }

        public double bonusBl;
        public double transferBl;

        public bool starfall;

        public string? promocode;

        [JsonIgnore]
        public DataHandler dataHandler;

        public List<Transaction> transactions = new List<Transaction>();
        public List<Pay> pays = new List<Pay>();

        [JsonIgnore]
        public long[] users => recivers.Keys.ToArray();


        public Dictionary<long, notify> recivers = new Dictionary<long, notify>();

        public bool stopped = false;

        [JsonIgnore]
        public long Admin
        {
            get => users[0];
            set
            {
                for (int j = 1; j < users.Length; j++)
                {
                    if (users[j] == value)
                    {
                        users[j] = users[0];
                        break;
                    }
                }
                users[0] = value;
            }
        }

        [JsonIgnore]
        public bool active => balance > 0 && !stopped;

        public DateTime lastPaid;
        public DateTime registerTime;

        public byte archiveDays = 7;
        public byte stockDays = 14;

        public ClientData(string apiKey, long reciver)
        {
            this.recivers.Add(reciver, notify.All);
            this.apiKey = apiKey;
            dataHandler = new DataHandler(this);
            registerTime = DateTime.Now;
        }

        public ClientData()
        {
            dataHandler = new DataHandler(this);
            registerTime = DateTime.Now;
        }

        [JsonIgnore]
        public string ShowBalance => $"\n\n💰 Текущий баланс: {balance / 1000} руб.";

        public void AddBonusBalance(uint rub)
        {
            bonusBl += rub * 1000;
            CheckBalance();
        }

        public void AddTransferBalance(uint rub)
        {
            transferBl += rub * 1000;
            CheckBalance();
        }

        public void CheckBalance()
        {
            if (active)
                if (DateTime.Now.Subtract(lastPaid).TotalDays >= 7)
                {
                    lastPaid = DateTime.Now;
                    balance -= CONSTS.WeekCost * 1000;
                    pays.Add(new Pay());

                    if (pays.Count > 4)
                    {
                        pays.RemoveAt(0);
                    }
                }
        }
    }

    [Flags]
    public enum notify : uint
    {
        none = 0,
        Feedback = 1,
        Orders = 2,
        Sells = 4,
       // WeekReport = 8,
        All = 7,
    }

    /*
    public class userSettings
    {
        public notify Notify = notify.All;
        pub
    }
    */

    public class DataHandler : IDisposable
    {
        public DateTime lastOrdersUpdate = new DateTime(2019, 1, 1);
        public DateTime lastStocksUpdate = new DateTime(2019, 1, 1);
        public DateTime lastSalesUpdate = new DateTime(2019, 1, 1);

        public Dictionary<uint, ProductInfo> products = new Dictionary<uint, ProductInfo>();
        public Dictionary<uint, DataContainer> containers = new Dictionary<uint, DataContainer>();

        [JsonIgnore]
        private HashSet<DataContainer> updateContainers = new HashSet<DataContainer>();

        [JsonIgnore]
        public ClientData data;

        public DataHandler(ClientData data)
        {
            this.data = data;
        }
        private T[] ContainerFill<T>(string? content, ref DateTime lastUpdate, Func<DataContainer, T, bool> update) where T : IData
        {
            if (content == null) return null;

            List<T> newData = new List<T>();
            T[]? data = JsonConvert.DeserializeObject<T[]?>(content);

            if (data is not null)
            {
                foreach (T current in data)
                {
                    DataContainer container = null;

                    if (!containers.ContainsKey(current.primalKey.Value))
                    {
                        container = new DataContainer(current.primalKey.Value);
                        containers.Add(current.primalKey.Value, container);
                    }
                    else
                    {
                        container = containers[current.primalKey.Value];
                    }
                    
                    if (update.Invoke(container, current))
                    {
#if DEBUG
                        Base.debugStream.Input($"New DC: {current.dictionaryKey} {current.primalKey} {current.lastChangeDate}", MessageType.client);
#endif
                        newData.Add(current);
                        updateContainers.Add(container);
                    }
                }

                if (data.Length > 0)
                    lastUpdate = data[^1].lastChangeDate;
            }
            return newData.ToArray();
        }

        public (OrderData[], SaleData[]) Update(string? orders, string? sales) => (
            ContainerFill<OrderData>(orders, ref lastOrdersUpdate, (a, b) => a.Update(b)),
            ContainerFill<SaleData>(sales, ref lastSalesUpdate, (a, b) => a.Update(b))
            );

        public async Task ProductInfoLoad()
        {
            try
            {
                foreach (DataContainer dataContainer in updateContainers)
                {
                    if (dataContainer.root == null)
                    {
                        if (!dataContainer.Upload()) continue;
                    }

                    ProductInfo info = new ProductInfo();

                    if (!products.TryGetValue(dataContainer.root.Value, out info))
                    {
                        if (info == null)
                            info = new ProductInfo();

                        products.Add(dataContainer.root.Value, info);
                    }else if(info == null)
                    {
                        products[dataContainer.root.Value] = info;
                    }

                    WildberriesHandler.GetProductInfo(info, dataContainer.root.Value, (a, b, c) => ReciveFeedback(dataContainer.nmId, a, b, c));

                    if (info.name == null)
                    {
                        info.name = await WildberriesHandler.GetItemName(dataContainer.nmId);
                    }

                    if (data.active)
                    {
                        if (dataContainer.orders.lastElement != null)
                        {
                            OrderData order = dataContainer.orders.lastElement;

                            if (info.fee == null)
                            {
                                    info.fee = OutputHandler.GetFee(order.category, order.subject);
                            }

                            if (order.subject != null)
                            {
                                info.searchPosition.currentPosition = await WildberriesHandler.GetPosNum(order.subject!, dataContainer.nmId);
                            }
                            else
                            {
                                Base.debugStream.Input($"Order subject is null!! {data.Name}", MessageType.error);
                            }
                        }

                    }
                }
            }
            catch (Exception e)
            {
                Base.debugStream.Input($"{data.Name}, containers: {updateContainers.Count}, error: {e}", MessageType.error);
            }
            finally
            {
                if (updateContainers != null)
                    updateContainers.Clear();

                if (data != null)
                    data.CheckBalance();
            }
        }

        private void ReciveFeedback(uint nmId, ProductInfo info, string text, int? vl)
        {
            ClientData.reciveFeedBack?.Invoke(data, info, nmId, text, vl);
        }

        public void Dispose()
        {
            foreach (DataContainer container in containers.Values)
            {
                container.Dispose();
            }
        }
    }

    public interface IData
    {
        public uint? primalKey { get; }
        public ulong? dictionaryKey { get; }
        public bool Canceled { get; }
        public DateTime lastChangeDate { get; set; }
        public DateTime date { get; set; }

        public void Dispose();
    }
}

