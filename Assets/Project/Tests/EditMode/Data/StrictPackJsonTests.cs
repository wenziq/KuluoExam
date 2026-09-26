using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Validation;
using Sokoban.Runtime.Persistence;

namespace Sokoban.Tests.EditMode.Data
{
    public class StrictPackJsonTests
    {
        // Hand-authored v1 fixture, transcribed from MASTER_HANDOVER §14.
        private const string EmptyDraft = "{\"formatVersion\":1,\"rulesVersion\":1,\"documentKind\":\"DraftPack\",\"packId\":\"p1\",\"name\":\"中文\",\"description\":\"\",\"contentRevision\":0,\"unlockPolicy\":\"Sequential\",\"levels\":[],\"levelOrder\":[],\"solutionWitnesses\":[]}";

        [Test] public void HandAuthoredDraftParsesWithoutRequiringPlayableStructure()
        {
            var pack = StrictPackJson.Parse(EmptyDraft);
            Assert.That(pack.packId, Is.EqualTo("p1"));
            Assert.That(pack.name, Is.EqualTo("中文"));
            Assert.That(PackValidator.Validate(pack).IsValid, Is.True);
        }

        [TestCase(DocumentKind.DraftPack)]
        [TestCase(DocumentKind.PlayablePack)]
        public void RealDtoRoundTripPreservesIdentityCoordinatesOrderTextAndEmptyWitness(DocumentKind kind)
        {
            var pack = PackValidationTests.Pack();
            pack.documentKind = kind;
            pack.name = "推箱子😀";
            pack.description = "描述\n第二行";
            pack.levels[0].designNotes = "引号\"和\\";
            var second = PackValidationTests.Level();
            second.levelId = "level2";
            pack.levels.Add(second);
            pack.levelOrder.Insert(0, "level2");
            pack.solutionWitnesses.Add(new WitnessData { levelId = "level1", levelFingerprint = LevelFingerprint.Compute(pack.levels[0]), moves = "", source = WitnessSource.Manual });
            var json = StrictPackJson.SerializeToString(pack);
            Assert.That(json, Does.Contain("\"documentKind\":\"" + kind + "\""));
            Assert.That(json, Does.Not.Contain("$type"));
            var result = StrictPackJson.Parse(StrictPackJson.Serialize(pack));
            Assert.That(DocumentHash.Compute(result), Is.EqualTo(DocumentHash.Compute(pack)));
            Assert.That(result.levelOrder, Is.EqualTo(new[] { "level2", "level1" }));
            Assert.That(result.levels[0].entities[0].x, Is.EqualTo(1));
            Assert.That(result.levels[0].features[0].y, Is.EqualTo(2));
            Assert.That(result.solutionWitnesses[0].moves, Is.Empty);
            Assert.That(PackValidator.Validate(result).IsValid, Is.True);
        }

        [TestCase("/*comment*/")]
        [TestCase("//comment\n")]
        [TestCase("{}")]
        [TestCase("null")]
        public void TrailingContentIsRejected(string suffix) => Reject(EmptyDraft + suffix);

        [TestCase("'name':'x'")]
        [TestCase("\"name\":NaN")]
        [TestCase("\"name\":undefined")]
        [TestCase("\"name\":\"x\",\"name\":\"y\"")]
        [TestCase("\"na\\u006de\":\"x\",\"name\":\"y\"")]
        [TestCase("\"name\":\"\\uD800\"")]
        [TestCase("\"name\":\"\\uDC00\"")]
        [TestCase("\"name\":\"\\uD800x\"")]
        [TestCase("\"name\":\"line\nline\"")]
        public void NonStandardOrInvalidStringsAndDuplicateKeysAreRejected(string replacement)
            => Reject(EmptyDraft.Replace("\"name\":\"中文\"", replacement));

        [Test] public void TrailingCommasAndDeepNestingAreRejected()
        {
            Reject(EmptyDraft.Replace("\"levels\":[]", "\"levels\":[,]"));
            Reject(EmptyDraft.Insert(EmptyDraft.Length - 1, ","));
            var depth = Assert.Throws<FormatException>(() => StrictPackJson.Parse(new string('[', 100) + "0" + new string(']', 100)));
            Assert.That(depth.Message, Does.Contain("32"));
        }

