using System;
using Newtonsoft.Json;

namespace WbStarBot.DataTypes
{
	public class SaleData : IData
    {
        [JsonIgnore]
        public bool Canceled => false;

        public DateTime lastChangeDate { get; set; }
        public DateTime date { get; set; }

        public uint? primalKey => nmId;
        [JsonIgnore]
        public ulong? dictionaryKey { get => odid; }
        public uint? nmId;
        public ulong? odid;
        public string saleID;
        public decimal? forPay;

        public short? spp;
        public string? category;
        public string? subject;
        public string? brand;
        public string? techSize;
        public string? supplierArticle;

        public void Dispose()
        {
            nmId = null;
            odid = null;
            forPay = null;
            spp = null;
            category = null;
            subject = null;
            brand = null;
            techSize = null;
            supplierArticle = null;
        }

        [JsonIgnore]
        public string? Brand => brand?.Replace("_", "").Replace("*", "").Replace("`", "");

        [JsonIgnore]
        public bool isCancel => saleID[0] == 'R' || saleID[0] == 'B';

        /*
		 * "date": "2022-03-04T00:00:00",
"lastChangeDate": "2022-03-06T10:11:07",
"supplierArticle": "12345",
"techSize": "",
"barcode": "123453559000",
"totalPrice": 0,
"discountPercent": 0,
"isSupply": false,
"isRealization": true,
"promoCodeDiscount": 0,
"warehouseName": "Подольск",
"countryName": "Россия",
"oblastOkrugName": "Центральный федеральный округ",
"regionName": "Московская",
"incomeID": 0,
"saleID": "D99937000247",
"odid": 456739003897,
"spp": 0,
"forPay": 0,
"finishedPrice": 0,
"priceWithDisc": 0,
"nmId": 1234567,
"subject": "Мультистайлеры",
"category": "Бытовая техника",
"brand": "Тест",
"IsStorno": 0,
"gNumber": "34343462218572569531",
"sticker": ""
		*/
    }
}

