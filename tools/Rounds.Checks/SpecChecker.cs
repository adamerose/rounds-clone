using System.Text.Json;

namespace Rounds.Checks;

public static class SpecChecker
{
    private static readonly string[] RequiredMeasuredFacts =
    [
        "player-diameter",
        "player-run-speed",
        "player-jump-apex-height",
        "player-jump-apex-time",
        "combat-projectile-speed",
        "combat-projectile-radius",
        "combat-recoil-speed",
        "combat-block-window",
        "camera-horizontal-span",
        "camera-out-of-bounds-result-delay",
    ];

    private static readonly IReadOnlyDictionary<string, string> RequiredDocuments = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["sources.json"] = "source-index.schema.json",
        ["match.json"] = "mechanics.schema.json",
        ["controls.json"] = "mechanics.schema.json",
        ["player.json"] = "mechanics.schema.json",
        ["combat.json"] = "mechanics.schema.json",
        ["camera.json"] = "mechanics.schema.json",
        ["measurements.json"] = "measurements.schema.json",
    };

    public static IReadOnlyList<string> CheckRepository(string repository)
    {
        var failures = new List<string>();
        var specRoot = Path.Combine(repository, "spec");
        var schemaRoot = Path.Combine(specRoot, "schema");
        var documents = new Dictionary<string, JsonDocument>(StringComparer.Ordinal);
        var schemas = new Dictionary<string, JsonDocument>(StringComparer.Ordinal);

        try
        {
            foreach (var schemaName in RequiredDocuments.Values.Distinct(StringComparer.Ordinal))
            {
                schemas[schemaName] = Parse(Path.Combine(schemaRoot, schemaName), failures);
            }

            foreach (var (documentName, schemaName) in RequiredDocuments)
            {
                var document = Parse(Path.Combine(specRoot, documentName), failures);
                documents[documentName] = document;
                if (document.RootElement.ValueKind == JsonValueKind.Null || schemas[schemaName].RootElement.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                foreach (var failure in JsonSchemaSubsetValidator.Validate(schemas[schemaName].RootElement, document.RootElement))
                {
                    failures.Add($"SPEC001 {documentName} {failure}");
                }
            }

            if (failures.Any(failure => failure.StartsWith("SPEC000", StringComparison.Ordinal) || failure.StartsWith("SPEC001", StringComparison.Ordinal)))
            {
                return failures;
            }

            CrossCheck(documents, failures);
            return failures;
        }
        finally
        {
            foreach (var document in documents.Values)
            {
                document.Dispose();
            }

            foreach (var schema in schemas.Values)
            {
                schema.Dispose();
            }
        }
    }

    private static JsonDocument Parse(string path, List<string> failures)
    {
        try
        {
            return JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            failures.Add($"SPEC000 {Path.GetFileName(path)} could not be read as JSON: {exception.Message}");
            return JsonDocument.Parse("null");
        }
    }

    private static void CrossCheck(IReadOnlyDictionary<string, JsonDocument> documents, List<string> failures)
    {
        var sourceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in documents["sources.json"].RootElement.GetProperty("sources").EnumerateArray())
        {
            var sourceId = source.GetProperty("id").GetString()!;
            if (!sourceIds.Add(sourceId))
            {
                failures.Add($"SPEC002 sources.json duplicates source id `{sourceId}`.");
            }
        }

        var factIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var documentName in RequiredDocuments.Keys.Where(name => RequiredDocuments[name] == "mechanics.schema.json"))
        {
            var root = documents[documentName].RootElement;
            var expectedKind = Path.GetFileNameWithoutExtension(documentName);
            if (root.GetProperty("kind").GetString() != expectedKind)
            {
                failures.Add($"SPEC003 {documentName} kind must be `{expectedKind}`.");
            }

            foreach (var fact in root.GetProperty("facts").EnumerateArray())
            {
                var factId = fact.GetProperty("id").GetString()!;
                if (!factIds.Add(factId))
                {
                    failures.Add($"SPEC004 {documentName} duplicates fact id `{factId}`.");
                }

                foreach (var source in fact.GetProperty("sources").EnumerateArray().Select(item => item.GetString()!))
                {
                    if (!sourceIds.Contains(source))
                    {
                        failures.Add($"SPEC005 {documentName} fact `{factId}` cites unknown source `{source}`.");
                    }
                }
            }
        }

        var measurementRoot = documents["measurements.json"].RootElement;
        var measurementIds = new HashSet<string>(StringComparer.Ordinal);
        var measuredSources = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var measurement in measurementRoot.GetProperty("measurements").EnumerateArray())
        {
            var measurementId = measurement.GetProperty("id").GetString()!;
            var factId = measurement.GetProperty("metricFactId").GetString()!;
            var sourceId = measurement.GetProperty("source").GetString()!;
            if (!measurementIds.Add(measurementId))
            {
                failures.Add($"SPEC008 measurements.json duplicates measurement id `{measurementId}`.");
            }

            if (!factIds.Contains(factId))
            {
                failures.Add($"SPEC006 measurements.json measurement `{measurementId}` targets unknown fact `{factId}`.");
            }

            if (!sourceIds.Contains(sourceId))
            {
                failures.Add($"SPEC007 measurements.json measurement `{measurementId}` cites unknown source `{sourceId}`.");
            }

            if (measurement.GetProperty("countsTowardCoverage").GetBoolean())
            {
                if (!measuredSources.TryGetValue(factId, out var sources))
                {
                    sources = new HashSet<string>(StringComparer.Ordinal);
                    measuredSources[factId] = sources;
                }

                sources.Add(sourceId);
            }

            var derivation = measurement.GetProperty("derivation");
            var operation = derivation.GetProperty("operation").GetString()!;
            var operands = derivation.GetProperty("operands").EnumerateArray().Select(item => item.GetDouble()).ToArray();
            var recomputed = operation switch
            {
                "identity" => operands[0],
                "divide" => operands.Skip(1).Aggregate(operands[0], (result, operand) => result / operand),
                "multiply" => operands.Aggregate(1.0, (result, operand) => result * operand),
                _ => double.NaN,
            };
            var recorded = measurement.GetProperty("normalizedValue").GetDouble();
            if (!double.IsFinite(recomputed) || Math.Abs(recomputed - recorded) > 0.0001)
            {
                failures.Add($"SPEC009 measurements.json measurement `{measurementId}` records {recorded} but its derivation recomputes to {recomputed:F6}.");
            }
        }

        var coveredFacts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var coverage in measurementRoot.GetProperty("coverage").EnumerateArray())
        {
            var factId = coverage.GetProperty("metricFactId").GetString()!;
            if (!coveredFacts.Add(factId))
            {
                failures.Add($"SPEC010 measurements.json duplicates coverage for fact `{factId}`.");
            }

            if (!factIds.Contains(factId))
            {
                failures.Add($"SPEC011 measurements.json covers unknown fact `{factId}`.");
            }

            var minimum = coverage.GetProperty("minimumIndependentSources").GetInt32();
            var actual = measuredSources.TryGetValue(factId, out var sources) ? sources.Count : 0;
            if (actual < minimum)
            {
                failures.Add($"SPEC012 measurements.json fact `{factId}` requires {minimum} independent sources but has {actual}.");
            }

            if (minimum < 2 && !coverage.TryGetProperty("limitation", out _))
            {
                failures.Add($"SPEC013 measurements.json fact `{factId}` allows fewer than two sources without documenting the limitation.");
            }
        }

        foreach (var factId in measuredSources.Keys)
        {
            if (!coveredFacts.Contains(factId))
            {
                failures.Add($"SPEC014 measurements.json fact `{factId}` has accepted measurements but no coverage contract.");
            }
        }

        if (RequiredMeasuredFacts.Any(factIds.Contains))
        {
            foreach (var factId in RequiredMeasuredFacts.Where(factId => !coveredFacts.Contains(factId)))
            {
                failures.Add($"SPEC015 measurements.json omits required coverage for fact `{factId}`.");
            }
        }
    }
}
