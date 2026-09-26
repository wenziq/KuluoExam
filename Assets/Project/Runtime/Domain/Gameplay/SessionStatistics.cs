namespace Sokoban.Domain.Gameplay
{
    public sealed class SessionStatistics
    {
        public int UndoCount { get; internal set; }
        public int RestartCount { get; internal set; }
        public double ElapsedSeconds { get; internal set; }
    }
    public sealed class SessionCompletion
    {
        public int RunNumber { get; internal set; }
        public int Moves { get; internal set; }
        public int Pushes { get; internal set; }
        public string Path { get; internal set; }
        public SessionMode Mode { get; internal set; }
    }
}
