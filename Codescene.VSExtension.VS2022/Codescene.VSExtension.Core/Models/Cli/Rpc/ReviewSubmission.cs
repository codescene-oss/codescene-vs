// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class ReviewSubmission
    {
        public ReviewDocument Document { get; set; }

        public string RelPath { get; set; }

        public string Content { get; set; }

        public bool UpdateDiagnosticsPane { get; set; }

        public bool UpdateMonitor { get; set; }
    }
}
