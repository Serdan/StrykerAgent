using System.Text;
using System.Text.RegularExpressions;

namespace StrykerAgent;

public sealed class GlobMatcher
{
    private readonly Regex _regex;

    public GlobMatcher(string pattern)
    {
        var normalized = pattern.Replace('\\', '/');
        _regex = new Regex("^" + GlobToRegex(normalized) + "$", RegexOptions.CultureInvariant);
    }

    public bool IsMatch(string value)
    {
        var normalized = value.Replace('\\', '/');
        return _regex.IsMatch(normalized);
    }

    private static string GlobToRegex(string pattern)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < pattern.Length; i++)
        {
            var current = pattern[i];
            if (current == '*')
            {
                var isDouble = i + 1 < pattern.Length && pattern[i + 1] == '*';
                if (isDouble)
                {
                    builder.Append(".*");
                    i++;
                }
                else
                {
                    builder.Append("[^/]*");
                }
            }
            else if (current == '?')
            {
                builder.Append("[^/]");
            }
            else
            {
                builder.Append(Regex.Escape(current.ToString()));
            }
        }

        return builder.ToString();
    }
}
