using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace LoveLetter.App.Configuration;

public static class LoveConfigLoader
{
    public static class Keys
    {
        public const string HeroTitle = "LOVE_HERO_TITLE";
        public const string HeroSubtitle = "LOVE_HERO_SUBTITLE";
        public const string HeroIntro = "LOVE_HERO_INTRO";
        public const string HeroCta = "LOVE_HERO_CTA";
        public const string HeroCtaAfter = "LOVE_HERO_CTA_AFTER";

        public const string RelationshipStartDate = "LOVE_REL_START_DATE";
        public const string RelationshipHeading = "LOVE_REL_HEADING";
        public const string RelationshipSubheading = "LOVE_REL_SUBHEADING";
        public const string RelationshipFutureTitle = "LOVE_REL_FUTURE_TITLE";
        public const string RelationshipFutureText = "LOVE_REL_FUTURE_TEXT";
        public const string RelationshipShowWheel = "LOVE_REL_SHOW_WHEEL";
        public const string RelationshipWheelItems = "LOVE_REL_WHEEL_ITEMS";

        public const string LoveLetterHeading = "LOVE_LETTER_HEADING";
        public const string LoveLetterParagraphs = "LOVE_LETTER_PARAGRAPHS";

        public const string GateTitle = "LOVE_GATE_TITLE";
        public const string GateSubtitle = "LOVE_GATE_SUBTITLE";
        public const string GateErrorMessage = "LOVE_GATE_ERROR_MESSAGE";
        public const string GateQuestions = "LOVE_GATE_QUESTIONS";

        public const string MemoriesVisible = "LOVE_MEMORIES_VISIBLE";
        public const string MemoriesItems = "LOVE_MEMORIES";

        public const string HighlightItems = "LOVE_HIGHLIGHT_ITEMS";
        public const string GalleryFavoriteLimit = "LOVE_GALLERY_FAVORITE_LIMIT";
        public const string TravelVisible = "LOVE_TRAVEL_VISIBLE";
        public const string TravelAllowUnmark = "LOVE_TRAVEL_ALLOW_UNMARK";

        public const string BucketEyebrow = "LOVE_BUCKET_EYEBROW";
        public const string BucketHeading = "LOVE_BUCKET_HEADING";
        public const string BucketSubheading = "LOVE_BUCKET_SUBHEADING";

        public const string SongsEyebrow = "LOVE_SONGS_EYEBROW";
        public const string SongsHeading = "LOVE_SONGS_HEADING";
        public const string SongsSubheading = "LOVE_SONGS_SUBHEADING";
        public const string SongsItems = "LOVE_SONGS_ITEMS";