        [TestCase("formatVersion", "2")]
        [TestCase("rulesVersion", "2")]
        [TestCase("formatVersion", "1.0")]
        [TestCase("contentRevision", "1e0")]
        [TestCase("contentRevision", "2147483648")]
        [TestCase("contentRevision", "-1")]
        [TestCase("documentKind", "0")]
        [TestCase("documentKind", "\"draftpack\"")]
        [TestCase("unlockPolicy", "\"Unknown\"")]
        [TestCase("name", "null")]
        [TestCase("name", "true")]
        [TestCase("levels", "{}")]
        [TestCase("levelOrder", "[null]")]
        public void ExactFieldTypesAndValuesAreRequired(string field, string value)
        {
            var token = JObject.Parse(EmptyDraft);
            token[field] = JToken.Parse(value);
            // Json.NET normalizes 1e0, so retain its source spelling explicitly.
            string json = token.ToString(Newtonsoft.Json.Formatting.None);
            if (value == "1e0") json = json.Replace("\"contentRevision\":1.0", "\"contentRevision\":1e0");
            Reject(json);
        }

        [Test] public void EveryRootFieldIsRequiredAndUnknownFieldsAreRejected()
        {
            foreach (var property in JObject.Parse(EmptyDraft).Properties().ToArray())
            {
                var token = JObject.Parse(EmptyDraft);
                token.Remove(property.Name);
                Reject(token.ToString());
            }
            Reject(EmptyDraft.Insert(1, "\"$type\":\"System.IO.FileInfo, mscorlib\","));
            Reject(EmptyDraft.Insert(1, "\"path\":\"/tmp/should-not-read\","));
        }

        [Test] public void NestedFieldsDuplicatesAndCollectionLimitsAreChecked()
        {
            string json = StrictPackJson.SerializeToString(PackValidationTests.Pack());
            Reject(json.Replace("\"width\":5", "\"width\":5,\"width\":5"));
            foreach (string field in new[] { "levelId", "name", "designNotes", "intendedDifficulty", "width", "height", "terrain", "features", "entities" })
            {
                var token = JObject.Parse(json);
                ((JObject)token["levels"][0]).Remove(field);
                Reject(token.ToString());
            }
            var many = JObject.Parse(EmptyDraft);
            many["levels"] = new JArray(Enumerable.Range(0, 31).Select(_ => JObject.Parse(json)["levels"][0]));
            Reject(many.ToString());
            var bad = JObject.Parse(json);
            bad["levels"][0]["terrain"] = new JArray(Enumerable.Repeat(0, 401));
            Reject(bad.ToString());
            bad = JObject.Parse(json);
            bad["levels"][0]["entities"][0]["x"] = 99;
            Reject(bad.ToString());
        }

        [Test] public void TextAndEvidenceLimitsApplyToParsingAndSerialization()
        {
            var pack = PackValidationTests.Pack();
            pack.description = new string('中', 2000);
            pack.solutionWitnesses.Add(new WitnessData { levelId = "level1", levelFingerprint = new string('a', 64), moves = new string('U', 100000) });
            Assert.DoesNotThrow(() => StrictPackJson.Parse(StrictPackJson.Serialize(pack)));
            pack.description += "中";
            Assert.Throws<FormatException>(() => StrictPackJson.Serialize(pack));
            pack.description = "";
            pack.solutionWitnesses[0].moves += "U";
            Assert.Throws<FormatException>(() => StrictPackJson.Serialize(pack));
        }

        [Test] public void ByteLimitIsAppliedBeforeUtf8DecodeAndExactBoundaryIsAccepted()
        {
            var content = Encoding.UTF8.GetBytes(EmptyDraft);
            var boundary = Enumerable.Repeat((byte)' ', ContentLimits.MaxFileBytes).ToArray();
            Array.Copy(content, boundary, content.Length);
            Assert.That(StrictPackJson.Parse(boundary).packId, Is.EqualTo("p1"));
            var oversized = new byte[ContentLimits.MaxFileBytes + 1];
            oversized[0] = 0xFF;
            Assert.That(Assert.Throws<FormatException>(() => StrictPackJson.Parse(oversized)).Message, Does.Contain("8 MiB"));
            Assert.Throws<FormatException>(() => StrictPackJson.Parse(new byte[] { 0xC0, 0xAF }));
            Assert.Throws<FormatException>(() => StrictPackJson.Parse(new byte[] { 0xED, 0xA0, 0x80 }));
            Reject(EmptyDraft.Replace("中文", "\uD800"));
            Assert.That(StrictPackJson.Parse(EmptyDraft.Replace("中文", "\\uD83D\\uDE00")).name, Is.EqualTo("😀"));
        }

        [Test] public void InitialVictoryEmptyWitnessAndIncompleteDraftBothRemainRepresentable()
        {
            var level = AsciiLevelFactory.Create("####", "#  #", "#@*#", "####");
            level.levelId = "initial";
            var pack = new PackData { levels = { level }, levelOrder = { "initial" } };
            pack.solutionWitnesses.Add(new WitnessData { levelId = "initial", levelFingerprint = LevelFingerprint.Compute(level), moves = "" });
            Assert.That(StrictPackJson.Parse(StrictPackJson.Serialize(pack)).solutionWitnesses[0].moves, Is.Empty);
            pack.solutionWitnesses.Clear();
            pack.levels[0].entities.Clear();
            Assert.DoesNotThrow(() => StrictPackJson.Parse(StrictPackJson.Serialize(pack)));
        }

