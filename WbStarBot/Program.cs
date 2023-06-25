using WbStarBot;
using WbStarBot.Telegram;
using WbStarBot.Cloud;
using DocumentFormat.OpenXml.ExtendedProperties;

Base.debugStream = new DebugStream(debugOutput);
OutputHandler.FileSystemInit();

await Base.LoadContentAsync();

new Thread(new ThreadStart(Base.UploadDataHandler)).Start();

#if DEBUG
string token = "5483308462:AAGUJ5o4600YoeWGPsqocRGZhWU3XkMPSIc";
#else
string token = "5719447713:AAF-7w3jQQnvs2v9ZjzJ-5nEL61fzYD0n8M";
#endif

Bot bot = new Bot(token);

while (true)
{
    string[]? cmd = Console.ReadLine()?.Split();

    if(cmd is not null && cmd[0].Length > 0)
    {
        try
        {
            switch (cmd[0])
            {
                case "echo":
                    if (cmd.Length > 1)
                    {
                        int fl = int.Parse(cmd[1]);
                        Base.debugStream.filter ^= (MessageFilter)(1 << fl);
                        if(((int)Base.debugStream.filter & (1<<fl)) != 0)
                        {
                            debugOutput($"{(MessageType)(1 << fl)} messages has turned On");
                        }
                        else
                        {
                            debugOutput($"{(MessageType)(1 << fl)} messages has turned Off");
                        }
                    }
                    else
                    {
                        debugOutput(Base.debugStream.filter.ToString());
                    }
                    break;
                case "clear":
                    Console.Clear();
                    break;
                case "save":
                    OutputHandler.SaveClients();
                    OutputHandler.SaveClientsData();
                    OutputHandler.SaveProductsData();
                    Console.WriteLine("Saved!");
                    break;
                case "upload":
                    new AmazonHandler().UploadData();
                    Console.WriteLine("Uploaded!");
                    break;
                case "memory":
                    Console.WriteLine($"Total memory used: {GC.GetTotalMemory(false)} bytes");
                    break;
                case "collect":
                    Base.UploadData();
                    break;
                case "pay":
                    if (cmd.Length > 1)
                    {
                        uint payCount = uint.Parse(cmd[1]);
                        Bot.payCount = payCount;
                    }
                    else
                    {
                        debugOutput(Bot.payCount.ToString());
                    }
                    break;
#if DEBUG
                case "rmdata":
                    Directory.Delete(OutputHandler.dataDir, true);
                    break;
#endif
                case "end":
                    //TODO:
                    OutputHandler.SaveClients();
                    OutputHandler.SaveClientsData();
                    OutputHandler.SaveProductsData();
                    OutputHandler.PushSettings(Bot.payCount);

                    Console.WriteLine("Data saved!");

                    new AmazonHandler().UploadData();
                    Console.WriteLine("S3 uploaded!");

                    bot.Dispose();
                    Environment.Exit(0);
                    return;
                case "authError":
                    foreach (ClientData c in Base.clientsData.Values.Where((c) => c.Unauth))
                        Console.WriteLine(c.Name);
                    break;
                case "authRemove":
                    foreach (string c in Base.clientsData.Where((c) => c.Value.Unauth).Select(c => c.Key))
                    {
                        Base.clientsData.Remove(c);

                        foreach(Client client in Base.clients.Values.Where(a => a.apiKeys.Contains(c)))
                        {
                            client.apiKeys.Remove(c);
                        }
                    }
                    
                    break;
                default:
                    debugOutput("Unknown command.");
                    break;
            }
        }
        catch (Exception e)
        {
            debugOutput("Incorrect command format.");
        }
    }
}

void debugOutput(string message, MessageType type = MessageType.warning)
{
    switch (type)
    {
        case MessageType.system:
            Console.ForegroundColor = ConsoleColor.Cyan;
            message = $"[{DateTime.Now.ToString("MM:dd HH:mm:ss")}] {message}";
            break;
        case MessageType.warning:
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            break;
        case MessageType.error:
            Console.ForegroundColor = ConsoleColor.Red;
            break;
        default:
            Console.ForegroundColor = ConsoleColor.Gray;
            break;
    }

    Console.WriteLine(message);

    Console.ResetColor();
}