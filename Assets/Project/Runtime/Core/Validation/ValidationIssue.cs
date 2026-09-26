using System.Collections.Generic;
using Sokoban.Core.Data;
namespace Sokoban.Core.Validation
{
    public enum IssueSeverity { Warning, Error }
    public enum IssueLocationAction { None, SelectLevel, SelectCell, SelectObject }
    public sealed class ValidationIssue
    {
        public string Code { get; } public IssueSeverity Severity { get; } public string Message { get; }
        public string LevelId { get; } public string ObjectId { get; } public Coordinate? Position { get; }
        public IssueLocationAction LocationAction { get; }
        public ValidationIssue(string code, string message, string levelId = null, string objectId = null, Coordinate? position = null, IssueSeverity severity = IssueSeverity.Error)
        { Code = code; Message = message; LevelId = levelId; ObjectId = objectId; Position = position; Severity = severity;
          LocationAction = objectId != null ? IssueLocationAction.SelectObject : position.HasValue ? IssueLocationAction.SelectCell : levelId != null ? IssueLocationAction.SelectLevel : IssueLocationAction.None; }
    }
    public sealed class ValidationReport
    {
        private readonly List<ValidationIssue> issues = new List<ValidationIssue>();
        public IReadOnlyList<ValidationIssue> Issues => issues;
        public bool IsValid => !issues.Exists(i => i.Severity == IssueSeverity.Error);
        public void Add(ValidationIssue issue) => issues.Add(issue);
        public void AddRange(ValidationReport report) => issues.AddRange(report.issues);
    }
}
