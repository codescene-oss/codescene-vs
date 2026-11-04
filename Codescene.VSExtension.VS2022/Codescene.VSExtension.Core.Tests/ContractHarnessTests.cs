// Copyright (c) CodeScene. All rights reserved.

using System.IO.Compression;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class ContractHarnessTests
    {
        private static readonly string[] DeclaredReviewFields =
        {
            "score",
            "raw-score",
            "file-level-code-smells",
            "function-level-code-smells.function",
            "function-level-code-smells.range.start-line",
            "function-level-code-smells.code-smells.category",
            "code-health-rules-error",
        };

        private static ContractBundle _bundle = null!;

        private readonly List<string> _tempDirectories = new List<string>();

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _bundle = ContractBundle.Locate();
        }

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var directory in _tempDirectories.Where(Directory.Exists))
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        [DataRow("minimal")]
        [DataRow("full")]
        public void Validate_WithEntityReviewFixture_Passes(string variant)
        {
            var entity = _bundle.Fixture("review", variant, FixtureForm.Entity);

            _bundle.Validate("review.json", "Review", entity);
        }

        [TestMethod]
        public void Validate_WithInjectedUnknownField_Fails()
        {
            var entity = (JObject)_bundle.Fixture("review", "full", FixtureForm.Entity);
            entity["injected-field"] = "not in the contract";

            var failure = Assert.Throws<AssertFailedException>(() => _bundle.Validate("review.json", "Review", entity));

            StringAssert.Contains(failure.Message, "injected-field");
        }

        [TestMethod]
        public void Validate_WithInjectedUnknownFieldBelowTheRoot_Fails()
        {
            var entity = _bundle.Fixture("review", "full", FixtureForm.Entity);
            ((JObject)Token(entity, "function-level-code-smells[0].range"))["injected-field"] = 1;

            var failure = Assert.Throws<AssertFailedException>(() => _bundle.Validate("review.json", "Review", entity));

            StringAssert.Contains(failure.Message, "injected-field");
        }

        [TestMethod]
        public void Validate_WithTheWireCasedFixture_Fails()
        {
            var wire = _bundle.Fixture("review", "full", FixtureForm.Wire);

            Assert.Throws<AssertFailedException>(() => _bundle.Validate("review.json", "Review", wire));
        }

        [TestMethod]
        public void Validate_WithAMissingRequiredField_Fails()
        {
            var entity = (JObject)_bundle.Fixture("review", "minimal", FixtureForm.Entity);
            entity.Remove("raw-score");

            var failure = Assert.Throws<AssertFailedException>(() => _bundle.Validate("review.json", "Review", entity));

            StringAssert.Contains(failure.Message, "raw-score");
        }

        [TestMethod]
        public void Validate_WithAWrongType_Fails()
        {
            var entity = (JObject)_bundle.Fixture("review", "full", FixtureForm.Entity);
            entity["score"] = "high";

            Assert.Throws<AssertFailedException>(() => _bundle.Validate("review.json", "Review", entity));
        }

        [TestMethod]
        public void Validate_WithATupleSchema_RejectsAnExtraElement()
        {
            var entity = (JArray)_bundle.Fixture("license-status", "valid-claims", FixtureForm.Entity).DeepClone();
            _bundle.Validate("license.json", "LicenseStatus", entity);
            entity.Add("one element too many");

            Assert.Throws<AssertFailedException>(() => _bundle.Validate("license.json", "LicenseStatus", entity));
        }

        [TestMethod]
        public void Validate_WithAnUnknownDefinition_ThrowsInsteadOfPassing()
        {
            var entity = _bundle.Fixture("review", "minimal", FixtureForm.Entity);

            Assert.Throws<InvalidOperationException>(() => _bundle.Validate("review.json", "NoSuchDefinition", entity));
        }

        [TestMethod]
        public void Validate_WithAnUnknownSchemaFile_ThrowsInsteadOfPassing()
        {
            var entity = _bundle.Fixture("review", "minimal", FixtureForm.Entity);

            Assert.Throws<InvalidOperationException>(() => _bundle.Validate("no-such-schema.json", "Review", entity));
        }

        [TestMethod]
        public void EveryEntityFixtureValidatesAgainstItsSchema()
        {
            var index = (JObject)JToken.Parse(File.ReadAllText(Path.Combine(_bundle.Root, "fixtures", "index.json")));
            var fixtureCount = 0;

            foreach (var root in index.Properties())
            {
                var schemaFile = Text(root.Value, "schema");
                var definition = Text(root.Value, "definition");
                foreach (var variant in _bundle.FixtureVariants(root.Name))
                {
                    _bundle.Validate(schemaFile, definition, _bundle.Fixture(root.Name, variant, FixtureForm.Entity));
                    fixtureCount++;
                }
            }

            Assert.IsGreaterThan(0, fixtureCount);
        }

        [TestMethod]
        public void EveryWireFixtureHasTheCasingTheIndexDeclaresAndValidatesInKebabForm()
        {
            var index = (JObject)JToken.Parse(File.ReadAllText(Path.Combine(_bundle.Root, "fixtures", "index.json")));

            foreach (var root in index.Properties())
            {
                var casing = Text(root.Value, "wire-casing") == "camel" ? Casing.Camel : Casing.Kebab;
                foreach (var variant in _bundle.FixtureVariants(root.Name))
                {
                    var wire = _bundle.Fixture(root.Name, variant, FixtureForm.Wire);
                    ContractBundle.AssertCasing(wire, casing);
                    _bundle.Validate(Text(root.Value, "schema"), Text(root.Value, "definition"), ContractBundle.ToKebabKeys(wire));
                }
            }
        }

        [TestMethod]
        public void AssertCasing_WithTheCamelWireFixture_PassesForCamelAndFailsForKebab()
        {
            var wire = _bundle.Fixture("review", "full", FixtureForm.Wire);

            ContractBundle.AssertCasing(wire, Casing.Camel);
            Assert.Throws<AssertFailedException>(() => ContractBundle.AssertCasing(wire, Casing.Kebab));
        }

        [TestMethod]
        public void AssertCasing_WithAReCasedKeyInACamelFixture_Fails()
        {
            var wire = (JObject)_bundle.Fixture("review", "full", FixtureForm.Wire);
            var score = wire.Property("rawScore") ?? throw new InvalidOperationException("no rawScore");
            score.Replace(new JProperty("raw-score", score.Value));

            var failure = Assert.Throws<AssertFailedException>(() => ContractBundle.AssertCasing(wire, Casing.Camel));

            StringAssert.Contains(failure.Message, "$.raw-score");
        }

        [TestMethod]
        public void AssertCasing_WithAReCasedNestedKeyInAKebabFixture_Fails()
        {
            var entity = _bundle.Fixture("review", "full", FixtureForm.Entity);
            ContractBundle.AssertCasing(entity, Casing.Kebab);
            var range = (JObject)Token(entity, "function-level-code-smells[0].range");
            var startLine = range.Property("start-line") ?? throw new InvalidOperationException("no start-line");
            startLine.Replace(new JProperty("startLine", startLine.Value));

            var failure = Assert.Throws<AssertFailedException>(() => ContractBundle.AssertCasing(entity, Casing.Kebab));

            StringAssert.Contains(failure.Message, "$.function-level-code-smells[0].range.startLine");
        }

        [TestMethod]
        public void ToKebabKeys_ConvertsTheWireFormOfAFixtureToItsEntityForm()
        {
            var wire = _bundle.Fixture("review", "full", FixtureForm.Wire);
            var entity = _bundle.Fixture("review", "full", FixtureForm.Entity);

            Assert.IsTrue(JToken.DeepEquals(entity, ContractBundle.ToKebabKeys(wire)));
        }

        [TestMethod]
        public void AssertDeclaredFieldsMatch_WithTheFixtureItself_Passes()
        {
            var entity = _bundle.Fixture("review", "full", FixtureForm.Entity);

            ContractBundle.AssertDeclaredFieldsMatch(entity, entity.DeepClone(), DeclaredReviewFields);
        }

        [TestMethod]
        public void AssertDeclaredFieldsMatch_WithADefaultedOptional_Fails()
        {
            var fixture = _bundle.Fixture("review", "minimal", FixtureForm.Entity);
            var entity = (JObject)fixture.DeepClone();
            entity["score"] = 0;

            var failure = Assert.Throws<AssertFailedException>(() => ContractBundle.AssertDeclaredFieldsMatch(fixture, entity, DeclaredReviewFields));

            StringAssert.Contains(failure.Message, "$.score: fixture has (absent), entity has 0");
        }

        [TestMethod]
        public void AssertDeclaredFieldsMatch_WithTheSameNumberAsIntegerAndFloat_Passes()
        {
            var fixture = JObject.Parse(@"{""score"":8}");
            var entity = JObject.Parse(@"{""score"":8.0}");

            ContractBundle.AssertDeclaredFieldsMatch(fixture, entity, new[] { "score" });
        }

        [TestMethod]
        public void AssertDeclaredFieldsMatch_WithADifferentNumberOfTheOtherNumericKind_Fails()
        {
            var fixture = JObject.Parse(@"{""score"":8}");
            var entity = JObject.Parse(@"{""score"":8.5}");

            Assert.Throws<AssertFailedException>(() => ContractBundle.AssertDeclaredFieldsMatch(fixture, entity, new[] { "score" }));
        }

        [TestMethod]
        public void AssertDeclaredFieldsMatch_WithADroppedField_Fails()
        {
            var fixture = _bundle.Fixture("review", "full", FixtureForm.Entity);
            var entity = (JObject)fixture.DeepClone();
            entity.Remove("score");

            var failure = Assert.Throws<AssertFailedException>(() => ContractBundle.AssertDeclaredFieldsMatch(fixture, entity, DeclaredReviewFields));

            StringAssert.Contains(failure.Message, "$.score: fixture has 8.5, entity has (absent)");
        }

        [TestMethod]
        public void AssertDeclaredFieldsMatch_WithANullWhereTheFixtureHasNoValue_Fails()
        {
            var fixture = _bundle.Fixture("review", "minimal", FixtureForm.Entity);
            var entity = (JObject)fixture.DeepClone();
            entity["score"] = JValue.CreateNull();

            Assert.Throws<AssertFailedException>(() => ContractBundle.AssertDeclaredFieldsMatch(fixture, entity, DeclaredReviewFields));
        }

        [TestMethod]
        public void AssertDeclaredFieldsMatch_WithAChangedValueInsideAnArray_FailsAtThatElement()
        {
            var fixture = _bundle.Fixture("review", "full", FixtureForm.Entity);
            var entity = fixture.DeepClone();
            Token(entity, "function-level-code-smells[0].range")["start-line"] = 99;

            var failure = Assert.Throws<AssertFailedException>(() => ContractBundle.AssertDeclaredFieldsMatch(fixture, entity, DeclaredReviewFields));

            StringAssert.Contains(failure.Message, "$.function-level-code-smells[0].range.start-line: fixture has 3, entity has 99");
        }

        [TestMethod]
        public void AssertDeclaredFieldsMatch_WithADifferentArrayLength_Fails()
        {
            var fixture = _bundle.Fixture("review", "full", FixtureForm.Entity);
            var entity = fixture.DeepClone();
            ((JArray)Token(entity, "function-level-code-smells")).Clear();

            var failure = Assert.Throws<AssertFailedException>(() => ContractBundle.AssertDeclaredFieldsMatch(fixture, entity, DeclaredReviewFields));

            StringAssert.Contains(failure.Message, "fixture has 1 items, entity has 0");
        }

        [TestMethod]
        public void AssertDeclaredFieldsMatch_IgnoresFieldsTheModelDoesNotDeclare()
        {
            var fixture = _bundle.Fixture("review", "full", FixtureForm.Entity);
            var entity = (JObject)fixture.DeepClone();
            entity["raw-score"] = "changed";

            ContractBundle.AssertDeclaredFieldsMatch(fixture, entity, new[] { "score" });
        }

        [TestMethod]
        public void Manifest_ListsTheReviewRequestWithItsSchemas()
        {
            var entry = _bundle.ManifestEntry("cs-ide/review");

            Assert.AreEqual("schematized", Text(entry, "status"));
            Assert.AreEqual("ReviewParams", Text(entry, "params.definition"));
            Assert.AreEqual("camel", Text(entry, "result.casing"));
        }

        [TestMethod]
        public void ManifestEntry_WithAnUnknownMethod_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _bundle.ManifestEntry("cs-ide/no-such-method"));
        }

        [TestMethod]
        public void Fixture_WithAnUnknownRootOrVariant_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _bundle.Fixture("no-such-root", "minimal", FixtureForm.Entity));
            Assert.Throws<InvalidOperationException>(() => _bundle.Fixture("review", "no-such-variant", FixtureForm.Entity));
        }

        [TestMethod]
        public void Locate_WithAnEnvironmentDirectory_UsesIt()
        {
            var located = ContractBundle.Locate(_bundle.Root, AppContext.BaseDirectory);

            Assert.AreEqual(_bundle.Root, located.Root);
        }

        [TestMethod]
        public void Locate_WithAnEnvironmentZip_ExtractsIt()
        {
            var zip = Path.Combine(NewTempDirectory(), "bundle.zip");
            ZipFile.CreateFromDirectory(_bundle.Root, zip);

            var located = ContractBundle.Locate(zip, AppContext.BaseDirectory);
            _tempDirectories.Add(located.Root);

            located.Validate("review.json", "Review", located.Fixture("review", "minimal", FixtureForm.Entity));
        }

        [TestMethod]
        public void Locate_WithAnEnvironmentPathThatDoesNotExist_Fails()
        {
            var missing = Path.Combine(NewTempDirectory(), "nowhere");

            var failure = Assert.Throws<InvalidOperationException>(() => ContractBundle.Locate(missing, AppContext.BaseDirectory));

            StringAssert.Contains(failure.Message, "does not exist");
        }

        [TestMethod]
        public void Locate_WithoutAnEnvironmentValue_FindsTheBundleInTheRepositoryRoot()
        {
            var repository = NewTempDirectory();
            Directory.CreateDirectory(Path.Combine(repository, ".github"));
            var outputDirectory = Path.Combine(repository, "Codescene.VSExtension.VS2022", "Codescene.VSExtension.VS2022", "bin", "Release", "net48");
            Directory.CreateDirectory(outputDirectory);
            CopyDirectory(_bundle.Root, Path.Combine(repository, "cs-ide-contract"));

            var located = ContractBundle.Locate(string.Empty, outputDirectory);

            Assert.AreEqual(Path.Combine(repository, "cs-ide-contract"), located.Root);
        }

        [TestMethod]
        public void Locate_WithoutAnEnvironmentValueAndWithoutTheBundle_FailsInsteadOfSkipping()
        {
            var repository = NewTempDirectory();
            Directory.CreateDirectory(Path.Combine(repository, ".github"));
            Directory.CreateDirectory(Path.Combine(repository, "Codescene.VSExtension.VS2022", "Codescene.VSExtension.VS2022"));

            var failure = Assert.Throws<InvalidOperationException>(() => ContractBundle.Locate(null, Path.Combine(repository, "Codescene.VSExtension.VS2022", "Codescene.VSExtension.VS2022")));

            StringAssert.Contains(failure.Message, "contract bundle is missing");
        }

        [TestMethod]
        public void At_WithADirectoryThatIsNotABundle_Fails()
        {
            var failure = Assert.Throws<InvalidOperationException>(() => ContractBundle.At(NewTempDirectory()));

            StringAssert.Contains(failure.Message, "is not a contract bundle");
        }

        [TestMethod]
        public void Validate_WithASchemaKeywordTheValidatorDoesNotEnforce_Fails()
        {
            var directory = NewTempDirectory();
            Directory.CreateDirectory(Path.Combine(directory, "schemas"));
            Directory.CreateDirectory(Path.Combine(directory, "fixtures"));
            File.WriteAllText(Path.Combine(directory, "wire-manifest.json"), "[]");
            File.WriteAllText(Path.Combine(directory, "fixtures", "index.json"), "{}");
            File.WriteAllText(Path.Combine(directory, "schemas", "odd.json"), "{\"definitions\":{\"Odd\":{\"type\":\"object\",\"properties\":{\"a\":{\"type\":\"string\",\"pattern\":\"^x$\"}}}}}");

            var failure = Assert.Throws<InvalidOperationException>(() => ContractBundle.At(directory).Validate("odd.json", "Odd", new JObject()));

            StringAssert.Contains(failure.Message, "pattern");
        }

        private static JToken Token(JToken root, string path)
        {
            return root.SelectToken(path) ?? throw new InvalidOperationException($"No {path} in {root}");
        }

        private static string Text(JToken root, string path)
        {
            return Token(root, path).ToString();
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
            }

            foreach (var directory in Directory.GetDirectories(source))
            {
                CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
            }
        }

        private string NewTempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "contract-harness-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            _tempDirectories.Add(directory);
            return directory;
        }
    }
}
