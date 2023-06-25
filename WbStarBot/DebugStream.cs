using System;
using System.IO;

namespace WbStarBot
{
    public struct DebugStream
    {
        public delegate void Output(string message, MessageType messageType);

        public Output? output = null;
        public MessageFilter filter = (MessageFilter)31;


        public DebugStream(Output output, MessageFilter filter = (MessageFilter)(31))
        {
            this.output = output;
            this.filter = filter;
        }

        public void Input(string message, MessageType messageType = MessageType.client)
        {
            if (filter.HasFlag((MessageFilter)messageType))
            {
                output?.Invoke(message, messageType);
            }

            if (messageType == MessageType.error)
            {
                OutputHandler.AppendErrors($"[{DateTime.Now}] {message}");
            }
            else if(messageType != MessageType.system)
            {
                message = $"[{messageType}] {message}";
                OutputHandler.AppendLog(message);
            }
        }

        public void Input(Exception message)
        {
            output?.Invoke(message.ToString(), MessageType.error);

            OutputHandler.AppendErrors(message.ToString());

        }
    }

    public enum MessageType : short
    {
        client = 1,
        callback = 2,
        system = 4,
        warning = 8,
        error = 16,
    }

    [Flags]
    public enum MessageFilter : short
    {
        None = 0,
        client = 1,
        callback = 2,
        system = 4,
        warning = 8,
        error = 16,
    }
}

