using System;

namespace Codescene.VSExtension.Core.Exceptions
{
    public class MissingAuthTokenException : Exception
    {
        private readonly string _message;

        public MissingAuthTokenException(string message) : base(message)
        {
            _message = message;
        }
    }
}
