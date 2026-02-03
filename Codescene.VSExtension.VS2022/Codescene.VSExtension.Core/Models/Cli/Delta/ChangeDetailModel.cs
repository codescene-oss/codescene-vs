using Codescene.VSExtension.Core.Enums;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Delta
{
    public class ChangeDetailModel
    {
        /// <summary>
        /// Gets or sets code smell category, for example Complex Method
        /// </summary>
        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("change-type")]
        public ChangeType ChangeType { get; set; }

        /// <summary>
        /// Gets or sets detailed description about what caused the code health to go down.
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets line number of this change. 1-indexed. Note that for 'fixed' changes, the line only indicates where the issue was before the change.
        /// </summary>
        [JsonProperty("line")]
        public int? Line { get; set; }
    }
}
