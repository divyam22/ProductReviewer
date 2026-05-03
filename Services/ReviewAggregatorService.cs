using SentimentAnalysis.Models;

namespace SentimentAnalysis.Services;

public class ReviewAggregatorService
{
    private readonly RedditService _reddit;
    private readonly PlayStoreService _playStore;
    private readonly AmazonService _amazon;
    private readonly WebReviewService _webReviews;
    private readonly SentimentAnalyzer _sentimentAnalyzer;
    private readonly ILogger<ReviewAggregatorService> _logger;

    private static readonly Dictionary<string, (string Icon, string Color)> _sourceStyles = new()
    {
        ["Reddit"]           = ("reddit", "#FF4500"),
        ["Google Play Store"]= ("play_store", "#34A853"),
        ["Amazon"]           = ("amazon", "#FF9900"),
        ["Trustpilot"]       = ("star", "#00B67A"),
        ["G2"]               = ("g2", "#FF492C"),
        ["Capterra"]         = ("business_center", "#002F56"),
        ["Product Hunt"]     = ("rocket_launch", "#DA552F"),
        ["Hacker News"]      = ("forum", "#FF6600"),
    };

    public ReviewAggregatorService(
        RedditService reddit,
        PlayStoreService playStore,
        AmazonService amazon,
        WebReviewService webReviews,
        SentimentAnalyzer sentimentAnalyzer,
        ILogger<ReviewAggregatorService> logger)
    {
        _reddit = reddit;
        _playStore = playStore;
        _amazon = amazon;
        _webReviews = webReviews;
        _sentimentAnalyzer = sentimentAnalyzer;
        _logger = logger;
    }

