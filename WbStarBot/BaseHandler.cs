using System;
using WbStarBot.Wildberries;
using Newtonsoft.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;
using WbStarBot.Cloud;

namespace WbStarBot
{
    public class BaseHandler
    {
        protected Dictionary<long, Client> clients => Base.clients;
        protected Dictionary<string, ClientData> clientsDatas => Base.clientsData;
        protected DebugStream debugStream => Base.debugStream;
    }

    public static class Base
    {
        public static Dictionary<long, Client> clients = new Dictionary<long, Client>();
        public static Dictionary<string, ClientData> clientsData = new Dictionary<string, ClientData>();
        public static DebugStream debugStream = new DebugStream();

        public static async Task LoadContentAsync()
        {
            try
            {
                if (File.Exists(OutputHandler.clientsDataFile))
                {
                    Console.WriteLine("# Content loading...");

                    clientsData = JsonConvert.DeserializeObject<Dictionary<string, ClientData>>(File.ReadAllText(OutputHandler.clientsDataFile)) ?? new Dictionary<string, ClientData>();

                    if (File.Exists(OutputHandler.clientsFile))
                        clients = JsonConvert.DeserializeObject<Dictionary<long, Client>>(File.ReadAllText(OutputHandler.clientsFile)) ?? new Dictionary<long, Client>();

                    Task[] tasks = new Task[clientsData.Count];

                    for (int i = 0; i < tasks.Length; i++)
                    {
                        ClientData data = clientsData.ElementAt(i).Value;
                        tasks[i] = Task.Run(async () =>
                        {
                            string path = $"{OutputHandler.productsDir}/{data.apiKey}.json";
                            if (File.Exists(path))
                            {
                                DataHandler? Loaded = JsonConvert.DeserializeObject<DataHandler>(File.ReadAllText(path));
                                if (Loaded != null)
                                {
                                    Loaded.data = data;
                                    data.dataHandler = Loaded;
                                }
                            }
                            else
                            {
                                try
                                {
                                    await WildberriesHandler.ClientDataUpdate(data);
                                }catch(Exception ex)
                                {
                                    debugStream.Input(ex);
                                }
                            }
                            Console.WriteLine($"Client {data.Name} has loaded.");
                        });
                    }
                    if (tasks.Length > 0)
                        await Task.WhenAll(tasks);
                    debugStream.Input($"Content has loaded successfully.\n1)\tLoaded clients:  {clients.Count},\n2)\tLoaded api keys: {clientsData.Count}.", MessageType.system);
                }
            }
            catch (Exception e)
            {
                debugStream.Input(e);
            }
        }

        public static void UploadDataHandler()
        {
            while (true)
            {
                Thread.Sleep(3600000);
                UploadData();
            }
        }

        public static void UploadData()
        {
            try
            {
                new AmazonHandler().UploadData();
                debugStream.Input($"Amazon S3 has uploaded!", MessageType.system);
                GC.Collect();
                debugStream.Input($"GC collected! Total memory used: {GC.GetTotalMemory(false)} bytes", MessageType.system);
            }
            catch (Exception e)
            {
                debugStream.Input(e);
            }
        }
    }
}

