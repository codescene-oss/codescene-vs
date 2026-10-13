// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class ServerStartEvent
    {
        public ServerMetadata Metadata { get; set; }

        public bool Restart { get; set; }
    }
}
