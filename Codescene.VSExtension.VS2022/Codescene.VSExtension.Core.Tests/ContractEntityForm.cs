// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    public static class ContractEntityForm
    {
        public static IReadOnlyList<string> FieldPaths(params JToken[] values)
        {
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                Collect(value, string.Empty, paths);
            }

            return paths.ToList();
        }

        public static JToken WithoutNullsAbsentFrom(JToken entity, JToken? fixture)
        {
            switch (entity)
            {
                case JObject obj:
                    var kept = new JObject();
                    var fixtureObject = fixture as JObject;
                    foreach (var property in obj.Properties())
                    {
                        JToken? fixtureChild = null;
                        fixtureObject?.TryGetValue(property.Name, out fixtureChild);
                        if (property.Value.Type == JTokenType.Null && fixtureChild == null)
                        {
                            continue;
                        }

                        kept[property.Name] = WithoutNullsAbsentFrom(property.Value, fixtureChild);
                    }

                    return kept;
                case JArray array:
                    var fixtureItems = fixture as JArray;
                    return new JArray(array.Select((item, index) =>
                        WithoutNullsAbsentFrom(item, fixtureItems != null && index < fixtureItems.Count ? fixtureItems[index] : null)));
                default:
                    return entity.DeepClone();
            }
        }

        private static void Collect(JToken value, string prefix, ISet<string> paths)
        {
            switch (value)
            {
                case JObject obj when obj.Count > 0:
                    foreach (var property in obj.Properties())
                    {
                        Collect(property.Value, prefix.Length == 0 ? property.Name : prefix + "." + property.Name, paths);
                    }

                    break;
                case JArray array when array.Any(item => item is JObject):
                    foreach (var item in array.OfType<JObject>())
                    {
                        Collect(item, prefix, paths);
                    }

                    break;
                default:
                    if (prefix.Length > 0)
                    {
                        paths.Add(prefix);
                    }

                    break;
            }
        }
    }
}
