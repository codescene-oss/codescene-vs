// Copyright (c) CodeScene. All rights reserved.

using System;

namespace Codescene.VSExtension.Core.Application.Cli.Rpc
{
    public class ReviewFailedEventArgs : EventArgs
    {
        public ReviewFailedEventArgs(string absolutePath, string message, string id)
        {
            AbsolutePath = absolutePath;
            Message = message;
            Id = id;
        }

        public string AbsolutePath { get; }

        public string Message { get; }

        public string Id { get; }
    }
}
