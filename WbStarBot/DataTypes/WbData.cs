using System;
using System.Numerics;

namespace WbStarBot.DataTypes
{
	public interface WbData
	{
        public DateTime lastUpdate { get; set; }
        public object? Update<T>(string content) where T : IData;
    }
}

