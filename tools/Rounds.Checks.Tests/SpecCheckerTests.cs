using System.Text.Json.Nodes;

namespace Rounds.Checks.Tests;

public sealed class SpecCheckerTests : IDisposable
{
    private readonly string _repository = Path.Combine(
        Path.GetTempPath(),
        $"rounds-spec-check-{Guid.NewGuid():N}");

    [Fact]
    public void CompleteSourcedSpecificationPasses()
    {
        CreateValidRepository();

        Assert.Empty(SpecChecker.CheckRepository(_repository));
    }

    [Fact]
    public void FactWithoutAProvenanceSourceIsRejectedBySchema()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "match.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["facts"]![0]!.AsObject().Remove("sources");
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC001 match.json", StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownSourceReferenceIsRejected()
    {
        CreateValidRepository();
        WriteMechanics("match", "match-fact", "missing-source");

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC005 match.json", StringComparison.Ordinal));
    }

    [Fact]
    public void MeasurementMustTargetAKnownFact()
    {
        CreateValidRepository();
        WriteMeasurements("missing-fact");

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC006 measurements.json", StringComparison.Ordinal));
    }

    [Fact]
    public void UnsupportedSchemaKeywordCannotSilentlyWeakenTheGate()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "schema", "mechanics.schema.json");
        var schema = JsonNode.Parse(File.ReadAllText(path))!;
        schema["unevaluatedProperties"] = false;
        File.WriteAllText(path, schema.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure =>
            failure.StartsWith("SPEC001 match.json", StringComparison.Ordinal) &&
            failure.Contains("unsupported schema keyword", StringComparison.Ordinal));
    }

    [Fact]
    public void NormalizedValueMustRecomputeFromRecordedRawFields()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "measurements.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["measurements"]![0]!["pixelMeasurements"]!["distance"] = 2;
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC009 measurements.json", StringComparison.Ordinal));
    }

    [Fact]
    public void DerivationOperandMustNameARecordedRawField()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "measurements.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["measurements"]![0]!["derivation"]!["operands"]![0] = "pixelMeasurements.missing";
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC016 measurements.json", StringComparison.Ordinal));
    }

    [Fact]
    public void AcceptedMeasurementMustHaveACoverageContract()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "measurements.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["coverage"]![0]!["metricFactId"] = "match-fact";
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC014 measurements.json", StringComparison.Ordinal));
    }

    [Fact]
    public void CoverageMustContainEnoughIndependentAcceptedSources()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "measurements.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["coverage"]![0]!["minimumIndependentSources"] = 2;
        document["coverage"]![0]!["limitation"] = "Fixture limitation does not waive the declared minimum.";
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC012 measurements.json", StringComparison.Ordinal));
    }

    [Fact]
    public void RequiredMetricCoverageCannotBeOmittedWithItsMeasurements()
    {
        CreateValidRepository();
        var mechanicsPath = Path.Combine(_repository, "spec", "player.json");
        var mechanics = JsonNode.Parse(File.ReadAllText(mechanicsPath))!;
        mechanics["facts"]![0]!["id"] = "player-run-speed";
        File.WriteAllText(mechanicsPath, mechanics.ToJsonString());
        var measurementsPath = Path.Combine(_repository, "spec", "measurements.json");
        var measurements = JsonNode.Parse(File.ReadAllText(measurementsPath))!;
        measurements["coverage"]![0]!["metricFactId"] = "player-run-speed";
        measurements["measurements"]![0]!["metricFactId"] = "player-run-speed";
        File.WriteAllText(measurementsPath, measurements.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure =>
            failure == "SPEC015 measurements.json omits required coverage for fact `player-diameter`.");
    }

    public void Dispose()
    {
        if (Directory.Exists(_repository))
        {
            Directory.Delete(_repository, recursive: true);
        }
    }

    private void CreateValidRepository()
    {
        var schemaRoot = Path.Combine(_repository, "spec", "schema");
        Directory.CreateDirectory(schemaRoot);
        var packagedSchemas = Path.Combine(AppContext.BaseDirectory, "spec", "schema");
        foreach (var source in Directory.EnumerateFiles(packagedSchemas, "*.json"))
        {
            File.Copy(source, Path.Combine(schemaRoot, Path.GetFileName(source)));
        }

        File.WriteAllText(Path.Combine(_repository, "spec", "sources.json"), """
            {
              "$schema": "./schema/source-index.schema.json",
              "schemaVersion": 1,
              "accessedOn": "2026-08-14",
              "sources": [{
                "id": "source-one",
                "title": "Source one",
                "publisher": "Publisher",
                "url": "https://example.test/source-one",
                "accessedOn": "2026-08-14",
                "kind": "official-store",
                "scope": "Test fixture.",
                "reliability": "high"
              }]
            }
            """);

        foreach (var kind in new[] { "match", "controls", "player", "combat", "camera" })
        {
            WriteMechanics(kind, $"{kind}-fact", "source-one");
        }

        WriteMeasurements("player-fact");
    }

    private void WriteMechanics(string kind, string factId, string sourceId)
    {
        File.WriteAllText(Path.Combine(_repository, "spec", $"{kind}.json"), $$"""
            {
              "$schema": "./schema/mechanics.schema.json",
              "schemaVersion": 1,
              "kind": "{{kind}}",
              "targetBuild": "21020021",
              "facts": [{
                "id": "{{factId}}",
                "statement": "Fixture fact.",
                "status": "confirmed",
                "value": 1,
                "unit": "fixture units",
                "confidence": "high",
                "tolerance": { "kind": "exact", "value": 0, "unit": "fixture units" },
                "method": "Fixture method.",
                "sources": ["{{sourceId}}"]
              }]
            }
            """);
    }

    private void WriteMeasurements(string factId)
    {
        File.WriteAllText(Path.Combine(_repository, "spec", "measurements.json"), $$"""
            {
              "$schema": "./schema/measurements.schema.json",
              "schemaVersion": 1,
              "targetBuild": "21020021",
              "coverage": [{
                "metricFactId": "{{factId}}",
                "category": "movement",
                "minimumIndependentSources": 1,
                "rationale": "Fixture coverage.",
                "limitation": "One fixture source is sufficient for this isolated checker test."
              }],
              "measurements": [{
                "id": "m-fixture",
                "metricFactId": "{{factId}}",
                "source": "source-one",
                "sourceTimestamp": "00:00:00.000",
                "observedFrameIntervalTicks": 1,
                "activeCards": [],
                "modifierControl": "No modifiers in the fixture.",
                "countsTowardCoverage": true,
                "pixelMeasurements": { "distance": 1 },
                "derivation": { "operation": "identity", "operands": ["pixelMeasurements.distance"] },
                "normalizedValue": 1,
                "unit": "fixture units",
                "tolerance": 0,
                "confidence": "high",
                "method": "Fixture method."
              }]
            }
            """);
    }
}
