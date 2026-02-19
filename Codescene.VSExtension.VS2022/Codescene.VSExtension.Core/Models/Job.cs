// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.WebComponent.Data;

namespace Codescene.VSExtension.Core.Models
{
    // Represents a pending job for informational/UX purposes (e.g., displaying running operations to the user)
    public class Job
    {
        public string Type { get; set; } // See: WebComponentConstants.JobTypes

        public string State { get; set; } // See: WebComponentConstants.StateTypes

        public File File { get; set; }
    }
}
