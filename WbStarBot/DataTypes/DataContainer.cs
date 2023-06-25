using System;
using System.Linq;
using WbStarBot.Wildberries;
using DocumentFormat.OpenXml.EMMA;
using Newtonsoft.Json;
using DocumentFormat.OpenXml.Drawing.Charts;

namespace WbStarBot.DataTypes
{
    public class DataContainer : IDisposable
    {
        public uint nmId;
        public uint? stock = null;
        public uint? root = null;

        public DataCell<OrderData> orders = new DataCell<OrderData>();
        public DataCell<SaleData> sales = new DataCell<SaleData>();

        [JsonIgnore]
        public IData lastElement => orders.data.Count > 0 ? orders.lastElement : sales.lastElement;

        private int canceled => orders.data.Values.Where(a => a.isCancel).Count();

        public DataContainer(uint nmId)
        {
            this.nmId = nmId;

            Upload();
        }

        public bool Upload()
        {
            (uint? stock, uint? root) vl = WildberriesHandler.GetItemHead(nmId);

            this.stock = vl.stock;
            this.root = vl.root;

            return root != null;
        }

        //СДЕЛАТЬ АПДЕЙТ!!!!!!!!!!!!!!!!!
        public bool Update(OrderData order) 
        {
            return orders.Update(order);
        }
        public bool Update(SaleData sale)
        {
            return sales.Update(sale);
        }

        public void Dispose()
        {
            orders.Dispose();
            sales.Dispose();
        }

        public (int cenceled, int count) buyout(byte archiveDays)
        {
            OrderData[] ord = orders.orderedData.TakeWhile(a => DateTime.Now.Date.Subtract(a.lastChangeDate.Date).TotalDays <= archiveDays).ToArray();

            return (ord.Where(a=>a.isCancel).Count(), ord.Length);
        }

        public float buyoutProcent(byte archiveDays)
        {
            (int, int) b = buyout(archiveDays);

            if (b.Item2 == 0)
                return 0;

            return 100f - (float)b.Item1 / b.Item2 * 100f;
        }

        public uint buyoutCount(byte archiveDays)=>
             (uint)orders.orderedData.TakeWhile(a => DateTime.Now.Date.Subtract(a.lastChangeDate.Date).TotalDays <= archiveDays).Where(a => !a.isCancel).Count();
        
        public (int, float)[] daysBuyout()
        {
            (int, float)[] buyouts = new (int, float)[2] {
                (0,0),
                (0,0)
            };

            foreach (OrderData data in orders.orderedData)
            {
                if (data.isCancel) continue;

                double days = DateTime.Now.Date.Subtract(data.lastChangeDate.Date).TotalDays;

                if (days < 1)
                {
                    buyouts[0].Item1++;
                    buyouts[0].Item2 += data.price;
                }
                else if (days < 2)
                {
                    buyouts[1].Item1++;
                    buyouts[1].Item2 += data.price;
                }
                else
                {
                    return buyouts;
                }
            }
            return buyouts;
        }
    }

    public class DataCell<T> : IDisposable where T : class, IData
    {
        public Dictionary<ulong, T> data = new Dictionary<ulong, T>();
        [JsonIgnore]
        public int Count => data.Count;
        [JsonIgnore]
        public T? lastElement => (data.Count > 0 ? (data.Values.TakeLast(1).First()) : null);
        [JsonIgnore]
        public IOrderedEnumerable<T> orderedData => data.Values.OrderByDescending(a => a.date);

        public int periodData(int days,bool canceled)
        {
            return orderedData.TakeWhile(a => DateTime.Now.Subtract(a.date).TotalDays <= days).Where(a=>a.Canceled == canceled).Count();
        }

        public bool Update(T element)
        {
            if (element == null) return false;

            if (!data.TryAdd(element.dictionaryKey!.Value, element))
            {
                bool resistance = data[element.dictionaryKey!.Value].Canceled != element.Canceled;
                if(resistance)
                    data[element.dictionaryKey!.Value] = element;
                return resistance;
            }

            return true;
        }

        public void Dispose()
        {
            foreach (T i in data.Values.SkipLast(1))
            {
                i.Dispose();
            }
        }
    }
}

