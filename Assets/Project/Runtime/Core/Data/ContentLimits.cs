namespace Sokoban.Core.Data
{
    public static class ContentLimits
    {
        public const int FormatVersion = 1, RulesVersion = 1;
        public const int MaxFileBytes = 8 * 1024 * 1024, MaxLevels = 30;
        public const int MinWidth = 4, MinHeight = 4, MaxWidth = 20, MaxHeight = 20, MaxBoxes = 16;
        public const int DefaultWidth = 10, DefaultHeight = 10;
        public const int MaxNameLength = 80, MaxNotesLength = 2000, MaxIdLength = 64, MaxWitnessMoves = 100000;
    }
}
