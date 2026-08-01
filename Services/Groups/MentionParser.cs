using System.Text.RegularExpressions;

namespace BlogGraphQlApp.Services.Groups
{
    public static class MentionParser
    {
        private static readonly Regex MentionRegex = new(@"@([A-Za-z0-9_.-]{1,32})", RegexOptions.Compiled);

        public static IReadOnlyList<string> Parse(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return [];

            return MentionRegex.Matches(content)
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
