// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public abstract class ResultNotification<TResult>
    {
        public string Id { get; set; }

        public string Path { get; set; }

        public string RepoRoot { get; set; }

        public TResult Result { get; set; }
    }
}
