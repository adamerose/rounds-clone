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
        document["measurements"]![0]!["pixelMeasurements"]!["distancePixels"] = 2;
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
    public void RecordedSpanMustMatchItsEndpoints()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "measurements.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["measurements"]![0]!["pixelMeasurements"]!["endCenterX"] = 2;
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC017 measurements.json", StringComparison.Ordinal));
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

    [Fact]
    public void CardWithUnknownStatOrHookIsRejected()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "cards.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["cards"]![0]!["effects"]![0]!["target"] = "weapon.unknown-stat";
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC021 cards.json", StringComparison.Ordinal));
    }

    [Fact]
    public void CardEffectWithoutSourceIsRejectedBySchema()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "cards.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["cards"]![0]!["effects"]![0]!["provenance"]!.AsObject().Remove("sources");
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC001 cards.json", StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateCardIdIsRejected()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "cards.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["cards"]![1]!["id"] = document["cards"]![0]!["id"]!.GetValue<string>();
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC019 cards.json", StringComparison.Ordinal));
    }

    [Fact]
    public void UnsupportedCardStackingOperatorIsRejectedBySchema()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "cards.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["cards"]![0]!["effects"]![0]!["stacking"] = "concatenate";
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC001 cards.json", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingPerEffectStackingProvenanceIsRejectedBySchema()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "cards.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["cards"]![0]!["effects"]![0]!.AsObject().Remove("stackingProvenance");
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC001 cards.json", StringComparison.Ordinal));
    }

    [Fact]
    public void MultiplyOperationWithPercentUnitIsRejected()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "cards.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        var effect = document["cards"]![0]!["effects"]![0]!;
        effect["operation"] = "multiply";
        effect["unit"] = "percent";
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC030 cards.json", StringComparison.Ordinal));
    }

    [Fact]
    public void UndefinedCardEvaluationOrderIsRejected()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "cards.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["cards"]![0]!["effects"]![0]!["order"] = 999;
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC029 cards.json", StringComparison.Ordinal));
    }

    [Fact]
    public void ResolvedStackingWithUnknownEvidenceIsRejected()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "cards.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["cards"]![0]!["effects"]![0]!["stackingProvenance"]!["status"] = "unknown";
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC031 cards.json", StringComparison.Ordinal));
    }

    [Fact]
    public void UnresolvedStackingCaseCannotAssertAFormulaOnItsEffect()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "cards.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["stackingCases"]![0]!["resolution"] = "unresolved";
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC032 cards.json", StringComparison.Ordinal));
    }

    [Fact]
    public void KnownSourceOmissionCannotBeUsedAsCorroboration()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "cards.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["sourceExclusions"]!.AsArray().Add(new JsonObject
        {
            ["source"] = "source-one",
            ["cardId"] = "fixture-card-0",
            ["fact"] = "effect:fixture-effect",
            ["reason"] = "Fixture source omits this fact.",
        });
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC037 cards.json", StringComparison.Ordinal));
    }

    [Fact]
    public void KnownSourceUnitConflictCannotBeUsedAsCorroboration()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "cards.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["sourceExclusions"]!.AsArray().Add(new JsonObject
        {
            ["source"] = "source-one",
            ["cardId"] = "fixture-card-0",
            ["fact"] = "effect:fixture-effect",
            ["reason"] = "Fixture source reports flat points rather than the catalog's percentage unit.",
        });
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC037 cards.json", StringComparison.Ordinal));
    }

    [Fact]
    public void UnsupportedMapPrimitiveIsRejectedBySchema()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "maps.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["maps"]![0]!["primitives"]![0]!["primitive"] = "triangle";
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC001 maps.json", StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownMapSourceIsRejected()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "maps.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["maps"]![0]!["visualEvidence"]!["source"] = "missing-source";
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC055 maps.json", StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateMapIdIsRejected()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "maps.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["maps"]![1]!["id"] = document["maps"]![0]!["id"]!.GetValue<string>();
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC041 maps.json", StringComparison.Ordinal));
    }

    [Fact]
    public void UnsafeMapSpawnSeparationIsRejected()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "maps.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["maps"]![0]!["spawnRegions"]![1]!["xMin"] = -1.6;
        document["maps"]![0]!["spawnRegions"]![1]!["xMax"] = -0.4;
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC050 maps.json", StringComparison.Ordinal));
    }

    [Fact]
    public void EntireMapSpawnRegionMustFitItsSupport()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "maps.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["maps"]![0]!["spawnRegions"]![0]!["xMin"] = -16;
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC048 maps.json", StringComparison.Ordinal));
    }

    [Fact]
    public void TwoIndependentMapIndexesAreRequired()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "maps.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["reconciliation"]![2]!["source"] = "source-one";
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC060 maps.json", StringComparison.Ordinal));
    }

    [Fact]
    public void UnboundedMapCoordinateIsRejected()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "maps.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["maps"]![0]!["primitives"]![0]!["x"] = 41;
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC056 maps.json", StringComparison.Ordinal));
    }

    [Fact]
    public void RepresentativeMapMustExhibitItsNamedCategory()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "maps.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["representativeExamples"]!["ringOut"] = "fixture-map-0";
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC057 maps.json", StringComparison.Ordinal));
    }

    [Fact]
    public void SubThresholdMapLayoutEvidenceIsRejected()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "maps.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["maps"]![0]!["layoutEvidence"]!["intersectionCells"] = 100;
        document["maps"]![0]!["layoutEvidence"]!["unionCells"] = 300;
        document["maps"]![0]!["layoutEvidence"]!["coarseIntersectionOverUnion"] = 0.333333;
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC001 maps.json", StringComparison.Ordinal));
    }

    [Fact]
    public void InconsistentMapLayoutEvidenceIsRejected()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "maps.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["maps"]![0]!["layoutEvidence"]!["intersectionCells"] = 200;
        document["maps"]![0]!["layoutEvidence"]!["unionCells"] = 208;
        document["maps"]![0]!["layoutEvidence"]!["coarseIntersectionOverUnion"] = 0.99;
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC058 maps.json", StringComparison.Ordinal));
    }

    [Fact]
    public void MapGeometryRenderDriftIsRejected()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "maps.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["maps"]![0]!["primitives"]![0]!["x"] = 1;
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC059 maps.json", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingSourceComponentRepresentationIsRejected()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "maps.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["maps"]![0]!["layoutEvidence"]!["sourceComponentCount"] = 2;
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC061 maps.json", StringComparison.Ordinal));
    }

    [Fact]
    public void PixelTraceScalePrimitiveCountIsRejected()
    {
        CreateValidRepository();
        var path = Path.Combine(_repository, "spec", "maps.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        var primitives = document["maps"]![0]!["primitives"]!.AsArray();
        for (var index = 2; index <= 97; index++)
        {
            var primitive = primitives[0]!.DeepClone();
            primitive["id"] = $"platform-{index:000}";
            primitives.Add(primitive);
        }
        File.WriteAllText(path, document.ToJsonString());

        var failures = SpecChecker.CheckRepository(_repository);

        Assert.Contains(failures, failure => failure.StartsWith("SPEC061 maps.json", StringComparison.Ordinal));
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
              }, {
                "id": "source-two",
                "title": "Source two",
                "publisher": "Publisher",
                "url": "https://example.test/source-two",
                "accessedOn": "2026-08-14",
                "kind": "runtime-observation",
                "scope": "Second test fixture source.",
                "reliability": "high"
              }]
            }
            """);

        WriteCards();

        foreach (var kind in new[] { "match", "controls", "player", "combat", "camera" })
        {
            WriteMechanics(kind, $"{kind}-fact", "source-one");
        }

        WriteMeasurements("player-fact");
        WriteMaps();
    }

    private void WriteCards()
    {
        static JsonObject Provenance() => new()
        {
            ["status"] = "confirmed",
            ["confidence"] = "high",
            ["method"] = "Fixture method.",
            ["tolerance"] = "Exact fixture value.",
            ["sources"] = new JsonArray("source-one", "source-two"),
        };

        var cards = new JsonArray();
        for (var index = 0; index < 67; index++)
        {
            cards.Add(new JsonObject
            {
                ["id"] = $"fixture-card-{index}",
                ["originalName"] = $"Fixture Card {index}",
                ["rarity"] = "common",
                ["draftAvailability"] = "available",
                ["implementationTier"] = "stat-only",
                ["metadataProvenance"] = Provenance(),
                ["behavior"] = new JsonObject
                {
                    ["summary"] = "Fixture behavior.",
                    ["hook"] = "passive",
                    ["provenance"] = Provenance(),
                },
                ["effects"] = new JsonArray(new JsonObject
                {
                    ["id"] = "fixture-effect",
                    ["target"] = "weapon.damage",
                    ["operation"] = "add-percent",
                    ["value"] = 1,
                    ["unit"] = "percent",
                    ["stacking"] = "additive",
                    ["order"] = 20,
                    ["cap"] = "none-observed",
                    ["provenance"] = Provenance(),
                    ["stackingProvenance"] = Provenance(),
                }),
                ["unknowns"] = new JsonArray("Fixture unknown."),
            });
        }

        var fixtureEffects = cards[0]!["effects"]!.AsArray();
        foreach (var (effectId, operation, unit, stacking) in new[]
        {
            ("multiplicative-effect", "multiply", "factor", "multiplicative"),
            ("count-effect", "add-count", "count", "count"),
            ("max-wins-effect", "register-hook", "presence", "max-wins"),
            ("hook-effect", "register-hook", "presence", "hook-per-copy"),
        })
        {
            fixtureEffects.Add(new JsonObject
            {
                ["id"] = effectId,
                ["target"] = "weapon.damage",
                ["operation"] = operation,
                ["value"] = 1,
                ["unit"] = unit,
                ["stacking"] = stacking,
                ["order"] = 20,
                ["cap"] = "unknown",
                ["provenance"] = Provenance(),
                ["stackingProvenance"] = Provenance(),
            });
        }

        var stackingCases = new JsonArray();
        foreach (var stackingOperator in new[] { "additive", "multiplicative", "count", "max-wins", "hook" })
        {
            var effectId = stackingOperator switch
            {
                "additive" => "fixture-effect",
                "multiplicative" => "multiplicative-effect",
                "count" => "count-effect",
                "max-wins" => "max-wins-effect",
                _ => "hook-effect",
            };
            stackingCases.Add(new JsonObject
            {
                ["operator"] = stackingOperator,
                ["resolution"] = "confirmed",
                ["cardId"] = "fixture-card-0",
                ["effectId"] = effectId,
                ["rule"] = "Fixture stacking rule.",
                ["provenance"] = Provenance(),
            });
        }

        var document = new JsonObject
        {
            ["$schema"] = "./schema/cards.schema.json",
            ["schemaVersion"] = 1,
            ["targetBuild"] = "21020021",
            ["targetVersion"] = "v1.1.2.a75ee335a",
            ["catalogCount"] = 67,
            ["reconciliation"] = new JsonArray(
                new JsonObject { ["source"] = "source-one", ["reportedCount"] = 67, ["relationship"] = "exact", ["notes"] = "Fixture exact count." },
                new JsonObject { ["source"] = "source-two", ["reportedCount"] = 65, ["relationship"] = "lower-bound", ["notes"] = "Fixture lower bound." },
                new JsonObject { ["source"] = "source-one", ["reportedCount"] = 1, ["relationship"] = "observed-subset", ["notes"] = "Fixture observation." }),
            ["patchHistory"] = new JsonArray(
                new JsonObject { ["version"] = "fixture-one", ["date"] = "2026-08-14", ["source"] = "source-one", ["scope"] = "Fixture patch.", ["binding"] = "Fixture binding." },
                new JsonObject { ["version"] = "fixture-two", ["date"] = "2026-08-14", ["source"] = "source-two", ["scope"] = "Fixture patch.", ["binding"] = "Fixture binding." },
                new JsonObject { ["version"] = "fixture-three", ["date"] = "2026-08-14", ["source"] = "source-one", ["scope"] = "Fixture patch.", ["binding"] = "Fixture binding." }),
            ["evaluationOrder"] = new JsonArray(
                new JsonObject { ["order"] = 20, ["phase"] = "fixture-phase", ["rule"] = "Fixture evaluation phase." }),
            ["sourceExclusions"] = new JsonArray(),
            ["stackingCases"] = stackingCases,
            ["cards"] = cards,
        };

        File.WriteAllText(Path.Combine(_repository, "spec", "cards.json"), document.ToJsonString());
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
                "pixelMeasurements": { "startCenterX": 0, "endCenterX": 1, "distancePixels": 1 },
                "derivation": { "operation": "identity", "operands": ["pixelMeasurements.distancePixels"] },
                "normalizedValue": 1,
                "unit": "fixture units",
                "tolerance": 0,
                "confidence": "high",
                "method": "Fixture method."
              }]
            }
            """);
    }

    private void WriteMaps()
    {
        static JsonObject Bounds(double xMin, double xMax, double yMin, double yMax) => new()
        {
            ["xMin"] = xMin,
            ["xMax"] = xMax,
            ["yMin"] = yMin,
            ["yMax"] = yMax,
        };

        static JsonObject Provenance() => new()
        {
            ["status"] = "provisional",
            ["confidence"] = "low",
            ["method"] = "Fixture vectorization.",
            ["tolerance"] = "Fixture tolerance.",
            ["sources"] = new JsonArray("source-one"),
        };

        var maps = new JsonArray();
        for (var index = 0; index < 70; index++)
        {
            maps.Add(new JsonObject
            {
                ["id"] = $"fixture-map-{index}",
                ["sourceRow"] = index + 2,
                ["visualEvidence"] = new JsonObject
                {
                    ["source"] = "source-one",
                    ["previewSha256"] = new string('0', 64),
                    ["previewPixels"] = new JsonObject { ["width"] = 640, ["height"] = 360 },
                    ["rowAnchorMethod"] = "xlsx-drawing-one-cell-anchor",
                },
                ["layoutEvidence"] = new JsonObject
                {
                    ["threshold"] = "max-rgb-greater-than-or-equal-to-24",
                    ["maskSha256"] = new string('0', 64),
                    ["sourceComponentCount"] = 1,
                    ["coarseGridPixels"] = new JsonObject { ["width"] = 8, ["height"] = 8 },
                    ["sourceOccupiedCells"] = 204,
                    ["renderedOccupiedCells"] = 204,
                    ["renderedMaskSha256"] = "1683a9c57a22db7b23b964fe23ecaa2c1c65a1734ca50485cfdfb927c48cb0b0",
                    ["intersectionCells"] = 204,
                    ["unionCells"] = 204,
                    ["coarseIntersectionOverUnion"] = 1,
                },
                ["archetype"] = "visible-platform-layout",
                ["symmetry"] = "mirror",
                ["ringOutFocused"] = false,
                ["cameraBounds"] = Bounds(-18, 18, -10, 10),
                ["collisionBounds"] = Bounds(-15, 15, -0.5, 0.5),
                ["killBoundaryY"] = -12,
                ["spawnRegions"] = new JsonArray(
                    new JsonObject { ["id"] = "spawn-left", ["xMin"] = -8.6, ["xMax"] = -7.4, ["yMin"] = 0.75, ["yMax"] = 1.25, ["clearanceDiameters"] = 1, ["supportPrimitiveId"] = "platform-001" },
                    new JsonObject { ["id"] = "spawn-right", ["xMin"] = 7.4, ["xMax"] = 8.6, ["yMin"] = 0.75, ["yMax"] = 1.25, ["clearanceDiameters"] = 1, ["supportPrimitiveId"] = "platform-001" }),
                ["primitives"] = new JsonArray(
                    new JsonObject { ["id"] = "platform-001", ["primitive"] = "oriented-box", ["role"] = "static", ["x"] = 0, ["y"] = 0, ["width"] = 30, ["height"] = 1, ["rotationDegrees"] = 0 }),
                ["behaviorModules"] = new JsonArray(),
                ["provenance"] = Provenance(),
                ["unknowns"] = new JsonArray("Fixture unknown."),
            });
        }

        maps[1]!["behaviorModules"] = new JsonArray(
            new JsonObject { ["id"] = "moving-001", ["kind"] = "moving-assembly", ["bounds"] = Bounds(-2, 2, -1, 1), ["evidenceStatus"] = "visual-candidate", ["timingStatus"] = "unknown" });
        maps[2]!["behaviorModules"] = new JsonArray(
            new JsonObject { ["id"] = "breakable-001", ["kind"] = "breakable-field", ["bounds"] = Bounds(-2, 2, -1, 1), ["evidenceStatus"] = "visual-candidate", ["timingStatus"] = "unknown" });
        maps[3]!["behaviorModules"] = new JsonArray(
            new JsonObject { ["id"] = "saw-001", ["kind"] = "radial-saw", ["x"] = 0, ["y"] = 5, ["radius"] = 1, ["evidenceStatus"] = "visible", ["timingStatus"] = "unknown" });
        maps[4]!["symmetry"] = "asymmetric";
        maps[5]!["ringOutFocused"] = true;

        var vocabulary = new JsonArray();
        foreach (var id in new[] { "oriented-box", "radial-saw", "breakable-field", "moving-assembly", "physics-assembly" })
        {
            vocabulary.Add(new JsonObject { ["id"] = id, ["purpose"] = "Fixture vocabulary entry." });
        }

        var document = new JsonObject
        {
            ["$schema"] = "./schema/maps.schema.json",
            ["schemaVersion"] = 3,
            ["targetBuild"] = "21020021",
            ["targetVersion"] = "v1.1.2.a75ee335a",
            ["catalogCount"] = 70,
            ["units"] = new JsonObject { ["distance"] = "player-diameters", ["time"] = "60-hz-ticks", ["sourcePlayerDiameterPixels"] = 18, ["sourceMaskPixels"] = new JsonObject { ["width"] = 640, ["height"] = 360 } },
            ["geometryVocabulary"] = vocabulary,
            ["reconciliation"] = new JsonArray(
                new JsonObject { ["source"] = "source-one", ["reportedCount"] = 70, ["relationship"] = "lower-bound", ["notes"] = "Fixture lower bound." },
                new JsonObject { ["source"] = "source-one", ["reportedCount"] = 70, ["relationship"] = "exact-preview-index", ["notes"] = "Fixture preview index." },
                new JsonObject { ["source"] = "source-two", ["reportedCount"] = 6, ["relationship"] = "historical-removed-subset", ["notes"] = "Fixture removed subset." },
                new JsonObject { ["source"] = "source-one", ["reportedCount"] = 0, ["relationship"] = "observed-subset", ["notes"] = "Fixture subset." }),
            ["representativeExamples"] = new JsonObject
            {
                ["static"] = "fixture-map-0",
                ["movingCandidate"] = "fixture-map-1",
                ["breakableCandidate"] = "fixture-map-2",
                ["hazard"] = "fixture-map-3",
                ["asymmetric"] = "fixture-map-4",
                ["ringOut"] = "fixture-map-5",
            },
            ["maps"] = maps,
        };

        File.WriteAllText(Path.Combine(_repository, "spec", "maps.json"), document.ToJsonString());
    }
}
