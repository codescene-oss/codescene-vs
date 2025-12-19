using System;

namespace Codescene.VSExtension.Core.Application.Services.ErrorHandling
{
    public class DevtoolsException : Exception
    {
        public override string Message { get; }
        public int Status { get; }
        public string TraceId { get; set; }

        public DevtoolsException(string message, int status, string traceId)
            : base(message)
        {
            this.Message = message;
            this.Status = status;
            this.TraceId = traceId;
        }
    }
}
