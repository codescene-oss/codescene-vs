// Copyright (c) CodeScene. All rights reserved.

using System;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Exceptions
{
    public class DevtoolsException : Exception
    {
        [JsonConstructor]
        public DevtoolsException(string message, int status, string traceId)
            : base(message)
        {
            this.Message = message;
            this.Status = status;
            this.TraceId = traceId;
        }

        public override string Message { get; }

        public int Status { get; }

        public string TraceId { get; set; }
    }
}
