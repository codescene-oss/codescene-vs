// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;

namespace Codescene.VSExtension.Core.Tests
{
    public enum Casing
    {
        Kebab,
        Camel,
    }

    public enum FixtureForm
    {
        Entity,
        Wire,
    }

    public sealed class ContractBundle
    {
        public const string EnvironmentVariable = "CS_IDE_CONTRACT_BUNDLE";

        public const string DirectoryName = "cs-ide-contract";

        private static readonly string[] SupportedSchemaKeywords =
        {
            "$ref", "$schema", "additionalItems", "additionalProperties", "anyOf", "definitions", "description", "enum", "items", "oneOf", "properties", "required", "title", "type",
        };

        private static readonly Regex UpperCaseLetter = new Regex("[A-Z]", RegexOptions.Compiled);

        private readonly ConcurrentDictionary<string, JsonSchema> _schemas = new ConcurrentDictionary<string, JsonSchema>();

        private ContractBundle(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static ContractBundle Locate()
        {
            return Locate(Environment.GetEnvironmentVariable(EnvironmentVariable), AppContext.BaseDirectory);
        }

        public static ContractBundle Locate(string? environmentValue, string startDirectory)
        {
            if (!string.IsNullOrWhiteSpace(environmentValue))
            {
                return FromEnvironmentValue(environmentValue!.Trim());
            }

            return At(Path.Combine(RepositoryRoot(startDirectory), DirectoryName));
        }

        public static ContractBundle At(string directory)
        {
            if (!Directory.Exists(directory))
            {
                throw new InvalidOperationException(
                    $"The contract bundle is missing: {directory} does not exist. Run .github/cli.ps1 to fetch it, or set {EnvironmentVariable} to an unpacked bundle directory or a bundle zip.");
            }

            foreach (var required in new[] { "wire-manifest.json", Path.Combine("fixtures", "index.json") })
            {
                if (!File.Exists(Path.Combine(directory, required)))
                {
                    throw new InvalidOperationException($"{directory} is not a contract bundle: {required} is missing.");
                }
            }

            if (!Directory.Exists(Path.Combine(directory, "schemas")))
            {
                throw new InvalidOperationException($"{directory} is not a contract bundle: schemas is missing.");
            }

            return new ContractBundle(directory);
        }

        public static JToken ToKebabKeys(JToken value)
        {
            return MapKeys(value, key => UpperCaseLetter.Replace(key, match => "-" + match.Value.ToLowerInvariant()));
        }

        public static void AssertCasing(JToken value, Casing casing)
        {
            Func<string, bool> offends = casing == Casing.Camel ? key => key.Contains("-") : key => UpperCaseLetter.IsMatch(key);
            var offending = OffendingPaths(value, offends, "$");
            if (offending.Count > 0)
            {
                Assert.Fail($"Keys not in {casing.ToString().ToLowerInvariant()}-case: {string.Join(", ", offending)}");
            }
        }

        public static void AssertDeclaredFieldsMatch(JToken fixture, JToken entity, IEnumerable<string> declaredFields)
        {
            var problems = new List<string>();
            foreach (var field in declaredFields)
            {
                CompareAt(fixture, entity, field.Split('.'), "$", problems);
            }

            if (problems.Count > 0)
            {
                Assert.Fail("Declared fields differ from the fixture:\n  " + string.Join("\n  ", problems));
            }
        }

        public JArray Manifest()
        {
            return (JArray)ReadJson("wire-manifest.json");
        }

        public JObject ManifestEntry(string method)
        {
            var entry = Manifest().OfType<JObject>().FirstOrDefault(candidate => (string?)candidate["method"] == method);
            return entry ?? throw new InvalidOperationException($"The wire manifest has no entry for {method}");
        }

        public IReadOnlyList<string> FixtureVariants(string root)
        {
            var variants = FixtureRoot(root)["variants"] as JArray ?? throw new InvalidOperationException($"Fixture root \"{root}\" has no variants");
            return variants.Select(variant => variant.ToString()).ToList();
        }

        public (string SchemaFile, string Definition) FixtureSchema(string root)
        {
            var info = FixtureRoot(root);
            return (RequiredText(info, "schema"), RequiredText(info, "definition"));
        }

        public JToken Fixture(string root, string variant, FixtureForm form)
        {
            var variants = FixtureVariants(root);
            if (!variants.Contains(variant))
            {
                throw new InvalidOperationException($"Fixture root \"{root}\" has no variant \"{variant}\"; it has: {string.Join(", ", variants)}");
            }

            return ReadJson(Path.Combine("fixtures", form.ToString().ToLowerInvariant(), root, variant + ".json"));
        }

        public void Validate(string schemaFile, string definition, JToken value)
        {
            var root = Schema(schemaFile);
            if (!root.Definitions.TryGetValue(definition, out var schema))
            {
                throw new InvalidOperationException($"Schema {schemaFile} has no definition \"{definition}\"");
            }

            var errors = schema.Validate(value);
            if (errors.Count > 0)
            {
                var problems = string.Join("\n", errors.Select(error => $"  {error.Path} {error.Kind}: {error}"));
                Assert.Fail($"Value does not validate against {schemaFile}#{definition}:\n{problems}");
            }
        }

        internal static string RepositoryRoot(string startDirectory)
        {
            var directory = new DirectoryInfo(startDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ".github")) && Directory.Exists(Path.Combine(directory.FullName, "Codescene.VSExtension.VS2022")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException($"Could not find the repository root above {startDirectory}; set {EnvironmentVariable} to the contract bundle.");
        }

        internal static JToken ParseJson(string text)
        {
            using var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None };
            return JToken.ReadFrom(reader);
        }

