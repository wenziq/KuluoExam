using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Rules;
namespace Sokoban.Domain.Gameplay
{
    public enum SessionMode { Formal, Trial, AssistedReplayTakeover }
    public sealed class GameSession
    {
        private readonly struct HistoryEntry
        {
            public readonly BoardState State;
            public readonly int Pushes;
            public HistoryEntry(BoardState state, int pushes)
            {
                State = state;
                Pushes = pushes;
            }
        }
        private readonly Stack<HistoryEntry> history = new Stack<HistoryEntry>();
        private readonly StringBuilder path = new StringBuilder();
        private readonly List<SessionCompletion> completions = new List<SessionCompletion>();
        private readonly IReadOnlyList<SessionCompletion> completionView;
        private readonly Func<double> clock;
        private readonly double startTime;
        private readonly string fingerprint;
        private readonly string levelId;
        private readonly SessionStatistics statistics = new SessionStatistics();
        private int runNumber = 1;
        public GameSession(LevelData root, SessionMode mode, Func<double> clock = null)
        {
            if (!Enum.IsDefined(typeof(SessionMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            InitialState = new BoardState(root);
            State = InitialState;
            Mode = mode;
            completionView = completions.AsReadOnly();
            var snapshot = InitialState.ToLevelData();
            levelId = snapshot.levelId;
            fingerprint = LevelFingerprint.Compute(snapshot);
            if (clock == null)
            {
                var timer = Stopwatch.StartNew();
                this.clock = () => timer.Elapsed.TotalSeconds;
            }
            else
            {
                this.clock = clock;
            }
            startTime = this.clock();
            RecordCompletion();
        }
        public BoardState InitialState { get; }
        public BoardState State { get; private set; }
        public SessionMode Mode { get; }
        public int Moves => path.Length;
        public int Pushes { get; private set; }
        public string Path => path.ToString();
        public bool IsCompleted => State.IsWon;
        public int HistoryCount => history.Count;
        public IReadOnlyList<SessionCompletion> Completions => completionView;
        public SessionStatistics Statistics
        {
            get
            {
                double elapsed = clock() - startTime;
                if (!double.IsNaN(elapsed) && !double.IsInfinity(elapsed))
                    statistics.ElapsedSeconds = Math.Max(statistics.ElapsedSeconds, elapsed);
                return new SessionStatistics
                {
                    UndoCount = statistics.UndoCount,
                    RestartCount = statistics.RestartCount,
                    ElapsedSeconds = statistics.ElapsedSeconds
                };
            }
        }
        public MoveResult TryMove(Direction direction)
        {
            if (IsCompleted)
                return MoveResult.CompletedSession(State);
            var move = SokobanRules.TryMove(State, direction);
            if (!move.Succeeded)
                return move;
            history.Push(new HistoryEntry(State, Pushes));
            State = move.State;
            path.Append(SokobanRules.ToCharacter(direction));
            if (move.Pushed)
                Pushes++;
            RecordCompletion();
            return move;
        }
        public bool Undo()
        {
            if (history.Count == 0)
                return false;
            var previous = history.Pop();
            State = previous.State;
            Pushes = previous.Pushes;
            path.Length--;
            statistics.UndoCount++;
            return true;
        }
        public void Restart()
        {
            State = InitialState;
            Pushes = 0;
            path.Clear();
            history.Clear();
            statistics.RestartCount++;
            runNumber++;
            RecordCompletion();
        }
        private void RecordCompletion()
        {
            if (!IsCompleted)
                return;
            completions.Add(new SessionCompletion
            {
                RunNumber = runNumber,
                Moves = Moves,
                Pushes = Pushes,
                Path = Path,
                Mode = Mode
            });
        }
        public WitnessData CreateWitness()
        {
            if (!IsCompleted)
                throw new InvalidOperationException("只有已通关会话可以生成通关证据。");
            var witness = new WitnessData
            {
                levelId = levelId,
                levelFingerprint = fingerprint,
                rulesVersion = ContentLimits.RulesVersion,
                moves = Path,
                source = WitnessSource.Manual
            };
            var verification = WitnessVerifier.Verify(InitialState.ToLevelData(), witness);
            if (!verification.IsValid || verification.Moves != Moves || verification.Pushes != Pushes)
                throw new InvalidOperationException("当前路径未通过共同规则重放：" + verification.Reason);
            return witness;
        }
        /// <summary>Replays from the original root so takeover keeps prefix counts, identities and undo history.</summary>
        public static GameSession FromReplayPrefix(LevelData root, string prefix, Func<double> clock = null)
        {
            if (prefix == null || prefix.Length > ContentLimits.MaxWitnessMoves)
                throw new ArgumentException("接管路径缺失或过长。", nameof(prefix));
            var session = new GameSession(root, SessionMode.AssistedReplayTakeover, clock);
            foreach (char step in prefix)
            {
                if (!SokobanRules.TryParseDirection(step, out var direction) || !session.TryMove(direction).Succeeded)
                    throw new ArgumentException("接管路径包含非法移动或通关后的移动。", nameof(prefix));
            }
            return session;
        }
    }
}
