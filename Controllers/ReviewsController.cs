using Microsoft.AspNetCore.Mvc;
using SentimentAnalysis.Services;
using SentimentAnalysis.Models;

namespace SentimentAnalysis.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewsController : ControllerBase
{
    private readonly ReviewAggregatorService _aggregator;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(ReviewAggregatorService aggregator, ILogger<ReviewsController> logger)
    {
        _aggregator = aggregator;
        _logger = logger;
    }

    /// <summary>
    /// Analyze sentiment for a given product query
    /// </summary>
    [HttpGet("analyze")]
    public async Task<IActionResult> Analyze([FromQuery] string product, [FromQuery] string? sources = null)
    {
        if (string.IsNullOrWhiteSpace(product))
            return BadRequest(new { error = "Product query cannot be empty." });

        if (product.Length > 200)
            return BadRequest(new { error = "Product query too long (max 200 chars)." });

        try
        {
            _logger.LogInformation("Analyze request for: {Product}", product);
            var result = await _aggregator.AnalyzeProductAsync(product.Trim());
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing product: {Product}", product);
            return StatusCode(500, new { error = "An error occurred while fetching reviews. Please try again." });
        }
    }

    /// <summary>
    /// Quick demo with sample data (no network calls)
    /// </summary>
    [HttpGet("demo")]
    public IActionResult Demo()
    {
        var result = new ProductAnalysisResult
        {
            ProductQuery = "iPhone 15 Pro",
            OverallScore = 4.2,
            OverallRating = "Very Good",
            TotalReviews = 47,
            PositiveCount = 31,
            NegativeCount = 9,
            NeutralCount = 7,
            PositivePercent = 66.0,
            NegativePercent = 19.1,
            NeutralPercent = 14.9,
            KeyThemes = ["Camera Quality", "Performance", "Battery Life", "Build Quality", "Price/Value", "Design"],
            AnalyzedAt = DateTime.UtcNow,
            SourceSummaries =
            [
                new() { Source = "Reddit", Icon = "reddit", Color = "#FF4500", ReviewCount = 23, AverageScore = 0.74, Sentiment = "Positive" },
                new() { Source = "Amazon", Icon = "amazon", Color = "#FF9900", ReviewCount = 12, AverageScore = 0.71, Sentiment = "Positive" },
                new() { Source = "Google Play Store", Icon = "play_store", Color = "#34A853", ReviewCount = 7, AverageScore = 0.58, Sentiment = "Neutral" },
                new() { Source = "Trustpilot", Icon = "star", Color = "#00B67A", ReviewCount = 5, AverageScore = 0.42, Sentiment = "Negative" },
            ],
            Reviews =
            [
                new() { Source = "Reddit", Author = "u/TechEnthusiast42", Text = "The iPhone 15 Pro camera is absolutely incredible. The computational photography improvements over the 14 Pro are noticeable in every shot.", Sentiment = SentimentLabel.Positive, SentimentScore = 0.91, Rating = "👍 234", Date = "Apr 12, 2024" },
                new() { Source = "Amazon", Author = "JohnD", Text = "Terrible battery life. Dies by 3pm with moderate use. Returned it after 2 weeks. Very disappointed with Apple on this one.", Sentiment = SentimentLabel.Negative, SentimentScore = 0.08, Rating = "⭐ 1/5", Date = "Mar 28, 2024" },
                new() { Source = "Reddit", Author = "u/GadgetReview", Text = "The titanium build feels premium and the USB-C transition was long overdue. Performance is blazing fast, no complaints.", Sentiment = SentimentLabel.Positive, SentimentScore = 0.85, Rating = "👍 189", Date = "Apr 01, 2024" },
                new() { Source = "Google Play Store", Author = "Sarah M.", Text = "Decent phone but way overpriced. You get a good experience but similar Android phones offer more for less money.", Sentiment = SentimentLabel.Neutral, SentimentScore = 0.48, Rating = "⭐ 3/5", Date = "Mar 15, 2024" },
                new() { Source = "Trustpilot", Author = "Mike R.", Text = "Apple support is absolutely horrible. My phone developed a camera issue and getting it fixed under warranty was a nightmare.", Sentiment = SentimentLabel.Negative, SentimentScore = 0.11, Rating = "⭐ 1/5", Date = "Feb 20, 2024" },
            ]
        };
        return Ok(result);
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
}
