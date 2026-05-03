using HtmlAgilityPack;
using SentimentAnalysis.Models;

namespace SentimentAnalysis.Services;

public class PlayStoreService
{
    private readonly HttpClient _http;
    private readonly ILogger<PlayStoreService> _logger;

    public PlayStoreService(HttpClient http, ILogger<PlayStoreService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<Review>> FetchReviewsAsync(string productQuery, int maxResults = 20)
    {
        var reviews = new List<Review>();

        try
        {
            // First try to find the app ID
            var appId = await SearchAppIdAsync(productQuery);
            if (appId == null)
            {
                _logger.LogWarning("Could not find Play Store app for: {Query}", productQuery);
                return reviews;
            }

            // Fetch app details page
            var url = $"https://play.google.com/store/apps/details?id={Uri.EscapeDataString(appId)}&hl=en&gl=US&showAllReviews=true";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Play Store returned {Status} for app {AppId}", response.StatusCode, appId);
                return reviews;
            }

            var html = await response.Content.ReadAsStringAsync();
            reviews.AddRange(ParseReviewsFromHtml(html, maxResults));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Play Store reviews for {Query}", productQuery);
        }

        return reviews;
    }

    private async Task<string?> SearchAppIdAsync(string query)
    {
        try
        {
            var searchUrl = $"https://play.google.com/store/search?q={Uri.EscapeDataString(query)}&c=apps&hl=en";
            var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var html = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Find first app link
            var appLinks = doc.DocumentNode.SelectNodes("//a[contains(@href, '/store/apps/details')]");
            if (appLinks == null) return null;

            foreach (var link in appLinks)
            {
                var href = link.GetAttributeValue("href", "");
                var match = System.Text.RegularExpressions.Regex.Match(href, @"id=([^&]+)");
                if (match.Success)
                    return match.Groups[1].Value;
            }
        }
        catch { /* Ignore search errors */ }
        return null;
    }

    private List<Review> ParseReviewsFromHtml(string html, int maxResults)
    {
        var reviews = new List<Review>();
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Try structured data first (reviews in JSON-LD)
            var jsonLdNodes = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
            if (jsonLdNodes != null)
            {
                foreach (var node in jsonLdNodes)
                {
                    try
                    {
                        using var jdoc = System.Text.Json.JsonDocument.Parse(node.InnerText);
                        if (jdoc.RootElement.TryGetProperty("review", out var reviewArr))
                        {
                            foreach (var r in reviewArr.EnumerateArray())
                            {
                                if (reviews.Count >= maxResults) break;

                                var author = r.TryGetProperty("author", out var auth) && auth.TryGetProperty("name", out var name)
                                    ? name.GetString() ?? "Anonymous"
                                    : "Anonymous";

                                var body = r.TryGetProperty("reviewBody", out var rb) ? rb.GetString() ?? "" : "";
                                var rating = r.TryGetProperty("reviewRating", out var rr) && rr.TryGetProperty("ratingValue", out var rv)
                                    ? rv.GetString() ?? ""
                                    : "";

                                if (body.Length < 10) continue;
                                if (body.Length > 400) body = body[..400] + "...";

                                reviews.Add(new Review
                                {
                                    Source = "Google Play Store",
                                    Author = author,
                                    Text = body,
                                    Rating = !string.IsNullOrEmpty(rating) ? $"⭐ {rating}/5" : null
                                });
                            }
                        }
                    }
                    catch { /* Invalid JSON, skip */ }
                }
            }

            // Fallback: parse review divs from HTML
            if (reviews.Count == 0)
            {
                // Look for review text containers (Google Play uses data attributes)
                var reviewNodes = doc.DocumentNode.SelectNodes("//div[@jscontroller]//span[@jsname]") ?? new HtmlNodeCollection(null);
                foreach (var node in reviewNodes)
                {
                    if (reviews.Count >= maxResults) break;
                    var text = HtmlEntity.DeEntitize(node.InnerText).Trim();
                    if (text.Length > 30 && text.Length < 600 && !text.Contains("Cookie") && !text.Contains("script"))
                    {
                        reviews.Add(new Review
                        {
                            Source = "Google Play Store",
                            Author = "Play Store User",
                            Text = text.Length > 400 ? text[..400] + "..." : text
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error parsing Play Store HTML: {Msg}", ex.Message);
        }
        return reviews;
    }
}
