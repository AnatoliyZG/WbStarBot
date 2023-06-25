using System;
using System.IO;
using ClosedXML.Excel;
using Newtonsoft.Json;
using WbStarBot.Cloud;

namespace WbStarBot
{
    internal static class OutputHandler
    {

        public static string logfile => $"{logsDir}/log.txt";
        public static string errorsfile => $"{logsDir}/errors.txt";
        public static string settings => $"{logsDir}/settings.txt";

        public static string clientsFile => $"{dataDir}/clients.json";
        public static string clientsDataFile => $"{dataDir}/clientsData.json";

        private static string currentDir => Directory.GetCurrentDirectory();

        public static string logsDir => $"{dataDir}/logs";
        public static string dataDir => $"{currentDir}/data";
        public static string productImageDir => $"{currentDir}/productsImages";
        public static string productsDir => $"{dataDir}/productsDatas";

        public static string feeFile = $"{currentDir}/fees.xlsx";

        public static void AppendLog(string text) => AppendFile(logfile, text);
        public static void AppendErrors(string text) => AppendFile(errorsfile, text);

        public static void AppendFile(string path, string text)
        {
            using (StreamWriter sw = File.AppendText(path))
            {
                sw.WriteLine(text);
            }
        }

        public static void FileSystemInit()
        {
            checkDirectory(dataDir);
            checkDirectory(logsDir);
            checkDirectory(productImageDir);
            checkDirectory(productsDir);

            if (File.Exists(feeFile))
            {
                Console.WriteLine("Fees file loaded!");
            }
        }

        private static void checkDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public static void SaveClients()
        {
            string content = JsonConvert.SerializeObject(Base.clients.Where(a => a.Value.Length > 0).ToDictionary(a => a.Key, a => a.Value));
            File.WriteAllText(clientsFile, content);
        }

        public static void SaveClientsData()
        {
            try
            {
                string content = JsonConvert.SerializeObject(Base.clientsData);
                File.WriteAllText(clientsDataFile, content);
            }
            catch (Exception e)
            {
                Base.debugStream.Input(e);
            }
        }

        public static void SaveProductsData()
        {
            try
            {
                foreach (ClientData data in Base.clientsData.Values)
                {
                    string content = JsonConvert.SerializeObject(data.dataHandler);
                    File.WriteAllText($"{productsDir}/{data.apiKey}.json", content);
                }
            }
            catch (Exception e)
            {
                Base.debugStream.Input(e);
            }
        }

        public static void PushSettings(uint payCount)
        {
            File.WriteAllText(settings, payCount.ToString());
        }

        public static uint PopSettings()
        {
            if (File.Exists(settings))
                return uint.Parse(File.ReadAllText(settings));
            return 0;
        }

        public static short? GetFee(string? category, string? products)
        {
            if (category == null || products == null || !File.Exists(feeFile))
                return null;

            using (var feeBoock = new XLWorkbook(feeFile))
            {
                IXLCells c = feeBoock.Worksheet(1).Column(1).CellsUsed(x => x.Value.ToString() == category);
                foreach (IXLCell x in c)
                {
                    string pr = feeBoock.Worksheet(1).Column(2).Cell(x.Address.RowNumber).Value.GetText();
                    if (pr == products)
                    {
                        return short.Parse(feeBoock.Worksheet(1).Column(3).Cell(x.Address.RowNumber).Value.ToString());
                    }
                }
            }

            return null;
        }
    }
}

