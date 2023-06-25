using System;
namespace WbStarBot.Telegram
{
	internal class MessageException : Exception
	{
        public MessageException(string callbackMessage, bool nullableCallback = false) : base(callbackMessage)
        {
            this.nullableCallback = nullableCallback;
        }

        internal bool nullableCallback = false;
    }
}

