using System;
using System.Text;
using Sokoban.Core.Data;
namespace Sokoban.Core.Identity
{
    /// <summary>Fixed field order; strings are Unicode NFC and length-prefixed in UTF-16 code units,
    /// then encoded as strict UTF-8. Lists retain their explicit order. Null is distinct from empty.
    /// Includes all authoring fields; excludes contentRevision and derived solutionWitnesses.</summary>
    public static class DocumentHash
    {
        public static string Compute(PackData pack)
        {
            if (pack == null) throw new ArgumentNullException(nameof(pack));
            var text = new StringBuilder("sokoban-document-v1\n");
            Number(text,pack.formatVersion);Number(text,pack.rulesVersion);Number(text,(int)pack.documentKind);
            Value(text,pack.packId);Value(text,pack.name);Value(text,pack.description);Number(text,(int)pack.unlockPolicy);
            Number(text,pack.levels?.Count ?? -1);
            if (pack.levels != null) foreach (var level in pack.levels)
            {
                Number(text,level == null ? 0 : 1);if (level == null) continue;
                Value(text,level.levelId);Value(text,level.name);Value(text,level.designNotes);Number(text,(int)level.intendedDifficulty);
                Number(text,level.width);Number(text,level.height);
                Number(text,level.terrain?.Length ?? -1);if (level.terrain != null) foreach (int tile in level.terrain) Number(text,tile);
                Number(text,level.features?.Count ?? -1);
                if (level.features != null) foreach (var feature in level.features)
                {
                    Number(text,feature == null ? 0 : 1);if (feature == null) continue;
                    Value(text,feature.id);Number(text,(int)feature.type);Number(text,feature.x);Number(text,feature.y);
                }
                Number(text,level.entities?.Count ?? -1);
                if (level.entities != null) foreach (var entity in level.entities)
                {
                    Number(text,entity == null ? 0 : 1);if (entity == null) continue;
                    Value(text,entity.id);Number(text,(int)entity.type);Number(text,entity.x);Number(text,entity.y);
                }
            }
            Number(text,pack.levelOrder?.Count ?? -1);
            if (pack.levelOrder != null) foreach (string id in pack.levelOrder) Value(text,id);
            return LevelFingerprint.Sha256(text.ToString());
        }
        private static void Number(StringBuilder text,int value) => text.Append(LevelFingerprint.Number(value)).Append(';');
        private static void Value(StringBuilder text,string value)
        {
            if (value == null) { text.Append("-1:");return; }
            string normalized = value.Normalize(NormalizationForm.FormC);
            text.Append(LevelFingerprint.Number(normalized.Length)).Append(':').Append(normalized);
        }
    }
}
