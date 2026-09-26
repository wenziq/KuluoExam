using System;
namespace Sokoban.Core.Data
{
    public enum WitnessSource { Solver, Manual }
    [Serializable] public sealed class WitnessData
    {
        public string levelId = "", levelFingerprint = "", moves = "";
        public int rulesVersion = ContentLimits.RulesVersion;
        public WitnessSource source;
        public WitnessData DeepCopy() => (WitnessData)MemberwiseClone();
    }
}
