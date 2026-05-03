using SentimentAnalysis.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddControllers()
    .AddNewtonsoftJson(opts =>
    {
        opts.SerializerSettings.ContractResolver =
            new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
        opts.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
    });

// Named HTTP clients with appropriate timeouts
builder.Services.AddHttpClient<RedditService>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(15);
    c.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddHttpClient<PlayStoreService>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(20);
});

builder.Services.AddHttpClient<AmazonService>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(20);
});

builder.Services.AddHttpClient<WebReviewService>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(20);
});

// Register services
builder.Services.AddSingleton<SentimentAnalyzer>();
builder.Services.AddScoped<ReviewAggregatorService>();

// CORS — allow any origin for local development
builder.Services.AddCors(opts =>
{
    opts.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddResponseCompression(opts => opts.EnableForHttps = true);

var app = builder.Build();

// ── Middleware ─────────────────────────────────────────────────────────────────

app.UseCors();
app.UseResponseCompression();
app.UseStaticFiles();
app.MapControllers();

// Serve index.html for the root path
app.MapFallbackToFile("index.html");

// ── Startup log ───────────────────────────────────────────────────────────────

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("✅ SentimentAnalysis API is running.");
logger.LogInformation("📖 Open http://localhost:5000 in your browser.");

app.Run();
