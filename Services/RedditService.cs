using System.Text.Json;
using SentimentAnalysis.Models;

namespace SentimentAnalysis.Services;

public class RedditService
{
    private readonly HttpClient _http;
    private readonly ILogger<RedditService> _logger;

    public RedditService(HttpClient http, ILogger<RedditService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<Review>> FetchReviewsAsync(string productQuery, int maxResults = 30)
    {
        var reviews = new List<Review>();

        try
        {
            // Search Reddit for the product
            var searchUrl = $"https://www.reddit.com/search.json?q={Uri.EscapeDataString(productQuery + " review")}&sort=relevance&limit=50&type=link";

            var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            request.Headers.Add("User-Agent", "SentimentAnalysisBot/1.0 (product review analyzer)");

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Reddit search returned {Status}", response.StatusCode);
                return reviews;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var posts = doc.RootElement
                .GetProperty("data")
                .GetProperty("children");

            int count = 0;
            foreach (var post in posts.EnumerateArray())
            {
                if (count >= maxResults) break;
                var data = post.GetProperty("data");

                var title = data.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                var selftext = data.TryGetProperty("selftext", out var s) ? s.GetString() ?? "" : "";
                var author = data.TryGetProperty("author", out var a) ? a.GetString() ?? "u/unknown" : "u/unknown";
                var subreddit = data.TryGetProperty("subreddit", out var sub) ? sub.GetString() ?? "" : "";
                var score = data.TryGetProperty("score", out var sc) ? sc.GetInt32() : 0;
                var permalink = data.TryGetProperty("permalink", out var p) ? p.GetString() ?? "" : "";
                var created = data.TryGetProperty("created_utc", out var cr) ? cr.GetDouble() : 0;

                var text = string.IsNullOrWhiteSpace(selftext) ? title : $"{title}. {selftext}";
                if (text.Length < 10) continue;

                // Trim text to reasonable length
                if (text.Length > 500) text = text[..500] + "...";

                reviews.Add(new Review
                {
                    Source = $"Reddit (r/{subreddit})",
                    Author = $"u/{author}",
                    Text = text,
                    Rating = score > 0 ? $"👍 {score}" : null,
                    Date = created > 0 ? DateTimeOffset.FromUnixTimeSeconds((long)created).ToString("MMM dd, yyyy") : null,
                    Url = !string.IsNullOrEmpty(permalink) ? $"https://reddit.com{permalink}" : null
                });

                count++;
            }

            // Also fetch comments from the first post for richer data
            if (posts.GetArrayLength() > 0)
            {
                var firstPost = posts[0].GetProperty("data");
                var postId = firstPost.TryGetProperty("id", out var id) ? id.GetString() : null;
                var postSubreddit = firstPost.TryGetProperty("subreddit", out var sr) ? sr.GetString() : null;

                if (postId != null && postSubreddit != null && reviews.Count < maxResults)
                {
                    var commentReviews = await FetchCommentsAsync(postSubreddit, postId, maxResults - reviews.Count);
                    reviews.AddRange(commentReviews);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Reddit reviews for {Query}", productQuery);
        }

        return reviews;
    }

    private async Task<List<Review>> FetchCommentsAsync(string subreddit, string postId, int max)
    {
        var comments = new List<Review>();
        try
        {
            var url = $"https://www.reddit.com/r/{subreddit}/comments/{postId}.json?limit={max}&sort=top";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "SentimentAnalysisBot/1.0");

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return comments;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() < 2)
                return comments;

            var commentData = doc.RootElement[1].GetProperty("data").GetProperty("children");

            foreach (var comment in commentData.EnumerateArray())
            {
                if (comments.Count >= max) break;
                if (!comment.TryGetProperty("data", out var data)) continue;

                var body = data.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
                var author = data.TryGetProperty("author", out var a) ? a.GetString() ?? "unknown" : "unknown";
                var score = data.TryGetProperty("score", out var sc) ? sc.GetInt32() : 0;

                if (body.Length < 15 || body == "[deleted]" || body == "[removed]") continue;
                if (body.Length > 400) body = body[..400] + "...";

                comments.Add(new Review
                {
                    Source = $"Reddit (r/{subreddit})",
                    Author = $"u/{author}",
                    Text = body,
                    Rating = score > 0 ? $"👍 {score}" : null,
                });
            }
        }
        catch { /* Ignore comment fetch errors */ }
        return comments;
    }
}
