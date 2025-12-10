# Love-Letter
Persoenliche Love-Letter-Website auf .NET 9 / Blazor Server. Sie bringt ein Quiz-Gate, Hero mit eigenem Bild, Liebesbrief-Reveal, Galerie, Bucket List und einen Songs-Bereich. Alle Inhalte lassen sich ueber Umgebungsvariablen oder die Config-Seite im Browser anpassen.

## Features
- Gate mit Multiple-Choice/Text-Fragen bevor die Seite freigeschaltet wird.
- Hero + Liebesbrief: CTA deckt den Brief auf, Hero-Bild kann direkt im Browser hochgeladen werden.
- Galerie & Lightbox: Favoritenlimit konfigurierbar, Uploads landen in `wwwroot/uploads`.
- Erinnerungen & Beziehung: Timeline mit Startdatum, optional Wheel-of-Fortune fuer gemeinsame Plaene.
- Bucket List: Punkte erstellen, abschliessen (Foto/Video, 50 MB Limit), Medien in Galerie uebernehmen, geschuetzt durch ein Master-Passwort.
- Songs & Spotify: Statische Songliste oder kompletter Playlist-Import via `LOVE_SPOTIFY_*`, Metadaten werden aus der Spotify API geladen.
- Config-UI: Unter `/config` als Self-Service Editor, generiert `love.env` und speichert das Hero-Bild.

## Lokal starten
- Voraussetzungen: .NET 9 SDK, keine weiteren Dienste notwendig (SQLite wird automatisch angelegt).
- Restore & Start:
  - `dotnet restore`
  - `dotnet run --project LoveLetter.App/LoveLetter.App.csproj --urls http://localhost:8080`
- Datenhaltung: SQLite unter `LoveLetter.App/App_Data/loveletter.db` (wird beim Start migriert/seeded). Uploads und Thumbnails liegen unter `LoveLetter.App/wwwroot/uploads`.
- Admin-Tools: Konfiguration unter `/config` (Hero-Upload, Inhalte bearbeiten, `.env`-Download). Bucket-List-Admin-Aktionen verlangen das Master-Passwort.

## Umgebungsvariablen (Auszug)
Fehlende Werte fallen auf Defaults aus `LoveContent` zurueck. Listen muessen als JSON-Arrays gesetzt werden.
- Hero: `LOVE_HERO_TITLE`, `LOVE_HERO_SUBTITLE`, `LOVE_HERO_INTRO`, `LOVE_HERO_CTA`, `LOVE_HERO_CTA_AFTER`.
- Beziehung/Timeline: `LOVE_REL_START_DATE` (yyyy-MM-dd), `LOVE_REL_HEADING`, `LOVE_REL_SUBHEADING`, `LOVE_REL_FUTURE_TITLE`, `LOVE_REL_FUTURE_TEXT`, `LOVE_REL_SHOW_WHEEL` (bool), `LOVE_REL_WHEEL_ITEMS` (JSON array of strings).
- Gate: `LOVE_GATE_TITLE`, `LOVE_GATE_SUBTITLE`, `LOVE_GATE_ERROR_MESSAGE`, `LOVE_GATE_QUESTIONS` (JSON array aus `GateQuestion` mit `prompt`, `type` = "MultipleChoice"|"Text", `choices`, `answerIndex` oder `answerText`, `isCaseSensitive`).
- Liebesbrief: `LOVE_LETTER_HEADING`, `LOVE_LETTER_PARAGRAPHS` (JSON array of strings).
- Erinnerungen & Highlights: `LOVE_MEMORIES_VISIBLE` (bool), `LOVE_MEMORIES` (JSON array aus `title`, `description`, `date`, `icon`), `LOVE_HIGHLIGHT_ITEMS`, `LOVE_GALLERY_FAVORITE_LIMIT` (int).
- Bucket List: `LOVE_BUCKET_EYEBROW`, `LOVE_BUCKET_HEADING`, `LOVE_BUCKET_SUBHEADING`, `BUCKETLIST_MASTER_PASSWORD` (schuetzt Upload/Loesch-APIs, Default: LoveLetterMaster).
- Songs & Spotify: `LOVE_SONGS_EYEBROW`, `LOVE_SONGS_HEADING`, `LOVE_SONGS_SUBHEADING`, `LOVE_SONGS_ITEMS` (JSON array `{ "url": "...", "artist": "..." }`), `LOVE_SPOTIFY_CLIENT_ID`, `LOVE_SPOTIFY_CLIENT_SECRET`, `LOVE_SPOTIFY_PLAYLIST_ID` fuer automatischen Playlist-Import/Metadaten.
- Allgemein: `ASPNETCORE_URLS` fuer das Binding/den Port.

Minimalbeispiel fuer eine `love.env`:
```
LOVE_HERO_TITLE="Unsere Story"
LOVE_REL_START_DATE=2024-02-02
LOVE_LETTER_PARAGRAPHS=["Absatz 1","Absatz 2"]
LOVE_GATE_QUESTIONS=[{"prompt":"Wann war unser erstes Date?","type":"Text","answerText":"2024"}]
LOVE_SONGS_ITEMS=[{"url":"https://open.spotify.com/track/xyz","artist":"Artist"}]
BUCKETLIST_MASTER_PASSWORD=super-secret
```

## Docker Compose
Beispiel basierend auf `docker-compose.example.yml`:
```yaml
version: "3.9"

services:
  loveletter:
    image: benjaminbiber/love-letter:latest
    container_name: loveletter
    restart: unless-stopped
    ports:
      - "8080:8080"
    env_file:
      - ./love.env
    environment:
      ASPNETCORE_URLS: http://+:8080
      BUCKETLIST_MASTER_PASSWORD: super-secret-password
      # Optional: LOVE_SPOTIFY_CLIENT_ID=...
      # Optional: LOVE_SPOTIFY_CLIENT_SECRET=...
      # Optional: LOVE_SPOTIFY_PLAYLIST_ID=...
    volumes:
      - loveletter-data:/app/App_Data
      - loveletter-uploads:/app/wwwroot/uploads

volumes:
  loveletter-data:
  loveletter-uploads:
```
Starte mit `docker compose up -d`. `loveletter-data` enthaelt die SQLite-DB, `loveletter-uploads` speichert Hero-Bilder, Galerie-Uploads und Bucket-Medien.
