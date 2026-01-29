namespace StrykerAgent;

public static class StatusConstants
{
    public static readonly string[] All =
    [
        "Killed",
        "Survived",
        "NoCoverage",
        "CompileError",
        "RuntimeError",
        "Timeout",
        "Ignored",
        "Pending"
    ];

    private static readonly HashSet<string> AllSet = new(All, StringComparer.Ordinal);
    private static readonly Dictionary<string, string> Normalized =
        All.ToDictionary(value => value, value => value, StringComparer.OrdinalIgnoreCase);

    public static bool IsValid(string? value) => value != null && AllSet.Contains(value);

    public static bool TryNormalize(string value, out string normalized) =>
        Normalized.TryGetValue(value, out normalized!);

    public static bool IsEffectiveForScore(string status) =>
        status is "Killed" or "Survived" or "Timeout" or "NoCoverage" or "CompileError" or "RuntimeError";
}
