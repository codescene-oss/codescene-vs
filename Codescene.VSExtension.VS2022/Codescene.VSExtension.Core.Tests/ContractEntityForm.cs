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
                    return ObjectWithoutNullsAbsentFrom(obj, fixture as JObject);
                case JArray array:
                    return ArrayWithoutNullsAbsentFrom(array, fixture as JArray);
                default:
                    return entity.DeepClone();
            }
        }

        private static JObject ObjectWithoutNullsAbsentFrom(JObject entity, JObject? fixture)
        {
            var kept = new JObject();
            foreach (var property in entity.Properties())
            {
                JToken? fixtureChild = null;
                fixture?.TryGetValue(property.Name, out fixtureChild);
                if (property.Value.Type == JTokenType.Null && fixtureChild == null)
                {
                    continue;
                }

                kept[property.Name] = WithoutNullsAbsentFrom(property.Value, fixtureChild);
            }

            return kept;
        }

        private static JArray ArrayWithoutNullsAbsentFrom(JArray entity, JArray? fixture)
        {
            return new JArray(entity.Select((item, index) =>
                WithoutNullsAbsentFrom(item, fixture != null && index < fixture.Count ? fixture[index] : null)));
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
