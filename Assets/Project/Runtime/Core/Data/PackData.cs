using System;
using System.Collections.Generic;
namespace Sokoban.Core.Data
{
    public enum DocumentKind { DraftPack, PlayablePack }
    public enum UnlockPolicy { Sequential, AllOpen }
    [Serializable] public sealed class PackData
    {
        public int formatVersion = ContentLimits.FormatVersion, rulesVersion = ContentLimits.RulesVersion;
        public DocumentKind documentKind;
        public string packId = ContentIds.NewId(), name = "", description = "";
        public int contentRevision;
        public UnlockPolicy unlockPolicy;
        public List<LevelData> levels = new List<LevelData>();
        public List<string> levelOrder = new List<string>();
        public List<WitnessData> solutionWitnesses = new List<WitnessData>();
        public PackData DeepCopy()
        {
            var copy = (PackData)MemberwiseClone();
            copy.levels = levels?.ConvertAll(l => l?.DeepCopy());
            copy.levelOrder = levelOrder == null ? null : new List<string>(levelOrder);
            copy.solutionWitnesses = solutionWitnesses?.ConvertAll(w => w?.DeepCopy());
            return copy;
        }
    }
}
