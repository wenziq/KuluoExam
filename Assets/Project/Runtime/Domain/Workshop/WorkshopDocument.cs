using System;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Validation;
namespace Sokoban.Domain.Workshop
{
    /// <summary>Owns an isolated draft. All callbacks receive disposable copies, never retained mutable data.</summary>
    public sealed class WorkshopDocument
    {
        private PackData current;
        private PaintStroke stroke;
        private bool editing;
        private readonly EditHistory history;
        private string savedHash;
        public WorkshopDocument(PackData pack, int maxHistory = 100, long historyBudget = 64L * 1024 * 1024)
        {
            RequireValid(pack); current = pack.DeepCopy();
            history = new EditHistory(maxHistory, historyBudget);
            SelectedLevelId = current.levelOrder.Count == 0 ? null : current.levelOrder[0];
            savedHash = DocumentHash.Compute(current);
        }
        public PackData Snapshot() => (stroke == null ? current : stroke.Working).DeepCopy();
        public string SelectedLevelId { get; private set; }
        public string CurrentHash => DocumentHash.Compute(stroke == null ? current : stroke.Working);
        /// <summary>Null for a new document that has never been manually saved.</summary>
        public string SavedHash => savedHash;
        public bool IsDirty => CurrentHash != savedHash;
        public int UndoCount => history.UndoCount;
        public int RedoCount => history.RedoCount;
        public long HistoryEstimatedBytes => history.EstimatedBytes;
        public string LastOperationDescription { get; private set; }
        public bool HasActiveStroke => stroke != null;
        public void SelectLevel(string id)
        {
            RequireIdle();
            if (id == null ? current.levels.Count != 0 : !current.levelOrder.Contains(id)) throw new ArgumentException("关卡不存在。", nameof(id));
            SelectedLevelId = id;
        }
        /// <summary>Use when creating a new draft or independent copy, before its first successful manual save.</summary>
        public void MarkUnsaved()
        {
            if (editing) throw new InvalidOperationException("编辑回调期间不能修改保存点。");
            savedHash = null;
        }
        /// <summary>Call only after successful manual persistence, using the hash captured with that saved snapshot.</summary>
        public void MarkSaved(string committedHash)
        {
            if (editing) throw new InvalidOperationException("编辑回调期间不能修改保存点。");
            if (!PackValidator.IsFingerprint(committedHash)) throw new ArgumentException("保存点哈希无效。", nameof(committedHash));
            savedHash = committedHash;
        }
        public bool Edit(string description, string levelId, Action<PackData> change)
        {
            RequireIdle(); if (change == null) throw new ArgumentNullException(nameof(change));
            var candidate = current.DeepCopy(); editing = true;
            try { change(candidate); return Accept(description, levelId, candidate); }
            finally { editing = false; }
        }
        private bool Accept(string description, string levelId, PackData candidate)
        {
            RequireValid(candidate);
            var owned = candidate.DeepCopy();
            if (DocumentHash.Compute(current) == DocumentHash.Compute(owned)) { current = owned; return false; }
            string beforeSelection = current.levelOrder.Contains(levelId ?? "") ? levelId : SelectedLevelId;
            string afterSelection;
            if (owned.levelOrder.Contains(levelId ?? "")) afterSelection = levelId;
            else if (owned.levelOrder.Contains(SelectedLevelId ?? "")) afterSelection = SelectedLevelId;
            else if (owned.levelOrder.Count > 0) afterSelection = owned.levelOrder[0];
            else afterSelection = null;
            history.Push(current, owned, beforeSelection, afterSelection, description ?? "编辑");
            current = owned; SelectedLevelId = afterSelection; LastOperationDescription = description ?? "编辑"; return true;
        }
        public bool Undo()
        {
            RequireIdle(); var entry = history.Undo(); if (entry == null) return false;
            current = entry.Before; SelectedLevelId = entry.BeforeSelection; LastOperationDescription = entry.Description; return true;
        }
        public bool Redo()
        {
            RequireIdle(); var entry = history.Redo(); if (entry == null) return false;
            current = entry.After; SelectedLevelId = entry.AfterSelection; LastOperationDescription = entry.Description; return true;
        }
        public PaintPreview PreviewPaint(PaintTool tool, int x, int y)
        {
            var pack = stroke == null ? current : stroke.Working;
            var level = pack.levels.Find(item => item.levelId == SelectedLevelId);
            return PaintStroke.Assess(level,tool,x,y);
        }
        public PaintStroke BeginStroke(PaintTool tool)
        {
            RequireIdle(); if (SelectedLevelId == null) throw new InvalidOperationException("请先新建关卡。");
            if (!Enum.IsDefined(typeof(PaintTool), tool)) throw new ArgumentException("画笔无效。", nameof(tool));
            stroke = new PaintStroke(this, current.DeepCopy(), SelectedLevelId, tool); return stroke;
        }
        internal bool EndStroke(PaintStroke owner, bool commit)
        {
            if (!ReferenceEquals(stroke, owner)) return false;
            stroke = null;
            return commit && Accept("绘制：" + owner.ToolLabel, owner.LevelId, owner.Working);
        }
        private void RequireIdle() { if (stroke != null || editing) throw new InvalidOperationException("请先结束当前编辑操作。"); }
        private static void RequireValid(PackData pack)
        {
            if (!PackValidator.Validate(pack).IsValid) throw new ArgumentException("编辑结果超出安全草稿的数据范围。", nameof(pack));
            // Hashing validates Unicode normalization before publication as well.
            DocumentHash.Compute(pack);
        }
    }
}
