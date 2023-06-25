using System;
using System.Threading;
using System.Threading.Tasks;

namespace WbStarBot.Telegram
{
    public partial class Bot : BaseHandler
    {
        public void StartDailyTheard()
        {
            new Thread(new ThreadStart(DailyTheard)).Start();
        }

        public void DailyTheard()
        {
            int delay = (int)DateTime.Now.AddDays(1).Date.Subtract(DateTime.Now).TotalSeconds;
            debugStream.Input($"Next daily update in {delay} seconds", MessageType.warning);

            while (true)
            {
                Thread.Sleep(delay * 1000);

                OutputHandler.SaveClients();
                OutputHandler.SaveProductsData();

                delay = 86400;
            }
        }
    }
}

