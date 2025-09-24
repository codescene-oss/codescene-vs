using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Models.Cli
{
    public class PositionModel
    {
        [JsonProperty("line")]
        public int Line { get; set; }

        [JsonProperty("character")]
        public int Character { get; set; }
    }
}
