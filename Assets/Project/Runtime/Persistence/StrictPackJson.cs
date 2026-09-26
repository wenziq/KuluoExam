using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Sokoban.Core.Data;
using Sokoban.Core.Validation;

namespace Sokoban.Runtime.Persistence
{
    /// <summary>
    /// Strict v1 data codec. Passing this boundary establishes safe DTO structure only;
    /// witnesses still require local replay and playable packs may still need verification.
    /// No file access or input-directed type construction occurs here.
    /// </summary>
    public static class StrictPackJson
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public static PackData Parse(byte[] bytes)
        {
            if (bytes == null) throw new FormatException("关卡包字节数据缺失。");
            CheckSize(bytes.Length);
            try
            {
                int offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
                return ParseText(Utf8.GetString(bytes, offset, bytes.Length - offset));
            }
            catch (DecoderFallbackException error)
            {
                throw new FormatException("关卡包不是有效 UTF-8。", error);
            }
        }

        public static PackData Parse(string json)
        {
            if (json == null) throw new FormatException("关卡包 JSON 文本缺失。");
            CheckSize(json.Length); // Every UTF-16 unit needs at least one UTF-8 byte.
            try
            {
                CheckSize(Utf8.GetByteCount(json));
            }
            catch (EncoderFallbackException error)
            {
                throw new FormatException("JSON 文本包含无效 Unicode。", error);
            }
            return ParseText(json);
        }

        public static byte[] Serialize(PackData pack)
        {
            return Utf8.GetBytes(SerializeToString(pack));
        }

