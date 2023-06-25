using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace WbStarBot
{
    public delegate Task<string?> MessageCallback(ClientLink client, string message);

    public class Client : BaseHandler
	{
		public ClientData this[int index] => clientDatas[index];

		public string? userName = null;
		public uint paysCount = 0;

		[JsonIgnore]
		public ClientData[] clientDatas => apiKeys.Select(x => clientsDatas[x]).ToArray();

        [JsonIgnore]
        public int Length => clientDatas.Length;

        [JsonIgnore]
        public MessageCallback? messageCallback;

		[JsonProperty]
        public List<string> apiKeys = new List<string>();

		public List<(string product, int messageId)> trackAds = new List<(string product, int messageId)>();

        public Client(string? userName, params string[] apiKeys)
		{
			this.userName = userName;
			AddApis(apiKeys);
		}

        public void AddApis(params string[] apiKeys)
        {
			this.apiKeys.AddRange(apiKeys);
        }

		public int getClientDataId(ClientData data)
		{
			for(int i = 0; i < clientDatas.Length; i ++)
			{
				if (clientDatas[i] == data)
					return i;
			}
			return -1;
		}
    }

	public struct ClientLink
	{
		public required long clientId;

		public readonly Client? client
		{
			get => Base.clients.ContainsKey(clientId) ? Base.clients[clientId] : null;
		}

		[SetsRequiredMembers]
		public ClientLink(long clientId) 
		{
			this.clientId = clientId;
		}

        public static implicit operator Client?(ClientLink client) => client.client;
        public static implicit operator long(ClientLink client) => client.clientId;

        public static implicit operator ClientLink(long cliendId) => new ClientLink(cliendId);
    }
}

