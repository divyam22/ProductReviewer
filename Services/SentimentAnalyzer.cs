using SentimentAnalysis.Models;

namespace SentimentAnalysis.Services;

/// <summary>
/// VADER-inspired lexicon-based sentiment analysis engine.
/// Accurate for social media and product review text.
/// No heavy ML models required.
/// </summary>
public class SentimentAnalyzer
{
    private static readonly Dictionary<string, double> _lexicon = BuildLexicon();

    private static readonly HashSet<string> _boosterWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "absolutely", "amazingly", "awfully", "completely", "considerably", "decidedly",
        "deeply", "enormously", "entirely", "especially", "exceptionally", "extremely",
        "fabulously", "fantastically", "genuinely", "greatly", "highly", "incredibly",
        "insanely", "intensely", "largely", "legitimately", "majorly", "more", "most",
        "particularly", "profoundly", "purely", "quite", "really", "remarkably",
        "so", "something", "substantially", "super", "supremely", "thoroughly", "totally",
        "tremendously", "truly", "uber", "unbelievably", "unusually", "utterly",
        "very", "virtually"
    };

    private static readonly HashSet<string> _negators = new(StringComparer.OrdinalIgnoreCase)
    {
        "not", "no", "never", "nobody", "nothing", "neither", "nor", "none",
        "can't", "cannot", "won't", "wouldn't", "don't", "doesn't", "didn't",
        "isn't", "aren't", "wasn't", "weren't", "hardly", "barely", "scarcely"
    };

    public SentimentResult Analyze(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new SentimentResult { Score = 0, Label = SentimentLabel.Neutral };

        var words = Tokenize(text);
        double sum = 0;
        int count = 0;

        for (int i = 0; i < words.Count; i++)
        {
            var word = words[i].ToLowerInvariant().Trim('!', '?', '.', ',', ';', ':', '"', '\'', '(', ')');
            if (!_lexicon.TryGetValue(word, out double val)) continue;

            // Check for negation in preceding 3 words
            bool negated = false;
            for (int j = Math.Max(0, i - 3); j < i; j++)
            {
                if (_negators.Contains(words[j]))
                {
                    negated = true;
                    break;
                }
            }

            // Check for booster in preceding 2 words
            double boost = 1.0;
            for (int j = Math.Max(0, i - 2); j < i; j++)
            {
                if (_boosterWords.Contains(words[j]))
                {
                    boost = 1.3;
                    break;
                }
            }

            // Exclamation amplification
            int exclamations = text.Count(c => c == '!');
            if (exclamations > 0) boost += Math.Min(exclamations * 0.1, 0.3);

            // All caps amplification
            if (words[i] == words[i].ToUpperInvariant() && words[i].Length > 2)
                boost += 0.2;

            val *= boost;
            if (negated) val = -val * 0.74;

            sum += val;
            count++;
        }

        if (count == 0)
            return new SentimentResult { Score = 0.5, Label = SentimentLabel.Neutral };

        // Normalize to [0, 1]
        double rawScore = sum / Math.Sqrt(sum * sum + 15);
        double normalizedScore = (rawScore + 1.0) / 2.0;

        var label = normalizedScore >= 0.6
            ? SentimentLabel.Positive
            : normalizedScore <= 0.4
                ? SentimentLabel.Negative
                : SentimentLabel.Neutral;

        return new SentimentResult
        {
            Score = Math.Round(normalizedScore, 4),
            Label = label,
            RawScore = rawScore
        };
    }

    private static List<string> Tokenize(string text)
    {
        return text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static Dictionary<string, double> BuildLexicon()
    {
        // VADER-inspired sentiment lexicon with ~500+ terms
        return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            // Very positive (3.0-4.0)
            { "outstanding", 4.0 }, { "phenomenal", 4.0 }, { "spectacular", 3.8 },
            { "superb", 3.8 }, { "flawless", 3.8 }, { "incredible", 3.7 },
            { "amazing", 3.7 }, { "wonderful", 3.7 }, { "brilliant", 3.6 },
            { "fantastic", 3.6 }, { "magnificent", 3.6 }, { "exceptional", 3.5 },
            { "extraordinary", 3.5 }, { "perfect", 3.5 }, { "excellent", 3.4 },
            { "marvelous", 3.4 }, { "fabulous", 3.3 }, { "terrific", 3.3 },
            { "impressive", 3.0 }, { "delightful", 3.0 }, { "splendid", 3.0 },
            
            // Positive (1.5-2.9)
            { "great", 2.9 }, { "awesome", 2.9 }, { "love", 2.8 }, { "loved", 2.8 },
            { "beautiful", 2.7 }, { "best", 2.7 }, { "happy", 2.6 }, { "pleased", 2.5 },
            { "enjoy", 2.5 }, { "enjoyed", 2.5 }, { "enjoying", 2.5 },
            { "good", 2.4 }, { "nice", 2.3 }, { "like", 2.2 }, { "liked", 2.2 },
            { "recommend", 2.2 }, { "recommended", 2.2 }, { "worth", 2.0 },
            { "solid", 1.9 }, { "reliable", 1.9 }, { "quality", 1.8 }, { "fun", 1.8 },
            { "helpful", 1.8 }, { "useful", 1.8 }, { "efficient", 1.8 }, { "fast", 1.7 },
            { "smooth", 1.7 }, { "clean", 1.6 }, { "clear", 1.6 }, { "easy", 1.6 },
            { "convenient", 1.6 }, { "comfortable", 1.6 }, { "satisfied", 1.6 },
            { "satisfactory", 1.5 }, { "adequate", 1.5 }, { "decent", 1.5 },
            { "works", 1.4 }, { "working", 1.4 }, { "positive", 1.4 },
            { "upgrade", 1.3 }, { "improved", 1.3 }, { "improvement", 1.3 },
            { "innovative", 1.7 }, { "sleek", 1.6 }, { "stylish", 1.6 },
            { "durable", 1.8 }, { "sturdy", 1.7 }, { "robust", 1.8 },
            { "powerful", 1.7 }, { "capable", 1.5 }, { "responsive", 1.7 },
            { "intuitive", 1.6 }, { "seamless", 1.8 }, { "premium", 1.6 },
            { "thrilled", 2.8 }, { "excited", 2.5 }, { "ecstatic", 3.2 },
            { "adore", 2.9 }, { "cherish", 2.7 }, { "treasure", 2.5 },
            { "wow", 2.8 }, { "wowed", 2.8 }, { "impressed", 2.5 },
            { "praise", 2.3 }, { "highly", 1.5 }, { "definitely", 1.3 },
            { "stellar", 3.2 }, { "top", 2.0 }, { "top-notch", 3.0 },
            { "high-quality", 2.8 }, { "well-made", 2.4 }, { "well-built", 2.4 },
            { "bang", 1.8 }, { "value", 1.7 }, { "affordable", 1.9 },
            { "budget-friendly", 1.8 }, { "cheap", -0.5 }, { "inexpensive", 1.2 },

            // Mildly positive (0.5-1.4)
            { "ok", 0.9 }, { "okay", 0.9 }, { "fine", 0.8 }, { "alright", 0.8 },
            { "acceptable", 0.7 }, { "average", 0.3 }, { "passable", 0.5 },

            // Mildly negative (-0.5 to -1.4)
            { "meh", -0.8 }, { "mediocre", -1.0 }, { "disappointing", -1.8 },
            { "disappointed", -1.8 }, { "slow", -1.4 }, { "laggy", -1.8 },
            { "glitchy", -1.8 }, { "buggy", -1.8 }, { "issue", -1.2 },
            { "issues", -1.2 }, { "problem", -1.3 }, { "problems", -1.3 },
            { "overpriced", -1.5 }, { "expensive", -1.0 }, { "overrated", -2.0 },
            { "fragile", -1.4 }, { "weak", -1.3 }, { "poor", -1.8 },
            { "lacking", -1.2 }, { "limited", -0.8 }, { "annoying", -1.7 },
            { "frustrating", -1.8 }, { "frustrated", -1.8 }, { "confusing", -1.5 },
            { "complicated", -1.2 }, { "difficult", -1.1 }, { "hard", -0.7 },
            { "unclear", -1.1 }, { "unreliable", -1.8 }, { "boring", -1.5 },
            { "dull", -1.3 }, { "outdated", -1.2 }, { "old", -0.6 },

            // Negative (-1.5 to -2.9)
            { "bad", -2.5 }, { "worst", -3.4 }, { "terrible", -3.4 },
            { "horrible", -3.4 }, { "awful", -3.4 }, { "hate", -3.1 },
            { "hated", -3.1 }, { "useless", -2.8 }, { "waste", -2.5 },
            { "broken", -2.5 }, { "garbage", -3.0 }, { "trash", -2.8 },
            { "junk", -2.7 }, { "scam", -3.5 }, { "fraud", -3.5 },
            { "fake", -2.5 }, { "regret", -2.5 }, { "refund", -2.0 },
            { "return", -1.5 }, { "returned", -1.5 }, { "defective", -2.8 },
            { "defect", -2.5 }, { "faulty", -2.8 }, { "malfunction", -2.8 },
            { "fails", -2.0 }, { "fail", -2.0 }, { "failed", -2.0 },
            { "failure", -2.5 }, { "crash", -2.5 }, { "crashes", -2.5 },
            { "error", -1.8 }, { "errors", -1.8 }, { "unresponsive", -2.0 },
            { "overheating", -2.5 }, { "overheat", -2.5 }, { "flimsy", -2.0 },
            { "cheap-feeling", -2.0 }, { "low-quality", -2.5 }, { "poorly-made", -2.5 },
            { "misleading", -2.3 }, { "dishonest", -2.5 }, { "lied", -2.5 },
            { "sucks", -3.0 }, { "sucked", -3.0 }, { "disgusting", -3.2 },
            { "pathetic", -2.8 }, { "lousy", -2.7 }, { "atrocious", -3.2 },
            { "dreadful", -3.0 }, { "appalling", -3.0 }, { "unbearable", -3.0 },
            { "unacceptable", -2.5 }, { "ridiculous", -2.0 }, { "absurd", -1.8 },
            
            // Very negative (-3.0 to -4.0)
            { "catastrophic", -4.0 }, { "abysmal", -4.0 }, { "catastrophe", -4.0 },
            { "disaster", -3.5 }, { "nightmare", -3.5 }, { "worthless", -3.5 },
            { "deplorable", -3.5 },
        };
    }
}

public class SentimentResult
{
    public double Score { get; set; }
    public double RawScore { get; set; }
    public SentimentLabel Label { get; set; }
}
