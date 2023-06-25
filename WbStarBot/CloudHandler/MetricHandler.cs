using System;
using Yandex.Metrica;

namespace WbStarBot.Cloud
{
	public static class MetricHandler
	{
		private const string metricApi = "94029aea-a6a2-43f2-8ab6-d07dec6711e2";

		public static void Init()
		{
			YandexMetrica.Activate(metricApi);
		}

		private static void SendEvent(EventType type, Dictionary<string, string> args)
		{
			YandexMetrica.ReportEvent<Dictionary<string, string>>(type.ToString(), args);
		}


		public enum EventType
		{
			UserCreated,
			PromocodeUsed,
		}
	}
}