    public async Task<ProductAnalysisResult> AnalyzeProductAsync(string productQuery)
    {
        _logger.LogInformation("Starting analysis for: {Query}", productQuery);

        // Fetch reviews from all sources in parallel
        var tasks = new[]
        {
            _reddit.FetchReviewsAsync(productQuery, 25),
            _playStore.FetchReviewsAsync(productQuery, 15),
            _amazon.FetchReviewsAsync(productQuery, 15),
            _webReviews.FetchTrustpilotReviewsAsync(productQuery, 10),
            _webReviews.FetchG2ReviewsAsync(productQuery, 10),
            _webReviews.FetchCapterraReviewsAsync(productQuery, 10),
            _webReviews.FetchProductHuntReviewsAsync(productQuery, 10),
            _webReviews.FetchHackerNewsReviewsAsync(productQuery, 10)
        };

        var results = await Task.WhenAll(tasks);
        
        // Combine results and eliminate duplicate reviews by text content
        var allReviews = results.SelectMany(r => r)
            .GroupBy(r => r.Text.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        _logger.LogInformation("Fetched {Count} total reviews", allReviews.Count);

        // If no reviews found anywhere, create informational message
        if (allReviews.Count == 0)
        {
            return new ProductAnalysisResult
            {
                ProductQuery = productQuery,
                OverallScore = 0,
                OverallRating = "No Data",
                TotalReviews = 0,
                Reviews = [],
                KeyThemes = ["No reviews found. Try a more specific product name."]
            };
        }

        // Run sentiment analysis on all reviews
        foreach (var review in allReviews)
        {
            var result = _sentimentAnalyzer.Analyze(review.Text);
            review.Sentiment = result.Label;
            review.SentimentScore = result.Score;
        }

        // Build source summaries (including sources with 0 reviews)
        var sourceSummaries = _sourceStyles
            .Select(s =>
            {
                var sourceName = s.Key;
                var style = s.Value;
                var sourceReviews = allReviews
                    .Where(r => NormalizeSource(r.Source).Equals(sourceName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                double avgScore = 0.5; // Default neutral for 0 reviews
                if (sourceReviews.Count > 0)
                {
                    avgScore = sourceReviews.Average(r => r.SentimentScore);
                }

                return new SourceSummary
                {
                    Source = sourceName,
                    Icon = style.Icon,
                    Color = style.Color,
                    ReviewCount = sourceReviews.Count,
                    AverageScore = Math.Round(avgScore, 3),
                    Sentiment = sourceReviews.Count == 0 ? "No Data" : (avgScore >= 0.6 ? "Positive" : avgScore <= 0.4 ? "Negative" : "Neutral")
                };
            })
            .OrderByDescending(s => s.ReviewCount)
            .ToList();

        // Overall score (weighted by count)
        var overallScore = allReviews.Average(r => r.SentimentScore);
        var overallStars = overallScore * 5.0;

        // Count labels
        var positive = allReviews.Count(r => r.Sentiment == SentimentLabel.Positive);
        var negative = allReviews.Count(r => r.Sentiment == SentimentLabel.Negative);
        var neutral  = allReviews.Count(r => r.Sentiment == SentimentLabel.Neutral);
        var total    = allReviews.Count;

        // Extract key themes from most common words in reviews
        var themes = ExtractKeyThemes(allReviews);

        // Sort: most positive and most negative first (interesting reviews)
        var sortedReviews = allReviews
            .OrderByDescending(r => Math.Abs(r.SentimentScore - 0.5))
            .ToList();

        return new ProductAnalysisResult
        {
            ProductQuery = productQuery,
            OverallScore = Math.Round(overallStars, 2),
            OverallRating = GetRatingLabel(overallScore),
            TotalReviews = total,
            PositiveCount = positive,
            NegativeCount = negative,
            NeutralCount = neutral,
            PositivePercent = Math.Round((double)positive / total * 100, 1),
            NegativePercent = Math.Round((double)negative / total * 100, 1),
            NeutralPercent  = Math.Round((double)neutral  / total * 100, 1),
            SourceSummaries = sourceSummaries,
            Reviews = sortedReviews,
            KeyThemes = themes,
            AnalyzedAt = DateTime.UtcNow
        };
    }

    private static string NormalizeSource(string source)
    {
        if (source.StartsWith("Reddit")) return "Reddit";
        return source;
    }

    private static (string Icon, string Color) GetSourceStyle(string source)
    {
        foreach (var (key, value) in _sourceStyles)
        {
            if (source.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                return value;
        }
        return ("language", "#6366f1");
    }

    private static string GetRatingLabel(double score) => score switch
    {
        >= 0.8 => "Excellent",
        >= 0.65 => "Very Good",
        >= 0.55 => "Good",
        >= 0.45 => "Mixed",
        >= 0.35 => "Below Average",
        >= 0.2  => "Poor",
        _       => "Very Poor"
    };

    private static List<string> ExtractKeyThemes(List<Review> reviews)
    {
        var positiveThemes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var negativeThemes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for",
            "of", "with", "by", "from", "is", "it", "its", "this", "that", "i",
            "my", "me", "we", "you", "your", "they", "their", "was", "are", "be",
            "been", "has", "have", "had", "not", "no", "so", "as", "just", "very",
            "also", "can", "do", "did", "will", "would", "get", "got", "was", "were",
            "than", "then", "when", "what", "which", "who", "how", "all", "some",
            "if", "up", "out", "about", "into", "there", "after", "before", "more",
            "use", "used", "using", "im", "ive", "dont", "doesnt", "isnt", "its"
        };

        // Themed word groups
        var themeGroups = new Dictionary<string, string[]>
        {
            ["Battery Life"] = ["battery", "charge", "charging", "life", "drain"],
            ["Performance"] = ["fast", "slow", "performance", "speed", "lag", "laggy", "smooth"],
            ["Camera Quality"] = ["camera", "photo", "picture", "image", "video", "lens"],
            ["Display/Screen"] = ["screen", "display", "resolution", "brightness", "oled", "lcd"],
            ["Build Quality"] = ["build", "quality", "durable", "sturdy", "flimsy", "plastic", "metal"],
            ["Price/Value"] = ["price", "cost", "expensive", "cheap", "affordable", "value", "money", "worth"],
            ["Customer Support"] = ["support", "service", "customer", "help", "response", "warranty"],
            ["Software/App"] = ["app", "software", "update", "bug", "crash", "ui", "interface"],
            ["Sound/Audio"] = ["sound", "audio", "speaker", "music", "bass", "noise", "headphone"],
            ["Shipping/Delivery"] = ["shipping", "delivery", "arrived", "package", "box"],
            ["Connectivity"] = ["wifi", "bluetooth", "connectivity", "connect", "network", "internet"],
            ["Design"] = ["design", "look", "style", "color", "sleek", "aesthetic"],
        };

        var allText = string.Join(" ", reviews.Select(r => r.Text)).ToLowerInvariant();
        var themeScores = new Dictionary<string, double>();

        foreach (var (theme, keywords) in themeGroups)
        {
            var count = keywords.Sum(kw => CountOccurrences(allText, kw));
            if (count > 0) themeScores[theme] = count;
        }

        return themeScores
            .OrderByDescending(kv => kv.Value)
            .Take(8)
            .Select(kv => kv.Key)
            .ToList();
    }

    private static int CountOccurrences(string text, string word)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(word, idx, StringComparison.OrdinalIgnoreCase)) != -1)
        {
            count++;
            idx += word.Length;
        }
        return count;
    }
}
