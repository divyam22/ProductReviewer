namespace SentimentAnalysis.Models;

public class Review
{
    public string Source { get; set; } = "";
    public string Author { get; set; } = "";
    public string Text { get; set; } = "";
    public string? Rating { get; set; }
    public string? Date { get; set; }
    public string? Url { get; set; }
    public SentimentLabel Sentiment { get; set; } = SentimentLabel.Neutral;
    public double SentimentScore { get; set; }
}

public enum SentimentLabel
{
    Positive,
    Negative,
    Neutral
}

public class ProductAnalysisResult
{
    public string ProductQuery { get; set; } = "";
    public double OverallScore { get; set; }
    public string OverallRating { get; set; } = "";
    public int TotalReviews { get; set; }
    public int PositiveCount { get; set; }
    public int NegativeCount { get; set; }
    public int NeutralCount { get; set; }
    public double PositivePercent { get; set; }
    public double NegativePercent { get; set; }
    public double NeutralPercent { get; set; }
    public List<SourceSummary> SourceSummaries { get; set; } = new();
    public List<Review> Reviews { get; set; } = new();
    public List<string> KeyThemes { get; set; } = new();
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

public class SourceSummary
{
    public string Source { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Color { get; set; } = "";
    public int ReviewCount { get; set; }
    public double AverageScore { get; set; }
    public string Sentiment { get; set; } = "";
}
