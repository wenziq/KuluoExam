namespace Sokoban.Core.Rules
{
    public enum Direction { Up, Down, Left, Right }
    public enum MoveFailure { None, InvalidDirection, OutsideBoard, Wall, BoxBlocked, SessionCompleted }
    public sealed class MoveResult
    {
        public bool Succeeded => Failure == MoveFailure.None;
        public bool Pushed { get; }
        public string BoxId { get; }
        public BoardState State { get; }
        public MoveFailure Failure { get; }
        internal MoveResult(BoardState state, MoveFailure failure, string boxId = null)
        {
            State = state;
            Failure = failure;
            BoxId = boxId;
            Pushed = boxId != null;
        }
        public static MoveResult CompletedSession(BoardState state) => new MoveResult(state, MoveFailure.SessionCompleted);
    }
}