        [Test] public void AllNestedObjectAndWitnessFieldsAreRequired()
        {
            var pack = PackValidationTests.Pack();
            pack.solutionWitnesses.Add(new WitnessData { levelId = "level1", levelFingerprint = new string('a', 64) });
            string json = StrictPackJson.SerializeToString(pack);
            foreach (string location in new[] { "levels[0].features[0]", "levels[0].entities[0]", "solutionWitnesses[0]" })
            {
                foreach (string field in ((JObject)JObject.Parse(json).SelectToken(location)).Properties().Select(p => p.Name))
                {
                    var token = JObject.Parse(json);
                    ((JObject)token.SelectToken(location)).Remove(field);
                    Reject(token.ToString());
                }
                var unknown = JObject.Parse(json);
                ((JObject)unknown.SelectToken(location))["$type"] = "System.IO.FileInfo";
                Reject(unknown.ToString());
            }
        }

        [TestCase("levels[0].width", "5.0")]
        [TestCase("levels[0].width", "\"5\"")]
        [TestCase("levels[0].terrain[0]", "true")]
        [TestCase("levels[0].terrain[0]", "2")]
        [TestCase("levels[0].features[0].type", "\"Portal\"")]
        [TestCase("levels[0].entities[0].type", "0")]
        [TestCase("levels[0].intendedDifficulty", "\"Expert\"")]
        [TestCase("solutionWitnesses[0].source", "\"External\"")]
        [TestCase("solutionWitnesses[0].rulesVersion", "2")]
        [TestCase("solutionWitnesses[0].moves", "\"Q\"")]
        public void NestedFieldTypesEnumsAndEvidenceAreStrict(string path, string replacement)
        {
            var pack = PackValidationTests.Pack();
            pack.solutionWitnesses.Add(new WitnessData { levelId = "level1", levelFingerprint = new string('a', 64) });
            var token = JObject.Parse(StrictPackJson.SerializeToString(pack));
            token.SelectToken(path).Replace(JToken.Parse(replacement));
            Reject(token.ToString());
        }

        [Test] public void CollectionsAndTextAcceptBoundariesAndRejectOverflowOnImport()
        {
            var pack = new PackData { name = new string('名', 80), description = new string('述', 2000) };
            for (int i = 0; i < 30; i++)
            {
                var level = new LevelData { levelId = "level" + i, width = 20, height = 20, terrain = new int[400] };
                pack.levels.Add(level);
                pack.levelOrder.Add(level.levelId);
            }
            string json = StrictPackJson.SerializeToString(pack);
            Assert.That(StrictPackJson.Parse(json).levels.Count, Is.EqualTo(30));
            var token = JObject.Parse(json);
            token["name"] = new string('名', 81);
            Reject(token.ToString());
            token = JObject.Parse(json);
            token["description"] = new string('述', 2001);
            Reject(token.ToString());
            token = JObject.Parse(json);
            ((JArray)token["levelOrder"]).Add("extra");
            Reject(token.ToString());
            token = JObject.Parse(json);
            token["levels"][0]["features"] = new JArray(Enumerable.Range(0, 401).Select(i => new JObject { ["id"] = "goal" + i, ["type"] = "Goal", ["x"] = 0, ["y"] = 0 }));
            Reject(token.ToString());
        }

        [Test] public void FailuresLeaveOriginalDtoUntouchedAndUtf8BomIsSupported()
        {
            var original = PackValidationTests.Pack();
            string before = StrictPackJson.SerializeToString(original);
            Reject(before.Replace("\"packId\":\"pack1\"", "\"packId\":\"../escape\""));
            Assert.That(StrictPackJson.SerializeToString(original), Is.EqualTo(before));
            var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(EmptyDraft)).ToArray();
            Assert.That(StrictPackJson.Parse(bytes).packId, Is.EqualTo("p1"));
            Assert.Throws<FormatException>(() => StrictPackJson.Parse((string)null));
            Assert.Throws<FormatException>(() => StrictPackJson.Parse((byte[])null));
            Assert.Throws<FormatException>(() => StrictPackJson.Serialize(null));
        }

        private static void Reject(string json)
        {
            var error = Assert.Throws<FormatException>(() => StrictPackJson.Parse(json));
            Assert.That(error.Message, Is.Not.Empty);
        }
    }
}
