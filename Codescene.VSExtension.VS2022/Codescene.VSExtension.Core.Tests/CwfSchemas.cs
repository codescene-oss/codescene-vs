// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Concurrent;
using System.Text;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Validation;

namespace Codescene.VSExtension.Core.Tests
{
    /// <summary>
    /// The two schemas of the bundled webview (CWF) release, unpacked to <c>cs-cwf/</c> by <c>.github/cwf.ps1</c>:
    /// <c>webview-schema.json</c> (what the extension sends to the webview, a union of the views) and
    /// <c>message-schema.json</c> (what the webview sends to the extension, a union of the message types).
    /// Both are closed, so a field the webview does not declare fails. A missing file is a failure, never a skip.
    /// </summary>
    public sealed class CwfSchemas
    {
        public const string DirectoryName = "cs-cwf";

        public const string WebviewSchemaFile = "webview-schema.json";

        public const string MessageSchemaFile = "message-schema.json";

        private const int ReportedErrors = 12;

        private readonly ConcurrentDictionary<string, JsonSchema> _schemas = new ConcurrentDictionary<string, JsonSchema>();

        private CwfSchemas(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static CwfSchemas Locate()
        {
            return At(Path.Combine(ContractBundle.RepositoryRoot(AppContext.BaseDirectory), DirectoryName));
        }

        public static CwfSchemas At(string directory)
        {
            return new CwfSchemas(directory);
        }

        /// <summary>
        /// Mirrors what the webview does with what it receives (<c>parseIncomingDataFormat</c> in cs-webview): the delta
        /// of every file and the auth state may arrive in camelCase and are converted to kebab-case on mount, so the
        /// schema describes them in kebab-case. Everything else is validated as sent.
        /// </summary>
        public static JToken AsTheWebviewReadsIt(JToken payload)
        {
            var result = payload.DeepClone();
            if (!(result is JObject root))
            {
                return result;
            }

            if (root["view"]?.ToString() == "home" && root["data"]?["fileDeltaData"] is JArray files)
            {
                foreach (var file in files.OfType<JObject>())
                {
                    if (file.TryGetValue("delta", out var delta))
                    {
                        file["delta"] = ContractBundle.ToKebabKeys(delta);
                    }
                }
            }

            if (root.TryGetValue("auth", out var auth))
            {
                root["auth"] = ContractBundle.ToKebabKeys(auth);
            }

            return root;
        }

        public void ValidatePayload(JToken value)
        {
            ValidateAgainstOneOfTheBranches(WebviewSchemaFile, value);
        }

        public void ValidateMessage(JToken value)
        {
            ValidateAgainstOneOfTheBranches(MessageSchemaFile, value);
        }

        public IReadOnlyCollection<string> DeclaredProperties(string definition)
        {
            var root = Schema(WebviewSchemaFile);
            if (!root.Definitions.TryGetValue(definition, out var schema))
            {
                throw new InvalidOperationException($"{WebviewSchemaFile} has no definition \"{definition}\"");
            }

            return schema.ActualSchema.Properties.Keys.ToList();
        }

        private static JToken ConstAsEnum(JToken node)
        {
            switch (node)
            {
                case JObject obj:
                    var converted = new JObject();
                    foreach (var property in obj.Properties())
                    {
                        if (property.Name == "const")
                        {
                            converted["enum"] = new JArray(property.Value.DeepClone());
                        }
                        else
                        {
                            converted[property.Name] = ConstAsEnum(property.Value);
                        }
                    }

                    return converted;
                case JArray array:
                    return new JArray(array.Select(ConstAsEnum));
                default:
                    return node.DeepClone();
            }
        }

        private static IEnumerable<ValidationError> Leaves(IEnumerable<ValidationError> errors)
        {
            foreach (var error in errors)
            {
                if (error is ChildSchemaValidationError parent)
                {
                    foreach (var leaf in Leaves(parent.Errors.Values.SelectMany(children => children)))
                    {
                        yield return leaf;
                    }
                }
                else
                {
                    yield return error;
                }
            }
        }

        private static string Describe(ValidationError error)
        {
            var property = string.IsNullOrEmpty(error.Property) ? string.Empty : $" \"{error.Property}\"";
            return $"  {error.Path} {error.Kind}{property}";
        }

        /// <summary>
        /// A branch is the one the value was meant for when it declares the value's discriminator (view or messageType).
        /// </summary>
        private static bool DeclaresDiscriminatorOf(JsonSchema branch, JToken value)
        {
            foreach (var discriminator in new[] { "view", "messageType" })
            {
                var actual = (value as JObject)?[discriminator]?.ToString();
                if (actual != null && branch.ActualSchema.Properties.TryGetValue(discriminator, out var declared))
                {
                    return declared.ActualSchema.Enumeration.Any(candidate => candidate?.ToString() == actual);
                }
            }

            return false;
        }

        private void ValidateAgainstOneOfTheBranches(string schemaFile, JToken value)
        {
            var branches = Schema(schemaFile).AnyOf;
            if (branches.Count == 0)
            {
                throw new InvalidOperationException($"{schemaFile} has no anyOf branches to validate against");
            }

            var failures = new List<(JsonSchema Branch, List<ValidationError> Errors)>();
            foreach (var branch in branches)
            {
                var errors = branch.Validate(value);
                if (errors.Count == 0)
                {
                    return;
                }

                failures.Add((branch, Leaves(errors).ToList()));
            }

            var intended = failures.Where(failure => DeclaresDiscriminatorOf(failure.Branch, value)).ToList();
            var closest = (intended.Count > 0 ? intended : failures).OrderBy(failure => failure.Errors.Count).First();
            var problems = string.Join("\n", closest.Errors.Take(ReportedErrors).Select(Describe));
            Assert.Fail($"Value does not validate against {schemaFile} (the branch it matches by view or messageType reports):\n{problems}");
        }

        private JsonSchema Schema(string schemaFile)
        {
            return _schemas.GetOrAdd(schemaFile, Load);
        }

        private JsonSchema Load(string schemaFile)
        {
            var file = Path.Combine(Root, schemaFile);
            if (!File.Exists(file))
            {
                throw new InvalidOperationException(
                    $"The webview schema {schemaFile} is missing: {file} does not exist. Run .github/cwf.ps1 (or make updatecwf) to fetch the CWF release pinned in cwf.version.");
            }

            var document = ContractBundle.ParseJson(File.ReadAllText(file, Encoding.UTF8));
            return ContractBundle.Compile(ConstAsEnum(document), schemaFile);
        }
    }
}
