using System;
namespace WbStarBot.DataTypes
{
	public class StockData : IData
	{
        public bool Canceled => false;

        public uint? primalKey { get => warehouse; }
        public ulong? dictionaryKey { get => nmId; }
        public DateTime lastChangeDate { get; set; }
        public DateTime date { get; set; }
        public uint? nmId;

        public string supplierArticle;
        public string techSize;
        public string barcode;
        public int quantity;
        public bool isSupply;
        public bool isRealization;
        public int quantityFull;
        public int quantityNotInOrders;
        public uint? warehouse;
        public string warehouseName;
        public int inWayToClient;
        public int inWayFromClient;
        public string subject;
        public string category;
        public int daysOnSite;
        public string SCCode;
        public int Price;
        public int Discount;

        public void Dispose()
        {

        }
    }
    
}

