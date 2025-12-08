using LoveLetter.App.Components;
using LoveLetter.App.Configuration;
using LoveLetter.App.Data;
using LoveLetter.App.Services;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;
using BucketConfigMedia = LoveLetter.App.Configuration.BucketListMedia;
using BucketListMediaEntity = LoveLetter.App.Data.BucketListMedia;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var loveConfig = LoveConfigLoader.Load(builder.Configuration);
builder.Services.AddSingleton(loveConfig);
builder.Services.AddScoped<ProtectedSessionStorage>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<ISpotifyMetadataService, SpotifyMetadataService>();
builder.Services.AddSingleton<ISpotifyPlaylistService, SpotifyPlaylistService>();
builder.Services.AddSingleton<IHeroImageService, HeroImageService>();

builder.Services.Configure<BucketListSecurityOptions>(options =>
{
    options.MasterPassword = builder.Configuration["BUCKETLIST_MASTER_PASSWORD"]
        ?? builder.Configuration["BucketList__MasterPassword"]
        ?? builder.Configuration["BucketList:MasterPassword"]
        ?? "LoveLetterMaster";
});

var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDirectory);

var dbFilePath = Path.Combine(dataDirectory, "loveletter.db");
builder.Services.AddDbContextFactory<LoveLetterDbContext>(options =>
    options.UseSqlite($"Data Source={dbFilePath}")
           .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()));
builder.Services.AddSingleton<IBucketListService, BucketListService>();
builder.Services.AddSingleton<IGalleryService, GalleryService>();
builder.Services.AddSingleton<IImageThumbnailService, ImageThumbnailService>();
builder.Services.AddSingleton<IBucketThumbnailQueue, BucketThumbnailQueue>();
builder.Services.AddHostedService<BucketThumbnailBackgroundService>();
builder.Services.AddSingleton<ThumbnailBackfillService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

using (var scope = app.Services.CreateScope())
{
    var scopedServices = scope.ServiceProvider;
    var dbFactory = scopedServices.GetRequiredService<IDbContextFactory<LoveLetterDbContext>>();
    using var db = dbFactory.CreateDbContext();
    var runtimeLoveConfig = scopedServices.GetRequiredService<LoveConfig>();
    db.Database.Migrate();

    if (!db.BucketListEntries.Any())
    {
        var seedEntries = runtimeLoveConfig.BucketList.Items.Select(item => new BucketListEntry
        {
            Id = Guid.NewGuid(),
            Title = item.Title,
            Description = item.Description ?? item.Meta,
            RequiresPhoto = false,
            Completed = item.Completed,
            CreatedAt = DateTime.UtcNow,
            Media = (item.Media ?? Array.Empty<BucketConfigMedia>())
                .Select(media =>
                {
                    var normalizedPath = media.Src.StartsWith('/') ? media.Src.TrimStart('/') : media.Src;
                    return new BucketListMediaEntity
                    {
                        Id = Guid.NewGuid(),
                        FilePath = normalizedPath,
                        ThumbnailPath = normalizedPath,
                        OriginalFileName = media.Caption,
                        IsVideo = string.Equals(media.Type, "video", StringComparison.OrdinalIgnoreCase),
                        IsInGallery = false,
                        CreatedAt = DateTime.UtcNow
                    };
                })
                .ToList()
        }).ToList();

        db.BucketListEntries.AddRange(seedEntries);
        db.SaveChanges();
    }

    if (!db.GalleryPhotos.Any())
    {
        var galleryEntries = runtimeLoveConfig.Gallery.Select(item => new GalleryPhoto
        {
            Id = Guid.NewGuid(),
            Caption = item.Caption,
            FilePath = item.Src.StartsWith('/') ? item.Src.TrimStart('/') : item.Src,
            ThumbnailPath = item.Src.StartsWith('/') ? item.Src.TrimStart('/') : item.Src,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        db.GalleryPhotos.AddRange(galleryEntries);
        db.SaveChanges();
    }

    var backfill = scopedServices.GetRequiredService<ThumbnailBackfillService>();
    await backfill.RunAsync();
}

app.Run();