        public const string SpotifyClientId = "LOVE_SPOTIFY_CLIENT_ID";
        public const string SpotifyClientSecret = "LOVE_SPOTIFY_CLIENT_SECRET";
        public const string SpotifyPlaylistId = "LOVE_SPOTIFY_PLAYLIST_ID";
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static LoveConfig Load(IConfiguration configuration)
    {
        var defaults = LoveContent.Value;

        var hero = defaults.Hero with
        {
            Title = GetString(configuration, Keys.HeroTitle, defaults.Hero.Title),
            Subtitle = GetNullableString(configuration, Keys.HeroSubtitle, defaults.Hero.Subtitle),
            Intro = GetString(configuration, Keys.HeroIntro, defaults.Hero.Intro),
            Cta = GetString(configuration, Keys.HeroCta, defaults.Hero.Cta),
            CtaAfter = GetString(configuration, Keys.HeroCtaAfter, defaults.Hero.CtaAfter),
            FeaturedPhoto = defaults.Hero.FeaturedPhoto
        };

        var relationship = defaults.Relationship with
        {
            StartDate = GetDate(configuration, Keys.RelationshipStartDate, defaults.Relationship.StartDate),
            Heading = GetNullableString(configuration, Keys.RelationshipHeading, defaults.Relationship.Heading),
            Subheading = GetNullableString(configuration, Keys.RelationshipSubheading, defaults.Relationship.Subheading),
            FutureTitle = GetNullableString(configuration, Keys.RelationshipFutureTitle, defaults.Relationship.FutureTitle),
            FutureText = GetNullableString(configuration, Keys.RelationshipFutureText, defaults.Relationship.FutureText),
            ShowWheel = GetBool(configuration, Keys.RelationshipShowWheel, defaults.Relationship.ShowWheel),
            WheelItems = GetJsonList(configuration, Keys.RelationshipWheelItems, defaults.Relationship.WheelItems)
        };

        var loveLetter = defaults.LoveLetter with
        {
            Heading = GetString(configuration, Keys.LoveLetterHeading, defaults.LoveLetter.Heading),
            Paragraphs = GetJsonList(configuration, Keys.LoveLetterParagraphs, defaults.LoveLetter.Paragraphs)
        };

        var gate = defaults.Gate with
        {
            Title = GetString(configuration, Keys.GateTitle, defaults.Gate.Title),
            Subtitle = GetString(configuration, Keys.GateSubtitle, defaults.Gate.Subtitle),
            ErrorMessage = GetString(configuration, Keys.GateErrorMessage, defaults.Gate.ErrorMessage),
            Questions = GetJsonList(configuration, Keys.GateQuestions, defaults.Gate.Questions)
        };

        var memories = GetJsonList(configuration, Keys.MemoriesItems, defaults.Memories);
        var highlights = GetJsonList(configuration, Keys.HighlightItems, defaults.Highlights);

        var bucketListItems = NormalizeBucketItems(defaults.BucketList.Items);
        var bucketList = defaults.BucketList with
        {
            Eyebrow = GetString(configuration, Keys.BucketEyebrow, defaults.BucketList.Eyebrow),
            Heading = GetString(configuration, Keys.BucketHeading, defaults.BucketList.Heading),
            Subheading = GetString(configuration, Keys.BucketSubheading, defaults.BucketList.Subheading),
            Items = bucketListItems
        };

        var songs = defaults.Songs with
        {
            Eyebrow = GetString(configuration, Keys.SongsEyebrow, defaults.Songs.Eyebrow),
            Heading = GetString(configuration, Keys.SongsHeading, defaults.Songs.Heading),
            Subheading = GetString(configuration, Keys.SongsSubheading, defaults.Songs.Subheading),
            Items = GetJsonList(configuration, Keys.SongsItems, defaults.Songs.Items)
        };

        return defaults with
        {
            Hero = hero,
            Relationship = relationship,
            LoveLetter = loveLetter,
            Gate = gate,
            Memories = memories,
            MemoriesVisible = GetBool(configuration, Keys.MemoriesVisible, defaults.MemoriesVisible),
            TravelVisible = GetBool(configuration, Keys.TravelVisible, defaults.TravelVisible),
            Gallery = defaults.Gallery,
            Highlights = highlights,
            BucketList = bucketList,
            Songs = songs
        };
    }

    public static bool TryDeserialize<T>(string json, out T? value, out string? error)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            value = default;
            error = "Kein Inhalt";
            return false;
        }

        try
        {
            value = JsonSerializer.Deserialize<T>(json, JsonOptions);
            error = null;
            return value != null;
        }
        catch (Exception ex)
        {
            value = default;
            error = ex.Message;
            return false;
        }
    }

    public static string ToJson<T>(T value, bool indented = true)
    {
        var options = indented ? IndentedJsonOptions : CompactJsonOptions;
        return JsonSerializer.Serialize(value, options);
    }

    private static string GetString(IConfiguration configuration, string key, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string? GetNullableString(IConfiguration configuration, string key, string? fallback)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static bool GetBool(IConfiguration configuration, string key, bool fallback)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static DateOnly GetDate(IConfiguration configuration, string key, DateOnly fallback)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return DateOnly.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static IReadOnlyList<T> GetJsonList<T>(IConfiguration configuration, string key, IReadOnlyList<T>? fallback)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback ?? Array.Empty<T>();
        }

        if (TryDeserialize<IReadOnlyList<T>>(value, out var parsed, out _))
        {
            return parsed ?? fallback ?? Array.Empty<T>();
        }

        return fallback ?? Array.Empty<T>();
    }

    private static IReadOnlyList<BucketListItem> NormalizeBucketItems(IReadOnlyList<BucketListItem> items)
    {
        var list = new List<BucketListItem>();
        foreach (var item in items)
        {
            var id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id;
            list.Add(item with
            {
                Id = id,
                Media = item.Media ?? Array.Empty<BucketListMedia>()
            });
        }

        return list;
    }
}
