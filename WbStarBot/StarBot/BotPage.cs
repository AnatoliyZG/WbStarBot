using System;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;

namespace WbStarBot.Telegram
{
	public struct BotPage
	{
        public required string? text = "null";
		public IReplyMarkup? markup;

		public actionProp properties = actionProp.edit;

		[SetsRequiredMembers]
        public BotPage(string? text, IReplyMarkup? markup = null)
		{
			this.text = text;
			this.markup = markup;
		}
		public ParseMode parseMode = ParseMode.Markdown;

		public int? replyMessage = null;

		public enum actionProp
		{
			edit = 0,
			delete = 1,
			answer_message = 2,
			answer_message_alert = 3,
			answer_with_back = 4,
			answer_with_delete = 5,
			reply = 6,
		}

		public static implicit operator BotPage((string?, IReplyMarkup?) page) => new BotPage(page.Item1, page.Item2);
	}

	public struct PageQuery
	{
		public required string page { get; init; }
		public string[]? query = null;

		public string callback => query != null ? string.Join(' ', page, string.Join(' ', query)) : page;

		public string? this[int index]{
			get
			{
				if (query == null || index >= query.Length)
					return null;

				return query[index];
			}
		}

		[SetsRequiredMembers]
		public PageQuery(string page, params string[]? query)
		{
			this.page = page;
			this.query = query;
		}

		[SetsRequiredMembers]
		public PageQuery(string callback)
		{
			string[] args = callback.Split();

			page = args[0];
			if (args.Length > 1)
			{
				query = new string[args.Length-1];
				for (int i = 1; i < args.Length; i++)
				{
					query![i-1] = args[i];
                }
			}
		}

		public string ReplyCallback(string query) => $"{callback} {query}";

		public string ChangeCallback(string query) => $"{previousCallback} {query}";

		public (string text, string callback) BackButton =>("« Назад", previousCallback);

		private string previousCallback
		{
			get
			{
				string q = page;

				for(int i = 0; i < query?.Length-1; i++)
				{
					q += " " + query![i];
				}

				return q;
			}
		}

		public static implicit operator PageQuery(string s) => new PageQuery(s);

    }
}

