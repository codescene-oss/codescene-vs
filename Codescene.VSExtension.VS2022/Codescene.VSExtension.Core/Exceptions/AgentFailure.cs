// Copyright (c) CodeScene. All rights reserved.

using System;

namespace Codescene.VSExtension.Core.Exceptions
{
    public enum AgentFailureKind
    {
        Auth,
        Generic,
        Cancelled,
    }

    public class AgentFailure : Exception
    {
        public AgentFailure(AgentFailureKind kind, string message)
            : base(message)
        {
            this.Kind = kind;
        }

        public AgentFailureKind Kind { get; }
    }
}
