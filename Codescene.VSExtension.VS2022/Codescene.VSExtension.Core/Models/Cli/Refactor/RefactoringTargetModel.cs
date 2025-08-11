﻿using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Refactor
{
    public class RefactoringTargetModel
    {
        [JsonProperty("category")]
        public string Category { get; set; }

        /// <summary>
        /// Start line for the code smell.
        /// </summary>
        [JsonProperty("line")]
        public int Line { get; set; }
    }
}
