using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Enums
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
