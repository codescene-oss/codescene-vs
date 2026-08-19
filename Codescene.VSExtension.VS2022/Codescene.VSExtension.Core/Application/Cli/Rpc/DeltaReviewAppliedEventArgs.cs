// Copyright (c) CodeScene. All rights reserved.

using System;
using Codescene.VSExtension.Core.Models.Cli.Delta;

namespace Codescene.VSExtension.Core.Application.Cli.Rpc
{
    public class DeltaReviewAppliedEventArgs : EventArgs
    {
        public DeltaReviewAppliedEventArgs(string absolutePath, DeltaResponseModel delta, string id)
        {
            AbsolutePath = absolutePath;
            Delta = delta;
            Id = id;
        }

        public string AbsolutePath { get; }

        public DeltaResponseModel Delta { get; }

        public string Id { get; }
    }
}