        public static string SerializeToString(PackData pack)
        {
            ValidatePack(pack);
            var serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                Culture = CultureInfo.InvariantCulture,
                Converters = new List<JsonConverter> { new StringEnumConverter { AllowIntegerValues = false } }
            });
            string json = JObject.FromObject(pack, serializer).ToString(Formatting.None);
            // Share the wire boundary so invalid Unicode and future DTO fields cannot leak out.
            Parse(json);
            return json;
        }

        private static PackData ParseText(string json)
        {
            JsonSyntaxGuard.Validate(json);
            try
            {
                using (var input = new StringReader(json))
                using (var reader = new JsonTextReader(input)
                {
                    DateParseHandling = DateParseHandling.None,
                    MaxDepth = JsonSyntaxGuard.MaxDepth,
                    Culture = CultureInfo.InvariantCulture
                })
                {
                    var token = JToken.ReadFrom(reader, new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                        CommentHandling = CommentHandling.Ignore
                    });
                    var root = Object(token, "$", "formatVersion", "rulesVersion", "documentKind", "packId", "name", "description", "contentRevision", "unlockPolicy", "levels", "levelOrder", "solutionWitnesses");
                    var pack = new PackData
                    {
                        formatVersion = Integer(root["formatVersion"], "$.formatVersion"),
                        rulesVersion = Integer(root["rulesVersion"], "$.rulesVersion"),
                        documentKind = EnumValue<DocumentKind>(root["documentKind"], "$.documentKind"),
                        packId = Text(root["packId"], "$.packId", ContentLimits.MaxIdLength),
                        name = Text(root["name"], "$.name", ContentLimits.MaxNameLength),
                        description = Text(root["description"], "$.description", ContentLimits.MaxNotesLength),
                        contentRevision = Integer(root["contentRevision"], "$.contentRevision"),
                        unlockPolicy = EnumValue<UnlockPolicy>(root["unlockPolicy"], "$.unlockPolicy")
                    };
                    RequireVersion(pack.formatVersion, ContentLimits.FormatVersion, "$.formatVersion");
                    RequireVersion(pack.rulesVersion, ContentLimits.RulesVersion, "$.rulesVersion");
                    var levels = Array(root["levels"], "$.levels", ContentLimits.MaxLevels);
                    for (int i = 0; i < levels.Count; i++) pack.levels.Add(Level(levels[i], "$.levels[" + i + "]"));
                    var order = Array(root["levelOrder"], "$.levelOrder", ContentLimits.MaxLevels);
                    for (int i = 0; i < order.Count; i++) pack.levelOrder.Add(Text(order[i], "$.levelOrder[" + i + "]", ContentLimits.MaxIdLength));
                    var witnesses = Array(root["solutionWitnesses"], "$.solutionWitnesses", ContentLimits.MaxLevels);
                    for (int i = 0; i < witnesses.Count; i++) pack.solutionWitnesses.Add(Witness(witnesses[i], "$.solutionWitnesses[" + i + "]"));
                    ValidatePack(pack);
                    return pack;
                }
            }
            catch (JsonException error)
            {
                throw new FormatException("关卡包 JSON 格式错误：" + error.Message, error);
            }
        }

        private static LevelData Level(JToken token, string path)
        {
            var data = Object(token, path, "levelId", "name", "designNotes", "intendedDifficulty", "width", "height", "terrain", "features", "entities");
            var level = new LevelData
            {
                levelId = Text(data["levelId"], path + ".levelId", ContentLimits.MaxIdLength),
                name = Text(data["name"], path + ".name", ContentLimits.MaxNameLength),
                designNotes = Text(data["designNotes"], path + ".designNotes", ContentLimits.MaxNotesLength),
                intendedDifficulty = EnumValue<IntendedDifficulty>(data["intendedDifficulty"], path + ".intendedDifficulty"),
                width = Integer(data["width"], path + ".width"),
                height = Integer(data["height"], path + ".height")
            };
            var terrain = Array(data["terrain"], path + ".terrain", ContentLimits.MaxWidth * ContentLimits.MaxHeight);
            level.terrain = new int[terrain.Count];
            for (int i = 0; i < terrain.Count; i++) level.terrain[i] = Integer(terrain[i], path + ".terrain[" + i + "]");
            var features = Array(data["features"], path + ".features", ContentLimits.MaxWidth * ContentLimits.MaxHeight);
            for (int i = 0; i < features.Count; i++)
            {
                string itemPath = path + ".features[" + i + "]";
                var feature = Object(features[i], itemPath, "id", "type", "x", "y");
                level.features.Add(new FeatureData
                {
                    id = Text(feature["id"], itemPath + ".id", ContentLimits.MaxIdLength),
                    type = EnumValue<FeatureType>(feature["type"], itemPath + ".type"),
                    x = Integer(feature["x"], itemPath + ".x"),
                    y = Integer(feature["y"], itemPath + ".y")
                });
            }
            var entities = Array(data["entities"], path + ".entities", ContentLimits.MaxWidth * ContentLimits.MaxHeight);
            for (int i = 0; i < entities.Count; i++)
            {
                string itemPath = path + ".entities[" + i + "]";
                var entity = Object(entities[i], itemPath, "id", "type", "x", "y");
                level.entities.Add(new EntityData
                {
                    id = Text(entity["id"], itemPath + ".id", ContentLimits.MaxIdLength),
                    type = EnumValue<EntityType>(entity["type"], itemPath + ".type"),
                    x = Integer(entity["x"], itemPath + ".x"),
                    y = Integer(entity["y"], itemPath + ".y")
                });
            }
            return level;
        }

        private static WitnessData Witness(JToken token, string path)
        {
            var data = Object(token, path, "levelId", "levelFingerprint", "rulesVersion", "moves", "source");
            return new WitnessData
            {
                levelId = Text(data["levelId"], path + ".levelId", ContentLimits.MaxIdLength),
                levelFingerprint = Text(data["levelFingerprint"], path + ".levelFingerprint", 64),
                rulesVersion = Integer(data["rulesVersion"], path + ".rulesVersion"),
                moves = Text(data["moves"], path + ".moves", ContentLimits.MaxWitnessMoves),
                source = EnumValue<WitnessSource>(data["source"], path + ".source")
            };
        }

        private static JObject Object(JToken token, string path, params string[] fields)
        {
            if (!(token is JObject value)) throw Error(path, "必须是对象。");
            var expected = new HashSet<string>(fields, StringComparer.Ordinal);
            foreach (var property in value.Properties())
            {
                if (!expected.Contains(property.Name)) throw Error(path, "包含未知字段；协议不接受类型、路径或额外元数据。");
            }
            foreach (string field in fields)
            {
                if (value.Property(field, StringComparison.Ordinal) == null) throw Error(path + "." + field, "缺少必需字段。");
            }
            return value;
        }

        private static JArray Array(JToken token, string path, int limit)
        {
            if (!(token is JArray value)) throw Error(path, "必须是数组。");
            if (value.Count > limit) throw Error(path, "数组超过上限 " + limit + "。");
            return value;
        }

        private static string Text(JToken token, string path, int limit)
        {
            if (token == null || token.Type != JTokenType.String) throw Error(path, "必须是字符串。");
            string value = token.Value<string>();
            if (value.Length > limit) throw Error(path, "字符串超过长度上限 " + limit + "。");
            return value;
        }

        private static int Integer(JToken token, string path)
        {
            if (token == null || token.Type != JTokenType.Integer) throw Error(path, "必须是整数 token，不接受小数、指数或字符串。");
            if (!int.TryParse(token.ToString(Formatting.None), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value))
                throw Error(path, "整数超出 Int32 范围。");
            return value;
        }

        private static T EnumValue<T>(JToken token, string path) where T : struct
        {
            string name = Text(token, path, 64);
            foreach (string allowed in Enum.GetNames(typeof(T)))
            {
                if (string.Equals(name, allowed, StringComparison.Ordinal)) return (T)Enum.Parse(typeof(T), name, false);
            }
            throw Error(path, "未知枚举值。");
        }

        private static void RequireVersion(int actual, int expected, string path)
        {
            if (actual != expected) throw Error(path, "不支持此版本；仅支持 " + expected + "。");
        }

        private static void ValidatePack(PackData pack)
        {
            var report = PackValidator.Validate(pack);
            foreach (var issue in report.Issues)
            {
                if (issue.Severity == IssueSeverity.Error) throw new FormatException("关卡包安全校验失败 [" + issue.Code + "]：" + issue.Message);
            }
        }

        private static void CheckSize(int count)
        {
            if (count > ContentLimits.MaxFileBytes) throw new FormatException("关卡包超过 8 MiB 字节上限。");
        }

        private static FormatException Error(string path, string message) => new FormatException(path + "：" + message);
    }
}
