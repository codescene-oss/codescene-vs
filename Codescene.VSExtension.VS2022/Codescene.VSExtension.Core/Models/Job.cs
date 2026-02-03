using Codescene.VSExtension.Core.Models.WebComponent.Data;

namespace Codescene.VSExtension.Core.Models
{
    public class Job
    {
        public string Type { get; set; } // See: WebComponentConstants.JobTypes

        public string State { get; set; } // See: WebComponentConstants.StateTypes

        public File File { get; set; }
    }
}
