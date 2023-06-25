using System;
using Newtonsoft.Json;

namespace WbStarBot.DataTypes
{
    public class OrderData : IData
    {
        [JsonIgnore]
        public bool Canceled => isCancel;

        [JsonIgnore]
        public uint? primalKey { get => nmId; }

        [JsonIgnore]
        public ulong? dictionaryKey { get => odid; }

        public DateTime lastChangeDate { get; set; }
        public DateTime date { get; set; }

        public float totalPrice;
        public int discountPercent;
        public bool isCancel;

        public uint? nmId;
        public ulong? odid;
        public string? supplierArticle;
        public string? techSize;
        public string? warehouseName;
        public string? oblast;
        public string? brand;
        public string? category;
        public string? subject;
        //public string gNumber;
        //public int? incomeID;
        //public string barcode;
        //public DateTime date;

        [JsonIgnore]
        public int price
        {
            get
            {
                return (int)Math.Floor(totalPrice * (1f - discountPercent / 100f));
            }
        }

        [JsonIgnore]
        public string? Brand => brand?.Replace("_", "").Replace("*", "").Replace("`", "");


        public void Dispose()
        {
            nmId = null;
            odid = null;
            supplierArticle = null;
            techSize = null;
            warehouseName = null;
            oblast = null;
            brand = null;
            category = null;
            subject = null;
        }

    }
}

