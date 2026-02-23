using System.Globalization;

namespace VidStash.Helpers;

public static class StringHelpers
{
    private static readonly HashSet<string> SmallWords =
    [
        "a", "an", "and", "as", "at", "but", "by", "for",
        "in", "of", "on", "or", "the", "to", "with"
    ];

    public static string ToTitleCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (i == 0 || !SmallWords.Contains(words[i].ToLowerInvariant()))
            {
                words[i] = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words[i].ToLowerInvariant());
            }
            else
            {
                words[i] = words[i].ToLowerInvariant();
            }
        }
        return string.Join(' ', words);
    }

    public static double Similarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return 0;

        a = a.ToLowerInvariant().Trim();
        b = b.ToLowerInvariant().Trim();

        if (a == b) return 1.0;

        int maxLen = Math.Max(a.Length, b.Length);
        if (maxLen == 0) return 1.0;

        int distance = LevenshteinDistance(a, b);
        return 1.0 - ((double)distance / maxLen);
    }

    private static int LevenshteinDistance(string s, string t)
    {
        int n = s.Length, m = t.Length;
        var d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }
}
