using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Codescene.VSExtension.Core.Enums
{
    [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
    public enum ChangeType
    {
        Degraded,
        Fixed,
        Improved,
        Introduced
    }
}
