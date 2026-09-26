using System;
using System.Collections.Generic;
using Sokoban.Core.Data;
namespace Sokoban.Core.Validation
{
    /// <summary>Safe in-memory DTO validation. Strict JSON field/duplicate/depth checks belong to the file parser.
    /// This accepts incomplete drafts and never establishes witness truth or playability.</summary>
    public static class PackValidator
    {
        public static ValidationReport Validate(PackData pack)
        {
            var report = new ValidationReport();
            if (pack == null) { Add(report, "PACK_MISSING", "关卡集缺失。"); return report; }
            if (pack.formatVersion != ContentLimits.FormatVersion) Add(report, "FORMAT_UNSUPPORTED", "不支持此格式版本。");
            if (pack.rulesVersion != ContentLimits.RulesVersion) Add(report, "RULES_UNSUPPORTED", "不支持此规则版本。");
            CheckId(report, pack.packId, "PACK_ID_INVALID");
            CheckText(report, pack.name, ContentLimits.MaxNameLength, "PACK_NAME_INVALID");
            CheckText(report, pack.description, ContentLimits.MaxNotesLength, "PACK_DESCRIPTION_INVALID");
            if (!Enum.IsDefined(typeof(DocumentKind), pack.documentKind)) Add(report, "DOCUMENT_KIND_INVALID", "文档类型无效。");
            if (!Enum.IsDefined(typeof(UnlockPolicy), pack.unlockPolicy)) Add(report, "UNLOCK_POLICY_INVALID", "解锁方式无效。");
            if (pack.contentRevision < 0) Add(report, "REVISION_INVALID", "内容版本不能为负数。");
            if (pack.levels == null || pack.levels.Count > ContentLimits.MaxLevels) { Add(report, "LEVELS_INVALID", "关卡列表缺失或超过上限。"); return report; }
            if (pack.documentKind == DocumentKind.PlayablePack && pack.levels.Count == 0) Add(report, "PLAYABLE_PACK_EMPTY", "可玩关卡集至少需要一关。");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var level in pack.levels)
            {
                report.AddRange(ValidateLevel(level));
                if (level != null && !ids.Add(level.levelId ?? "")) Add(report, "LEVEL_ID_DUPLICATE", "关卡 ID 重复。", level.levelId);
            }
            if (pack.levelOrder == null || pack.levelOrder.Count > ContentLimits.MaxLevels) Add(report, "ORDER_INVALID", "关卡顺序缺失或超过上限。");
            else
            {
                var ordered = new HashSet<string>(StringComparer.Ordinal);
                foreach (var id in pack.levelOrder)
                {
                    if (id == null || !ids.Contains(id)) Add(report, "ORDER_REFERENCE_UNKNOWN", "顺序引用了不存在的关卡。", id);
                    if (!ordered.Add(id ?? "")) Add(report, "ORDER_DUPLICATE", "顺序重复引用同一关。", id);
                }
                foreach (var id in ids) if (!ordered.Contains(id)) Add(report, "ORDER_REFERENCE_MISSING", "关卡未出现在顺序中。", id);
            }
            if (pack.solutionWitnesses == null || pack.solutionWitnesses.Count > ContentLimits.MaxLevels) Add(report, "WITNESSES_INVALID", "解法证据列表缺失或超过上限。");
            else
            {
                var witnessed = new HashSet<string>(StringComparer.Ordinal);
                foreach (var witness in pack.solutionWitnesses)
                {
                    if (witness == null) { Add(report, "WITNESS_MISSING", "解法证据缺失。"); continue; }
                    if (witness.levelId == null || !ids.Contains(witness.levelId)) Add(report, "WITNESS_REFERENCE_UNKNOWN", "解法证据引用了不存在的关卡。", witness.levelId);
                    if (!witnessed.Add(witness.levelId ?? "")) Add(report, "WITNESS_DUPLICATE", "每关最多一份导出证据。", witness.levelId);
                    if (witness.rulesVersion != ContentLimits.RulesVersion) Add(report, "WITNESS_RULES_UNSUPPORTED", "证据规则版本无效。", witness.levelId);
                    if (!Enum.IsDefined(typeof(WitnessSource), witness.source)) Add(report, "WITNESS_SOURCE_INVALID", "证据来源无效。", witness.levelId);
                    if (!IsFingerprint(witness.levelFingerprint)) Add(report, "WITNESS_FINGERPRINT_INVALID", "证据指纹必须是小写 SHA-256。", witness.levelId);
                    if (witness.moves == null || witness.moves.Length > ContentLimits.MaxWitnessMoves) Add(report, "WITNESS_MOVES_INVALID", "证据操作序列缺失或超过上限。", witness.levelId);
                    else foreach (char move in witness.moves) if ("UDLR".IndexOf(move) < 0) { Add(report, "WITNESS_MOVES_INVALID", "证据只能包含 U/D/L/R 操作。", witness.levelId); break; }
                }
            }
            return report;
        }

