using System.Text.RegularExpressions;

namespace SlackBotBroker.Broker;

/// <summary>Replaces text matching any configured sensitive pattern before it is posted to Slack.</summary>
public sealed class SensitiveContentRedactor
{
    private const string Replacement = "[REDACTED]";

    private readonly IReadOnlyList<Regex> _patterns;

    public SensitiveContentRedactor(IEnumerable<string> patterns)
    {
        _patterns = patterns.Select(p => new Regex(p, RegexOptions.Compiled)).ToList();
    }

    public string Redact(string text) => _patterns.Aggregate(text, (current, pattern) => pattern.Replace(current, Replacement));
}
