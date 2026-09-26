using System;
namespace Sokoban.Core.Data
{
    /// <summary>Fixture helper; may produce sub-4-cell maps for tests. Production callers must validate the returned DTO.</summary>
    public static class AsciiLevelFactory
    {
        // Top row first: # wall, space/- floor, . goal, @ player, $ box, + player on goal, * box on goal.
        public static LevelData Create(params string[] rows)
        {
            if (rows == null || rows.Length == 0 || rows.Length > ContentLimits.MaxHeight || string.IsNullOrEmpty(rows[0]) || rows[0].Length > ContentLimits.MaxWidth)
                throw new ArgumentException("ASCII 地图尺寸无效。", nameof(rows));
            var level = new LevelData { width = rows[0].Length, height = rows.Length };
            level.terrain = new int[level.width * level.height];
            for (int row = 0; row < rows.Length; row++)
            {
                if (rows[row] == null || rows[row].Length != level.width) throw new ArgumentException("ASCII 地图各行必须等宽。", nameof(rows));
                int y = rows.Length - 1 - row;
                for (int x = 0; x < level.width; x++)
                {
                    char tile = rows[row][x];
                    if ("# -.@$+*".IndexOf(tile) < 0) throw new ArgumentException("ASCII 地图包含未知字符。", nameof(rows));
                    level.terrain[y * level.width + x] = tile == '#' ? 1 : 0;
                    if (tile == '.' || tile == '+' || tile == '*') level.features.Add(new FeatureData { type = FeatureType.Goal, x = x, y = y });
                    if (tile == '@' || tile == '+') level.entities.Add(new EntityData { type = EntityType.Player, x = x, y = y });
                    if (tile == '$' || tile == '*') level.entities.Add(new EntityData { type = EntityType.Box, x = x, y = y });
                }
            }
            return level;
        }
    }
}
