using System.Collections.Generic;
using Sokoban.Core.Data;
namespace Sokoban.Core.Validation
{
    /// <summary>Necessary classic gameplay structure only. Does not prove solvability.</summary>
    public static class StructureValidator
    {
        public static ValidationReport Validate(LevelData level)
        {
            var report = PackValidator.ValidateLevel(level);
            if (!report.IsValid) return report;
            ValidateGameplay(level, report);
            return report;
        }

        private static void ValidateGameplay(LevelData level, ValidationReport report)
        {
            var goals = new HashSet<int>();
            var occupied = new HashSet<int>();
            int players = 0, boxes = 0, boxesOnGoals = 0;
            foreach (var feature in level.features)
            {
                var position = new Coordinate(feature.x,feature.y);
                int index = position.ToIndex(level.width);
                if (!goals.Add(index)) PackValidator.Add(report,"GOAL_OVERLAP","同一格不能有重复目标。",level.levelId,feature.id,position);
                if (level.terrain[index] == 1) PackValidator.Add(report,"GOAL_ON_WALL","目标不能放在墙上。",level.levelId,feature.id,position);
            }
            foreach (var entity in level.entities)
            {
                var position = new Coordinate(entity.x,entity.y);
                int index = position.ToIndex(level.width);
                if (!occupied.Add(index)) PackValidator.Add(report,"ENTITY_OVERLAP","每格最多一个实体。",level.levelId,entity.id,position);
                if (level.terrain[index] == 1) PackValidator.Add(report,"ENTITY_ON_WALL","实体不能放在墙上。",level.levelId,entity.id,position);
                if (entity.type == EntityType.Player) players++;
                else { boxes++; if (goals.Contains(index)) boxesOnGoals++; }
            }
            if (players == 0) PackValidator.Add(report,"PLAYER_MISSING","请放置一个玩家。",level.levelId);
            else if (players != 1) PackValidator.Add(report,"PLAYER_COUNT_INVALID","只能放置一个玩家。",level.levelId);
            if (boxes == 0) PackValidator.Add(report,"BOX_MISSING","请至少放置一个箱子。",level.levelId);
            if (boxes != level.features.Count) PackValidator.Add(report,"BOX_GOAL_COUNT_MISMATCH","箱子与目标数量必须相等。",level.levelId);
            if (report.IsValid && boxes > 0 && boxesOnGoals == boxes)
                report.Add(new ValidationIssue("INITIAL_COMPLETED","初始已完成。",level.levelId,severity:IssueSeverity.Warning));
            if (string.IsNullOrWhiteSpace(level.designNotes))
                report.Add(new ValidationIssue("DESIGN_NOTES_EMPTY","可以填写设计备注，帮助后续制作。",level.levelId,severity:IssueSeverity.Warning));
        }
        public static ValidationReport Validate(PackData pack)
        {
            var report = PackValidator.Validate(pack);
            if (!report.IsValid) return report;
            if (pack.levels.Count == 0) PackValidator.Add(report,"PLAYABLE_PACK_EMPTY","试玩至少需要一关。");
            foreach (var level in pack.levels)
            {
                var levelReport = new ValidationReport();
                ValidateGameplay(level, levelReport);
                report.AddRange(levelReport);
            }
            return report;
        }
    }
}
