// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Models.Agent
{
    public class ConnectivityResult
    {
        public bool Success { get; set; }

        public bool Cancelled { get; set; }

        public string Error { get; set; }
    }
}
