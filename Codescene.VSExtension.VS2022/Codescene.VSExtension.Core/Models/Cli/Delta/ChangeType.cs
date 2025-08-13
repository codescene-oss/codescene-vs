using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Delta
{
    [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum ChangeType
    {
        Degraded,
        Fixed,
        Improved,
        Introduced
    }
}
