namespace PhigrosArchive.Abstractions
{
    public enum IssueSeverity
    {
        Info,
        Warning,
        Error
    }

    public enum IssueType
    {
        GameKeyTooSmall,
        RecordNotInTSV,
        RecordHasATButNoAT,
        InvalidScore,
        InvalidAccuracy,
        ChallengeTypeTooHigh,
        ChallengeRankTooHigh,
        ChallengeRankDifferent,
        AvatarDifferent,
        SummaryRankingScoreInvalid,
        DifficultyTSVNotLoaded,
        InfoTSVNotLoaded,
        DifficultyTSVNoErrorDetect,
        // Extendable
    }

    public class SaveDataIssue
    {
        public IssueSeverity Severity { get; set; }
        public IssueType Type { get; set; }
        public string? Message { get; set; }
        public object? Data { get; set; }
        public string? Path { get; set; }

        public SaveDataIssue(IssueSeverity severity, IssueType type, string? message = null, object? data = null, string? path = null)
        {
            Severity = severity;
            Type = type;
            Message = message;
            Data = data;
            Path = path;
        }
    }
}
