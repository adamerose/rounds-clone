using System.Text.RegularExpressions;

namespace Rounds.Checks;

public static class DeterminismBoundaryChecker
{
    private static readonly IReadOnlyList<Rule> Rules =
    [
        new("DET001", @"\busing\s+Godot\s*;|Godot\.NET\.Sdk|GodotSharp", "Godot references are forbidden in Rounds.Sim."),
        new("DET002", @"\bfloat\b|\bSystem\.Single\b", "`double` is the only floating-point type allowed in Rounds.Sim."),
        new("DET003", @"\b(?:System\.)?Math\.(?:Sin|Cos|Tan|Atan|Atan2|Pow|Exp|Log)\s*\(|\b(?:global\s+)?using\s+static\s+System\.Math\s*;|\b(?:global\s+)?using\s+\w+\s*=\s*System\.Math\s*;", "Unpinned math calls belong only in Math/Trig.cs."),
        new("DET004", @"\b(?:System\.)?Random\b", "Use the world-owned PCG instead of System.Random."),
        new("DET005", @"\b(?:I?Dictionary|HashSet|ISet|ConcurrentDictionary|ImmutableDictionary)\s*<", "Unordered collections are forbidden in Rounds.Sim."),
        new("DET006", @"\b(?:DateTime|DateTimeOffset|Stopwatch)\b|\bEnvironment\.TickCount(?:64)?\b", "Wall-clock APIs are forbidden in Rounds.Sim."),
        new("DET007", @"\b(?:async|await|Task|ValueTask|Thread|ThreadPool|Parallel)\b", "Concurrency is forbidden in the simulation step."),
    ];

    public static IReadOnlyList<string> CheckSimulation(string simulationRoot)
    {
        var failures = new List<string>();
        var files = Directory
            .EnumerateFiles(simulationRoot, "*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".csproj", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(simulationRoot, file).Replace('\\', '/');
            var body = File.ReadAllText(file);
            foreach (var rule in Rules)
            {
                if (rule.Id == "DET003" && relative == "Math/Trig.cs")
                {
                    continue;
                }

                if (rule.Expression.IsMatch(body))
                {
                    failures.Add($"{rule.Id} {relative}: {rule.Message}");
                }
            }
        }

        return failures;
    }

    private sealed class Rule
    {
        public Rule(string id, string pattern, string message)
        {
            Id = id;
            Expression = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            Message = message;
        }

        public string Id { get; }

        public Regex Expression { get; }

        public string Message { get; }
    }
}
