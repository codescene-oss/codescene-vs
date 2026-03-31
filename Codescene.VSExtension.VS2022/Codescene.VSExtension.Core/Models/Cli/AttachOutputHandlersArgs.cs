// Copyright (c) CodeScene. All rights reserved.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Codescene.VSExtension.Core.Tests")]

namespace Codescene.VSExtension.Core.Models.Cli
{
    internal class AttachOutputHandlersArgs
    {
        public AttachOutputHandlersArgs(
           Process process,
           StringBuilder output,
           StringBuilder error,
           TaskCompletionSource<bool> outputTcs,
           TaskCompletionSource<bool> errorTcs)
        {
            Process = process;
            Output = output;
            Error = error;
            OutputTcs = outputTcs;
            ErrorTcs = errorTcs;
        }

        public Process Process { get; set; }

        public StringBuilder Output { get; set; }

        public StringBuilder Error { get; set; }

        public TaskCompletionSource<bool> OutputTcs { get; set; }

        public TaskCompletionSource<bool> ErrorTcs { get; set; }
    }
}
