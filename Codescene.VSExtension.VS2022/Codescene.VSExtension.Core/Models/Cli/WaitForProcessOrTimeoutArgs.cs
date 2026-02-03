// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Codescene.VSExtension.Core.Tests")]

namespace Codescene.VSExtension.Core.Models.Cli
{

    public class WaitForProcessOrTimeoutArgs
    {
        public WaitForProcessOrTimeoutArgs()
        {
        }

        public Process Process { get; set; }

        public TaskCompletionSource<bool> OutputTcs { get; set; }

        public TaskCompletionSource<bool> ErrorTcs { get; set; }

        public TimeSpan Timeout { get; set; }

        public string Command { get; set; }
    }
}