        /// <summary>
        /// Compiles a schema document, refusing any keyword the C# validator would silently ignore.
        /// </summary>
        internal static JsonSchema Compile(JToken document, string schemaFile)
        {
            AssertSupportedKeywords(document, schemaFile, string.Empty);
            return JsonSchema.FromJsonAsync(document.ToString(Formatting.None)).GetAwaiter().GetResult();
        }

        private static string RequiredText(JObject info, string key)
        {
            return (string?)info[key] ?? throw new InvalidOperationException($"The fixture index has no \"{key}\"");
        }

        private static ContractBundle FromEnvironmentValue(string path)
        {
            if (Directory.Exists(path))
            {
                return At(path);
            }

            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"{EnvironmentVariable} {path} does not exist");
            }

            var extracted = Path.Combine(Path.GetTempPath(), DirectoryName + "-" + Guid.NewGuid().ToString("N"));
            ZipFile.ExtractToDirectory(path, extracted);
            return At(extracted);
        }

        private static JToken MapKeys(JToken value, Func<string, string> rename)
        {
            switch (value)
            {
                case JObject obj:
                    var mapped = new JObject();
                    foreach (var property in obj.Properties())
                    {
                        mapped[rename(property.Name)] = MapKeys(property.Value, rename);
                    }

                    return mapped;
                case JArray array:
                    return new JArray(array.Select(item => MapKeys(item, rename)));
                default:
                    return value.DeepClone();
            }
        }

        private static List<string> OffendingPaths(JToken value, Func<string, bool> offends, string location)
        {
            var paths = new List<string>();
            switch (value)
            {
                case JObject obj:
                    foreach (var property in obj.Properties())
                    {
                        var propertyPath = $"{location}.{property.Name}";
                        if (offends(property.Name))
                        {
                            paths.Add(propertyPath);
                        }

                        paths.AddRange(OffendingPaths(property.Value, offends, propertyPath));
                    }

                    break;
                case JArray array:
                    for (var index = 0; index < array.Count; index++)
                    {
                        paths.AddRange(OffendingPaths(array[index], offends, $"{location}[{index}]"));
                    }

                    break;
            }

            return paths;
        }

        private static string Describe(JToken? value)
        {
            return value == null ? "(absent)" : value.ToString(Formatting.None);
        }

        private static void CompareValues(JToken? expected, JToken? actual, string location, List<string> problems)
        {
            var equal = expected == null || actual == null ? expected == actual : SameValue(expected, actual);
            if (!equal)
            {
                problems.Add($"{location}: fixture has {Describe(expected)}, entity has {Describe(actual)}");
            }
        }

        private static bool SameValue(JToken expected, JToken actual)
        {
            if (IsNumber(expected) && IsNumber(actual))
            {
                return expected.Value<decimal>() == actual.Value<decimal>();
            }

            return JToken.DeepEquals(expected, actual);
        }

        private static bool IsNumber(JToken value)
        {
            return value.Type == JTokenType.Integer || value.Type == JTokenType.Float;
        }

        private static void CompareAt(JToken? expected, JToken? actual, string[] segments, string location, List<string> problems)
        {
            if (segments.Length == 0)
            {
                CompareValues(expected, actual, location, problems);
                return;
            }

            if (expected is JArray expectedItems && actual is JArray actualItems)
            {
                CompareItems(expectedItems, actualItems, segments, location, problems);
                return;
            }

            if (!(expected is JObject expectedObject) || !(actual is JObject actualObject))
            {
                CompareValues(expected, actual, location, problems);
                return;
            }

            var segment = segments[0];
            var expectedChild = expectedObject.TryGetValue(segment, out var expectedValue) ? expectedValue : null;
            var actualChild = actualObject.TryGetValue(segment, out var actualValue) ? actualValue : null;
            if (expectedChild == null && actualChild == null)
            {
                return;
            }

            CompareAt(expectedChild, actualChild, segments.Skip(1).ToArray(), $"{location}.{segment}", problems);
        }

        private static void CompareItems(JArray expected, JArray actual, string[] segments, string location, List<string> problems)
        {
            if (expected.Count != actual.Count)
            {
                problems.Add($"{location}: fixture has {expected.Count} items, entity has {actual.Count}");
                return;
            }

            for (var index = 0; index < expected.Count; index++)
            {
                CompareAt(expected[index], actual[index], segments, $"{location}[{index}]", problems);
            }
        }

        private static void AssertSupportedKeywords(JToken node, string schemaFile, string location)
        {
            if (!(node is JObject schema))
            {
                return;
            }

            foreach (var property in schema.Properties())
            {
                var propertyPath = $"{location}/{property.Name}";
                if (!SupportedSchemaKeywords.Contains(property.Name))
                {
                    throw new InvalidOperationException(
                        $"{schemaFile} uses the keyword \"{property.Name}\" at {propertyPath}, which the C# validator does not enforce; extend ContractBundle before accepting it.");
                }

                AssertSupportedKeywordsBelow(property, schemaFile, propertyPath);
            }
        }

        private static void AssertSupportedKeywordsBelow(JProperty keyword, string schemaFile, string location)
        {
            switch (keyword.Name)
            {
                case "properties":
                case "definitions":
                    foreach (var named in ((JObject)keyword.Value).Properties())
                    {
                        AssertSupportedKeywords(named.Value, schemaFile, $"{location}/{named.Name}");
                    }

                    break;
                case "oneOf":
                case "anyOf":
                    foreach (var alternative in (JArray)keyword.Value)
                    {
                        AssertSupportedKeywords(alternative, schemaFile, location);
                    }

                    break;
                case "items" when keyword.Value is JArray tuple:
                    foreach (var element in tuple)
                    {
                        AssertSupportedKeywords(element, schemaFile, location);
                    }

                    break;
                case "items":
                case "additionalItems":
                case "additionalProperties":
                    AssertSupportedKeywords(keyword.Value, schemaFile, location);
                    break;
            }
        }

        private JToken ReadJson(string relativePath)
        {
            var file = Path.Combine(Root, relativePath);
            if (!File.Exists(file))
            {
                throw new InvalidOperationException($"The contract bundle has no {relativePath} ({file})");
            }

            return ParseJson(File.ReadAllText(file, Encoding.UTF8));
        }

        private JObject FixtureRoot(string root)
        {
            var index = (JObject)ReadJson(Path.Combine("fixtures", "index.json"));
            if (!index.TryGetValue(root, out var rootInfo))
            {
                throw new InvalidOperationException($"Unknown fixture root \"{root}\"; the bundle has: {string.Join(", ", index.Properties().Select(property => property.Name))}");
            }

            return (JObject)rootInfo;
        }

        private JsonSchema Schema(string schemaFile)
        {
            return _schemas.GetOrAdd(schemaFile, LoadSchema);
        }

        private JsonSchema LoadSchema(string schemaFile)
        {
            return Compile(ReadJson(Path.Combine("schemas", schemaFile)), schemaFile);
        }
    }
}
