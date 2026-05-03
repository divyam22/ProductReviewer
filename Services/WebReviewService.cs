using HtmlAgilityPack;
using SentimentAnalysis.Models;

namespace SentimentAnalysis.Services;

/// <summary>
/// Fetches product discussions from Twitter/X, Trustpilot, and G2 / Capterra
/// </summary>
public class WebReviewService
{
    private readonly HttpClient _http;
    private readonly ILogger<WebReviewService> _logger;

    public WebReviewService(HttpClient http, ILogger<WebReviewService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<Review>> FetchTrustpilotReviewsAsync(string productQuery, int maxResults = 15)
    {
        var reviews = new List<Review>();
        try
        {
            var slug = productQuery.ToLower().Replace(" ", "-");
            var url = $"https://www.trustpilot.com/search?query={Uri.EscapeDataString(productQuery)}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/122.0.0.0 Safari/537.36");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return reviews;

            var html = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Find first company link
            var companyLink = doc.DocumentNode.SelectSingleNode("//a[contains(@href, '/review/')]");
            if (companyLink == null) return reviews;

            var companyHref = companyLink.GetAttributeValue("href", "");
            var reviewsUrl = $"https://www.trustpilot.com{companyHref}";

            // Fetch reviews page
            var reviewReq = new HttpRequestMessage(HttpMethod.Get, reviewsUrl);
            reviewReq.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/122.0.0.0 Safari/537.36");
            reviewReq.Headers.Add("Accept-Language", "en-US,en;q=0.9");

            var reviewResp = await _http.SendAsync(reviewReq);
            if (!reviewResp.IsSuccessStatusCode) return reviews;

            var reviewHtml = await reviewResp.Content.ReadAsStringAsync();
            var reviewDoc = new HtmlDocument();
            reviewDoc.LoadHtml(reviewHtml);

            // Parse review cards
            var reviewNodes = reviewDoc.DocumentNode.SelectNodes("//article[@class[contains(., 'review')]]") ??
                              reviewDoc.DocumentNode.SelectNodes("//div[@class[contains(., 'review-card')]]");

            if (reviewNodes == null)
            {
                // Try JSON-LD
                var scriptNodes = reviewDoc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
                if (scriptNodes != null)
                {
                    foreach (var scriptNode in scriptNodes)
                    {
                        try
                        {
                            using var jdoc = System.Text.Json.JsonDocument.Parse(scriptNode.InnerText);
                            if (jdoc.RootElement.TryGetProperty("@type", out var type) && type.GetString() == "Product")
                            {
                                if (jdoc.RootElement.TryGetProperty("review", out var revArr))
                                {
                                    foreach (var r in revArr.EnumerateArray())
                                    {
                                        if (reviews.Count >= maxResults) break;
                                        var body = r.TryGetProperty("reviewBody", out var rb) ? rb.GetString() ?? "" : "";
                                        var auth = r.TryGetProperty("author", out var a) && a.TryGetProperty("name", out var n) ? n.GetString() ?? "Anonymous" : "Anonymous";
                                        var rat = r.TryGetProperty("reviewRating", out var rr) && rr.TryGetProperty("ratingValue", out var rv) ? rv.GetString() ?? "" : "";

                                        if (body.Length < 10) continue;
                                        reviews.Add(new Review
                                        {
                                            Source = "Trustpilot",
                                            Author = auth,
                                            Text = body.Length > 400 ? body[..400] + "..." : body,
                                            Rating = !string.IsNullOrEmpty(rat) ? $"⭐ {rat}/5" : null,
                                            Url = reviewsUrl
                                        });
                                    }
                                }
                            }
                        }
                        catch { /* skip invalid JSON */ }
                    }
                }
                return reviews;
            }

            foreach (var node in reviewNodes)
            {
                if (reviews.Count >= maxResults) break;

                var bodyNode = node.SelectSingleNode(".//p[@class[contains(., 'review-content')]]") ??
                               node.SelectSingleNode(".//p");
                var authorNode = node.SelectSingleNode(".//span[@class[contains(., 'consumer-information__name')]]") ??
                                 node.SelectSingleNode(".//span[@class[contains(., 'name')]]");

                if (bodyNode == null) continue;
                var text = HtmlEntity.DeEntitize(bodyNode.InnerText.Trim());
                if (text.Length < 10) continue;

                reviews.Add(new Review
                {
                    Source = "Trustpilot",
                    Author = authorNode != null ? HtmlEntity.DeEntitize(authorNode.InnerText.Trim()) : "Reviewer",
                    Text = text.Length > 400 ? text[..400] + "..." : text,
                    Url = reviewsUrl
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Trustpilot reviews for {Query}", productQuery);
        }
        return reviews;
    }

    public async Task<List<Review>> FetchG2ReviewsAsync(string productQuery, int maxResults = 10)
    {
        var reviews = new List<Review>();
        try
        {
            var slug = productQuery.ToLower().Trim().Replace(" ", "-");
            var url = $"https://www.g2.com/products/{slug}/reviews";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/122.0.0.0 Safari/537.36");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return reviews;

            var html = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // JSON-LD review data
            var scriptNodes = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
            if (scriptNodes != null)
            {
                foreach (var scriptNode in scriptNodes)
                {
                    try
                    {
                        using var jdoc = System.Text.Json.JsonDocument.Parse(scriptNode.InnerText);
                        var root = jdoc.RootElement;

                        System.Text.Json.JsonElement reviewArr = default;
                        bool hasReviews = root.TryGetProperty("review", out reviewArr) ||
                                          (root.TryGetProperty("@graph", out var graph) &&
                                           graph.EnumerateArray().Any(g => g.TryGetProperty("review", out reviewArr)));

                        if (!hasReviews) continue;

                        foreach (var r in reviewArr.EnumerateArray())
                        {
                            if (reviews.Count >= maxResults) break;
                            var body = r.TryGetProperty("reviewBody", out var rb) ? rb.GetString() ?? "" : "";
                            var auth = r.TryGetProperty("author", out var a) && a.TryGetProperty("name", out var n) ? n.GetString() ?? "G2 User" : "G2 User";
                            var rat = r.TryGetProperty("reviewRating", out var rr) && rr.TryGetProperty("ratingValue", out var rv) ? rv.GetString() ?? "" : "";

                            if (body.Length < 10) continue;
                            reviews.Add(new Review
                            {
                                Source = "G2",
                                Author = auth,
                                Text = body.Length > 400 ? body[..400] + "..." : body,
                                Rating = !string.IsNullOrEmpty(rat) ? $"⭐ {rat}/5" : null,
                                Url = url
                            });
                        }
                    }
                    catch { /* skip */ }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching G2 reviews for {Query}", productQuery);
        }
        return reviews;
    }
}
