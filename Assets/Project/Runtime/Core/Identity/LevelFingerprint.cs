using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Sokoban.Core.Data;
using Sokoban.Core.Validation;
namespace Sokoban.Core.Identity
{
    public static class LevelFingerprint
    {
        public static string Compute(LevelData level, int rulesVersion = ContentLimits.RulesVersion) => Sha256(CanonicalText(level,rulesVersion));
        public static string CanonicalText(LevelData level, int rulesVersion = ContentLimits.RulesVersion)
        {
            if (rulesVersion != ContentLimits.RulesVersion || !StructureValidator.Validate(level).IsValid)
                throw new ArgumentException("只有当前规则下结构合法的关卡可以生成正式指纹。",nameof(level));
            var text = new StringBuilder("sokoban-level-v1\nrules=");
            text.Append(Number(rulesVersion)).Append("\nsize=").Append(Number(level.width)).Append(',').Append(Number(level.height)).Append("\nterrain=");
            foreach (int tile in level.terrain) text.Append(tile == 0 ? '0' : '1');
            text.Append("\ngoals=").Append(Indices(level.features.Select(f => new Coordinate(f.x, f.y).ToIndex(level.width))));
            var player = level.entities.Single(e => e.type == EntityType.Player);
            text.Append("\nplayer=").Append(Number(new Coordinate(player.x, player.y).ToIndex(level.width)));
            text.Append("\nboxes=").Append(Indices(level.entities.Where(e => e.type == EntityType.Box).Select(e => new Coordinate(e.x, e.y).ToIndex(level.width)))).Append('\n');
            return text.ToString();
        }
        private static string Indices(System.Collections.Generic.IEnumerable<int> indices) => string.Join(",",indices.OrderBy(i=>i).Select(Number));
        internal static string Number(int number) => number.ToString(CultureInfo.InvariantCulture);
        internal static string Sha256(string text)
        {
            using (var hash = SHA256.Create())
            {
                var bytes = hash.ComputeHash(new UTF8Encoding(false,true).GetBytes(text));
                var result = new StringBuilder(64);
                foreach (byte b in bytes) result.Append(b.ToString("x2",CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }
    }
}