        public static ValidationReport ValidateLevel(LevelData level)
        {
            var report = new ValidationReport();
            if (level == null) { Add(report, "LEVEL_MISSING", "关卡数据缺失。"); return report; }
            string id = level.levelId;
            CheckId(report, id, "LEVEL_ID_INVALID", id);
            CheckText(report, level.name, ContentLimits.MaxNameLength, "LEVEL_NAME_INVALID", id);
            CheckText(report, level.designNotes, ContentLimits.MaxNotesLength, "DESIGN_NOTES_INVALID", id);
            if (!Enum.IsDefined(typeof(IntendedDifficulty), level.intendedDifficulty)) Add(report, "DIFFICULTY_INVALID", "人工难度值无效。", id);
            bool sizeValid = level.width >= ContentLimits.MinWidth && level.width <= ContentLimits.MaxWidth && level.height >= ContentLimits.MinHeight && level.height <= ContentLimits.MaxHeight;
            if (!sizeValid) Add(report, "SIZE_INVALID", "地图宽高必须为 4～20。", id);
            if (level.terrain == null || !sizeValid || level.terrain.Length != level.width * level.height) Add(report, "TERRAIN_LENGTH_INVALID", "地形长度必须等于宽乘高。", id);
            else for (int i = 0; i < level.terrain.Length; i++) if (level.terrain[i] != 0 && level.terrain[i] != 1) Add(report, "TERRAIN_VALUE_INVALID", "地形只能为地板或墙。", id, null, Coordinate.FromIndex(i, level.width));
            var objectIds = new HashSet<string>(StringComparer.Ordinal);
            const int maxObjects = ContentLimits.MaxWidth * ContentLimits.MaxHeight;
            if (level.features == null || level.features.Count > maxObjects) Add(report, "FEATURES_INVALID", "设施列表缺失或超过上限。", id);
            else foreach (var feature in level.features)
            {
                if (feature == null) { Add(report, "FEATURE_MISSING", "设施数据缺失。", id); continue; }
                CheckObject(report, objectIds, feature.id, feature.x, feature.y, level);
                if (!Enum.IsDefined(typeof(FeatureType), feature.type)) Add(report, "FEATURE_TYPE_INVALID", "设施类型无效。", id, feature.id, new Coordinate(feature.x, feature.y));
            }
            if (level.entities == null || level.entities.Count > maxObjects) Add(report, "ENTITIES_INVALID", "实体列表缺失或超过上限。", id);
            else
            {
                int boxes = 0;
                foreach (var entity in level.entities)
                {
                    if (entity == null) { Add(report, "ENTITY_MISSING", "实体数据缺失。", id); continue; }
                    CheckObject(report, objectIds, entity.id, entity.x, entity.y, level);
                    if (!Enum.IsDefined(typeof(EntityType), entity.type)) Add(report, "ENTITY_TYPE_INVALID", "实体类型无效。", id, entity.id, new Coordinate(entity.x, entity.y));
                    if (entity.type == EntityType.Box) boxes++;
                }
                if (boxes > ContentLimits.MaxBoxes) Add(report, "BOX_LIMIT_EXCEEDED", "箱子最多 16 个。", id);
            }
            return report;
        }
        private static void CheckObject(ValidationReport report, HashSet<string> ids, string objectId, int x, int y, LevelData level)
        {
            var position = new Coordinate(x,y);
            if (!IsId(objectId)) Add(report, "OBJECT_ID_INVALID", "对象 ID 格式无效。", level.levelId, objectId, position);
            if (!ids.Add(objectId ?? "")) Add(report, "OBJECT_ID_DUPLICATE", "设施和实体的 ID 必须共同唯一。", level.levelId, objectId, position);
            if (!position.IsInside(level.width,level.height)) Add(report, "OBJECT_OUT_OF_BOUNDS", "对象在地图范围外。", level.levelId, objectId, position);
        }
        public static bool IsId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > ContentLimits.MaxIdLength) return false;
            foreach (char c in value) if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '_' && c != '-') return false;
            return true;
        }
        public static bool IsFingerprint(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char c in value) if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false;
            return true;
        }
        private static void CheckId(ValidationReport report, string value, string code, string levelId = null) { if (!IsId(value)) Add(report, code, "ID 必须为 1～64 位 ASCII 字母、数字、下划线或连字符。", levelId); }
        private static void CheckText(ValidationReport report, string value, int max, string code, string levelId = null) { if (value == null || value.Length > max) Add(report, code, "文本缺失或超过长度上限。", levelId); }
        internal static void Add(ValidationReport report, string code, string message, string levelId = null, string objectId = null, Coordinate? position = null) => report.Add(new ValidationIssue(code,message,levelId,objectId,position));
    }
}
