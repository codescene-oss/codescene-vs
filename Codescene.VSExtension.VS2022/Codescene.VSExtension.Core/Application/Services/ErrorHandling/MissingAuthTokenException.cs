using System;

namespace Codescene.VSExtension.Core.Application.Services.ErrorHandling
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
