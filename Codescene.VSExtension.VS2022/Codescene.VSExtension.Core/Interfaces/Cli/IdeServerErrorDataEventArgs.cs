// Copyright (c) CodeScene. All rights reserved.

using System;

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public class IdeServerErrorDataEventArgs : EventArgs
    {
        public IdeServerErrorDataEventArgs(string data)
        {
            Data = data;
        }

        public string Data { get; }
    }
}
