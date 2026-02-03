// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Delta
{
    public class FunctionInfoModel
    {
        /// <summary>
        /// Gets or sets name of function.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets full range of the function.
        /// </summary>
        [JsonProperty("range")]
        public CliRangeModel Range { get; set; }
    }
}
