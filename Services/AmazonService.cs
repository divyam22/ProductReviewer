using HtmlAgilityPack;
using SentimentAnalysis.Models;

namespace SentimentAnalysis.Services;

/// <summary>
/// Fetches product reviews from Amazon via web scraping.
/// </summary>
public class AmazonService
{
    private readonly HttpClient _http;
    private readonly ILogger<AmazonService> _logger;

    private static readonly string[] _userAgents =
    [
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15",
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
    ];

    public AmazonService(HttpClient http, ILogger<AmazonService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<Review>> FetchReviewsAsync(string productQuery, int maxResults = 15)
    {
        var reviews = new List<Review>();
        try
        {
            var searchUrl = $"https://www.amazon.com/s?k={Uri.EscapeDataString(productQuery)}&i=aps";
            var ua = _userAgents[Random.Shared.Next(_userAgents.Length)];

            var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            request.Headers.Add("User-Agent", ua);
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Amazon search returned {Status}", response.StatusCode);
                return reviews;
            }

            var html = await response.Content.ReadAsStringAsync();
            var asin = ExtractFirstAsin(html);
            if (asin == null) return reviews;

            reviews.AddRange(await FetchProductReviewsAsync(asin, ua, maxResults));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Amazon reviews for {Query}", productQuery);
        }
        return reviews;
    }

    private static string? ExtractFirstAsin(string html)
    {
        var match = System.Text.RegularExpressions.Regex.Match(html, @"data-asin=""([A-Z0-9]{10})""");
        return match.Success ? match.Groups[1].Value : null;
    }

    private async Task<List<Review>> FetchProductReviewsAsync(string asin, string ua, int max)
    {
        var reviews = new List<Review>();
        try
        {
            var url = $"https://www.amazon.com/product-reviews/{asin}?sortBy=recent&reviewerType=all_reviews&pageSize=20";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", ua);
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return reviews;

            var html = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Amazon review containers
            var reviewNodes = doc.DocumentNode.SelectNodes("//div[@data-hook='review']");
            if (reviewNodes == null) return reviews;

            foreach (var node in reviewNodes)
            {
                if (reviews.Count >= max) break;

                var titleNode = node.SelectSingleNode(".//a[@data-hook='review-title']//span[last()]");
                var bodyNode = node.SelectSingleNode(".//span[@data-hook='review-body']//span");
                var ratingNode = node.SelectSingleNode(".//i[@data-hook='review-star-rating']//span") ??
                                 node.SelectSingleNode(".//i[@data-hook='cmps-review-star-rating']//span");
                var authorNode = node.SelectSingleNode(".//span[@class='a-profile-name']");
                var dateNode = node.SelectSingleNode(".//span[@data-hook='review-date']");

                var title = titleNode != null ? HtmlEntity.DeEntitize(titleNode.InnerText.Trim()) : "";
                var body = bodyNode != null ? HtmlEntity.DeEntitize(bodyNode.InnerText.Trim()) : "";
                var rating = ratingNode != null ? ratingNode.InnerText.Trim().Split(' ')[0] : "";
                var author = authorNode != null ? HtmlEntity.DeEntitize(authorNode.InnerText.Trim()) : "Amazon Customer";
                var date = dateNode != null ? HtmlEntity.DeEntitize(dateNode.InnerText.Trim().Replace("Reviewed in the United States on ", "")) : null;

                var text = !string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(body)
                    ? $"{title}. {body}"
                    : !string.IsNullOrEmpty(body) ? body : title;

                if (text.Length < 10) continue;
                if (text.Length > 500) text = text[..500] + "...";

                reviews.Add(new Review
                {
                    Source = "Amazon",
                    Author = author,
                    Text = text,
                    Rating = !string.IsNullOrEmpty(rating) ? $"⭐ {rating}/5" : null,
                    Date = date,
                    Url = $"https://www.amazon.com/product-reviews/{asin}"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error parsing Amazon reviews: {Msg}", ex.Message);
        }
        return reviews;
    }
}
