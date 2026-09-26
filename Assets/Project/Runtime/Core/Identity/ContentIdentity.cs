using System;
using System.Collections.Generic;
using Sokoban.Core.Data;
using Sokoban.Core.Validation;
namespace Sokoban.Core.Identity
{
    public static class ContentIdentity
    {
        /// <summary>Create a separate document preserving its kind; retained witnesses are only reference-rebound,
        /// never promoted to verified evidence by copying.</summary>
        public static PackData CreateIndependentCopy(PackData pack)
        {
            if (!PackValidator.Validate(pack).IsValid) throw new ArgumentException("复制前必须通过安全结构校验。",nameof(pack));
            var copy = pack.DeepCopy();
            copy.packId = ContentIds.NewId();copy.contentRevision = 0;
            var ids = new Dictionary<string,string>(StringComparer.Ordinal);
            foreach (var level in copy.levels)
            {
                string original = level.levelId;level.levelId = ContentIds.NewId();ids.Add(original,level.levelId);
                foreach (var feature in level.features) feature.id = ContentIds.NewId();
                foreach (var entity in level.entities) entity.id = ContentIds.NewId();
            }
            for (int i = 0; i < copy.levelOrder.Count; i++) copy.levelOrder[i] = ids[copy.levelOrder[i]];
            foreach (var witness in copy.solutionWitnesses) witness.levelId = ids[witness.levelId];
            return copy;
        }
    }
}
