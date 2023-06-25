using System;
using System.Net;

namespace WbStarBot.Wildberries
{
    public class WildberriesException : Exception
    {
        public WildberriesException(ExceptionType exception) : base(exception.ToString()) { exceptionType = exception; }

        public WildberriesException(WebException exception): base(exception.ToString())
        {
            switch (exception.Message)
            {
                case "The remote server returned an error: (400) Bad Request.":
                    exceptionType = ExceptionType.data_bad_request;
                    break;
                case "The remote server returned an error: (429) Too Many Requests.":
                    exceptionType = ExceptionType.data_too_many_request;
                    break;
                case "Unauthorized":
                    exceptionType = ExceptionType.Unauthorized;
                    break;
                default:
                    exceptionType = ExceptionType.unk_exception;
                    break;
            }
        }

        public ExceptionType exceptionType;

        public enum ExceptionType : uint
        {
            data_bad_request = 0,
            data_too_many_request = 1,
            unk_exception = 2,
            Unauthorized = 3,

        }
    }
}

