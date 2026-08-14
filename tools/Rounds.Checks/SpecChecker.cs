using System.Text.Json;

namespace Rounds.Checks;

public static class SpecChecker
{
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

        foreach (var measurement in documents["measurements.json"].RootElement.GetProperty("measurements").EnumerateArray())
        {
            var measurementId = measurement.GetProperty("id").GetString()!;
            var factId = measurement.GetProperty("metricFactId").GetString()!;
            var sourceId = measurement.GetProperty("source").GetString()!;
            if (!factIds.Contains(factId))
            {
                failures.Add($"SPEC006 measurements.json measurement `{measurementId}` targets unknown fact `{factId}`.");
            }

            if (!sourceIds.Contains(sourceId))
            {
                failures.Add($"SPEC007 measurements.json measurement `{measurementId}` cites unknown source `{sourceId}`.");
            }
        }
    }
}
