// Copyright (c) CodeScene. All rights reserved.

using System;
using Codescene.VSExtension.Core.Models;

namespace Codescene.VSExtension.Core.Application.Cli.Rpc
{
    public class FileReviewAppliedEventArgs : EventArgs
    {
        public FileReviewAppliedEventArgs(string absolutePath, FileReviewModel review, string id)
        {
            AbsolutePath = absolutePath;
            Review = review;
            Id = id;
        }

        public string AbsolutePath { get; }

        public FileReviewModel Review { get; }

        public string Id { get; }
    }
}
