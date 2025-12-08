namespace LoveLetter.App.Configuration;

public static class LoveContent
{
    public static LoveConfig Value { get; } = new LoveConfig
    {
        Hero = new HeroSection
        {
            Title = "SSIO & Nutten",
            Subtitle = "Unsere Beispielgeschichte",
            Intro = "Dies ist ein Beispielinhalt fuer die Demo-Seite.",
            Cta = "Liebesbrief anzeigen",
            CtaAfter = "Weiterlesen",
            FeaturedPhoto = new FeaturedPhoto
            {
                Src = "images/roses.jpg",
                Caption = "Beispielbild"
            }
        },
        Relationship = new RelationshipSection
        {
            StartDate = new DateOnly(2024, 2, 2),
            Heading = "Wie alles begann",
            Subheading = "Eine kleine Demo-Geschichte mit SSIO als Insider.",
            FutureTitle = "Naechstes Kapitel",
            FutureText = "Noch viele gemeinsame Schritte stehen an.",
            ShowWheel = true,
            WheelItems =
            [
                "Erstes Treffen",
                "Festival mit SSIO",
                "Roadtrip",
                "Pizzaabend"
            ]
        },
        LoveLetter = new LoveLetterSection
        {
            Heading = "Ein paar Worte",
            Paragraphs =
            [
                "Dies ist ein Beispielbrief, damit du siehst, wie der Inhalt spaeter aussieht.",
                "In unserer Demo dreht sich vieles um gemeinsame Momente und um Musik von SSIO.",
                "Passe alles im Config-Bereich an, damit deine eigene Story entsteht."
            ]
        },
        Gate = new GateSection
        {
            Title = "Nur fuer uns",
            Subtitle = "Beantworte die Demo-Fragen.",
            ErrorMessage = "Noch nicht korrekt. Versuch es erneut.",
            Questions =
            [
                new GateQuestion
                {
                    Prompt = "Wer der King of Rap",
                    Type = GateQuestionType.MultipleChoice,
                    Choices = ["SSIO", "Shirin", "Apache"],
                    AnswerIndex = 0
                },
                new GateQuestion
                {
                    Prompt = "Wer guckt nur bei Sex relaxed?",
                    Type = GateQuestionType.Text,
                    AnswerText = "SSIO",
                    IsCaseSensitive = false
                }
            ]
        },
        Memories =
        [
            new MemoryEntry
            {
                Title = "Erstes Konzert",
                Description = "SSIO live im Beispiel-Club.",
                Date = "Februar 2024",
                Icon = "*"
            },
            new MemoryEntry
            {
                Title = "Pizzaabend",
                Description = "Demo-Soundtrack und viel Lachen.",
                Date = "Maerz 2024",
                Icon = "*"
            },
            new MemoryEntry
            {
                Title = "Sonnenuntergang",
                Description = "Beispielspaziergang am Fluss.",
                Date = "April 2024",
                Icon = "*"
            }
        ],
        MemoriesVisible = true,
        Gallery =
        [],
        Highlights =
        [
            new HighlightItem { Icon = "*", Title = "Lieblingssongs", Description = "Gemeinsam SSIO hoeren und lachen." },
            new HighlightItem { Icon = "*", Title = "Inside Jokes", Description = "Kleine Insider, die nur wir verstehen." },
            new HighlightItem { Icon = "*", Title = "Gemeinsame Ziele", Description = "Ideen fuer Reisen, Konzerte und mehr." }
        ],
        BucketList = new BucketListSection
        {
            Eyebrow = "Demo Todos",
            Heading = "Was wir noch erleben wollen",
            Subheading = "Passe die Liste in der App an.",
            Items = []
        },
        Songs = new SongsSection
        {
            Eyebrow = "Playlist",
            Heading = "Beispiel-Songs",
            Subheading = "Diese Liste kannst du im Config anpassen.",
            Items =
            [
                new SongItem { Url = "https://open.spotify.com/track/ssio-demo-1", Artist = "SSIO" },
                new SongItem { Url = "https://open.spotify.com/track/ssio-demo-2", Artist = "SSIO" },
                new SongItem { Url = "https://open.spotify.com/track/demo-song", Artist = "Sample Artist" }
            ]
        }
    };
}
