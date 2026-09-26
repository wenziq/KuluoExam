using System;
using System.Collections.Generic;
using System.Threading;
using Sokoban.Core.Data;
using Sokoban.Core.Rules;
namespace Sokoban.Domain.Gameplay
{
    /// <summary>Complete immutable positions at push boundaries, including the walking prefix.</summary>
    public sealed class SolutionPlaybackSession
    {
        readonly LevelData root;
        readonly string moves;
        readonly List<BoardState> states=new List<BoardState>();
        readonly List<int> offsets=new List<int>();
        readonly int firstWinOffset;
        public SolutionPlaybackSession(LevelData root, WitnessData witness, CancellationToken token=default)
        {
            var verified=WitnessVerifier.Verify(root,witness,cancellationToken:token);
            token.ThrowIfCancellationRequested();
            if(!verified.IsValid)throw new ArgumentException("参考解无效："+verified.Reason,nameof(witness));
            this.root=root.DeepCopy();moves=witness.moves;
            // The maximum 100k pushes retain < 40 MiB: shared geometry, <=16 box coordinates per position.
            var state=new BoardState(this.root);states.Add(state);offsets.Add(0);int firstWin=state.IsWon?0:int.MaxValue;
            for(int i=0;i<moves.Length;i++)
            {
                token.ThrowIfCancellationRequested();SokobanRules.TryParseDirection(moves[i],out var direction);
                var move=SokobanRules.TryMove(state,direction);state=move.State;
                if(move.Pushed){states.Add(state);offsets.Add(i+1);}
                if(state.IsWon&&firstWin==int.MaxValue)firstWin=i+1;
            }
            // Imported valid evidence can include trailing walking. Its final boundary shows the actual final state.
            states[states.Count-1]=state;offsets[offsets.Count-1]=moves.Length;
            firstWinOffset=firstWin;SeekPush(0);
        }
        public BoardState State=>states[PushIndex];
        public int PushIndex {get; private set;}
        public int TotalPushes=>states.Count-1;
        public int MoveOffset=>offsets[PushIndex];
        public bool CanTakeOver=>MoveOffset<=firstWinOffset;
        public void SeekPush(int index)
        {if(index<0||index> TotalPushes)throw new ArgumentOutOfRangeException(nameof(index));PushIndex=index;}
        public GameSession CreateTakeover()
        {
            if(!CanTakeOver)throw new InvalidOperationException("这段外部参考解包含通关后的移动，请回到通关前接管。");
            return GameSession.FromReplayPrefix(root,moves.Substring(0,MoveOffset));
        }
    }
}
