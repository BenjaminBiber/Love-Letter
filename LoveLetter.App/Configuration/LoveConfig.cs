namespace LoveLetter.App.Configuration;

public sealed record LoveConfig
{
    public required HeroSection Hero { get; init; }
    public required RelationshipSection Relationship { get; init; }
    public required LoveLetterSection LoveLetter { get; init; }
    public required GateSection Gate { get; init; }
    public required IReadOnlyList<MemoryEntry> Memories { get; init; }
    public bool MemoriesVisible { get; init; }
    public bool TravelVisible { get; init; }
    public required IReadOnlyList<GalleryItem> Gallery { get; init; }
    public required IReadOnlyList<HighlightItem> Highlights { get; init; }
    public required BucketListSection BucketList { get; init; }
    public required SongsSection Songs { get; init; }
}

public sealed record HeroSection
{
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public required string Intro { get; init; }
    public required string Cta { get; init; }
    public required string CtaAfter { get; init; }
    public required FeaturedPhoto FeaturedPhoto { get; init; }
}

public sealed record FeaturedPhoto
{
    public required string Src { get; init; }
    public string? Caption { get; init; }
}

public sealed record RelationshipSection
{
    public required DateOnly StartDate { get; init; }
    public string? Heading { get; init; }
    public string? Subheading { get; init; }
    public string? FutureTitle { get; init; }
    public string? FutureText { get; init; }
    public bool ShowWheel { get; init; }
    public IReadOnlyList<string>? WheelItems { get; init; }
}

public sealed record LoveLetterSection
{
    public required string Heading { get; init; }
    public required IReadOnlyList<string> Paragraphs { get; init; }
}

public sealed record GateSection
{
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string ErrorMessage { get; init; }
    public required IReadOnlyList<GateQuestion> Questions { get; init; }
}

public enum GateQuestionType
{
    MultipleChoice,
    Text
}

public sealed record GateQuestion
{
    public required string Prompt { get; init; }
    public GateQuestionType Type { get; init; } = GateQuestionType.MultipleChoice;
    public IReadOnlyList<string>? Choices { get; init; }
    public int? AnswerIndex { get; init; }
    public string? AnswerText { get; init; }
    public bool IsCaseSensitive { get; init; }
}

public sealed record MemoryEntry
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Date { get; init; }
    public required string Icon { get; init; }
}

public sealed record GalleryItem
{
    public required string Src { get; init; }
    public string? Caption { get; init; }
}

public sealed record HighlightItem
{
    public required string Icon { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
}

public sealed record BucketListSection
{
    public required string Eyebrow { get; init; }
    public required string Heading { get; init; }
    public required string Subheading { get; init; }
    public required IReadOnlyList<BucketListItem> Items { get; init; }
}

public sealed record BucketListItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Meta { get; init; }
    public string? Description { get; init; }
    public bool Completed { get; init; }
    public required IReadOnlyList<BucketListMedia> Media { get; init; }
}

public sealed record BucketListMedia
{
    public string? Type { get; init; }
    public required string Src { get; init; }
    public string? Caption { get; init; }
    public string? Alt { get; init; }
    public string? Format { get; init; }
}

public sealed record SongsSection
{
    public required string Eyebrow { get; init; }
    public required string Heading { get; init; }
    public required string Subheading { get; init; }
    public required IReadOnlyList<SongItem> Items { get; init; }
}

public sealed record SongItem
{
    public required string Url { get; init; }
    public required string Artist { get; init; }
}
