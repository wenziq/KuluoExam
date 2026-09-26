using System;
using System.Linq;
using Sokoban.Core.Data;
using Sokoban.Core.Validation;
namespace Sokoban.Core.Rules
{
    /// <summary>Immutable position. Transitions share isolated static geometry and box identity.</summary>
    public sealed class BoardState
    {
        private readonly LevelData root;
        private readonly bool[] goals;
        private readonly string[] boxIds;
        private readonly Coordinate[] boxes;
        public BoardState(LevelData level)
        {
            if (!StructureValidator.Validate(level).IsValid)
                throw new ArgumentException("棋盘必须具有合法的玩法结构。", nameof(level));
            root = level.DeepCopy();
            goals = new bool[root.width * root.height];
            foreach (var goal in root.features)
                goals[goal.y * root.width + goal.x] = true;
            var boxEntities = root.entities.Where(entity => entity.type == EntityType.Box).ToArray();
            boxIds = boxEntities.Select(entity => entity.id).ToArray();
            boxes = boxEntities.Select(entity => new Coordinate(entity.x, entity.y)).ToArray();
            var player = root.entities.Single(entity => entity.type == EntityType.Player);
            Player = new Coordinate(player.x, player.y);
        }
        private BoardState(BoardState previous, Coordinate player, Coordinate[] positions)
        {
            root = previous.root;
            goals = previous.goals;
            boxIds = previous.boxIds;
            boxes = positions;
            Player = player;
        }
        public int Width => root.width;
        public int Height => root.height;
        public Coordinate Player { get; }
        public int BoxCount => boxes.Length;
        public bool IsWon => boxes.All(IsGoal);
        public Coordinate GetBoxPosition(int index) => boxes[index];
        public string GetBoxId(int index) => boxIds[index];
        public bool IsGoal(Coordinate position) => position.IsInside(Width, Height) && goals[position.ToIndex(Width)];
        public bool IsWall(Coordinate position) => !position.IsInside(Width, Height) || root.terrain[position.ToIndex(Width)] == (int)TerrainType.Wall;
        internal int FindBox(Coordinate position) => Array.IndexOf(boxes, position);
        internal BoardState Move(Coordinate player, int boxIndex, Coordinate boxPosition)
        {
            var positions = boxes;
            if (boxIndex >= 0)
            {
                positions = (Coordinate[])boxes.Clone();
                positions[boxIndex] = boxPosition;
            }
            return new BoardState(this, player, positions);
        }
        public LevelData ToLevelData()
        {
            var result = root.DeepCopy();
            foreach (var entity in result.entities)
            {
                var position = entity.type == EntityType.Player ? Player : boxes[Array.IndexOf(boxIds, entity.id)];
                entity.x = position.x;
                entity.y = position.y;
            }
            return result;
        }
    }
}
