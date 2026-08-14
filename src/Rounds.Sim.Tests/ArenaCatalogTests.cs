using System.Text;
using Rounds.Sim.Maps;

namespace Rounds.Sim.Tests;

public sealed class ArenaCatalogTests
{
    [Fact]
    public void EmbeddedCatalogLoadsArenaSixInSourceOrder()
    {
        var catalog = ArenaCatalog.LoadEmbedded();
        var arena = catalog.GetRequired("arena-006");

        Assert.Equal(70, catalog.Arenas.Count);
        Assert.Equal(15, arena.StaticBoxes.Count);
        Assert.Equal(2, arena.Spawns.Count);
        Assert.Equal(Enumerable.Range(0, 15), arena.StaticBoxes.Select(box => box.SourceOrder));
        Assert.All(
            arena.Spawns,
            spawn => Assert.Contains(arena.StaticBoxes, box => box.Id == spawn.SupportPrimitiveId));
    }

    [Fact]
    public void VisualBehaviorGeometryNeverLoadsAsStaticCollision()
    {
        var catalog = ArenaCatalog.LoadEmbedded();

        Assert.Equal(9, catalog.GetRequired("arena-015").StaticBoxes.Count);
        Assert.Equal(12, catalog.GetRequired("arena-026").StaticBoxes.Count);
        Assert.DoesNotContain(catalog.GetRequired("arena-026").StaticBoxes, box => box.Id.StartsWith("moving-part", StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownArenaIsRejected()
    {
        var catalog = ArenaCatalog.LoadEmbedded();

        Assert.Throws<KeyNotFoundException>(() => catalog.GetRequired("arena-missing"));
    }

    [Fact]
    public void MalformedArenaBoundsAreRejected()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""
            {
              "targetBuild": "21020021",
              "catalogCount": 1,
              "maps": [{
                "id": "fixture",
                "cameraBounds": { "xMin": 1, "xMax": -1, "yMin": -1, "yMax": 1 },
                "collisionBounds": { "xMin": -1, "xMax": 1, "yMin": -1, "yMax": 1 },
                "killBoundaryY": -2,
                "spawnRegions": [
                  { "id": "left", "xMin": -1, "xMax": -0.5, "yMin": 0, "yMax": 0.5, "supportPrimitiveId": "floor" },
                  { "id": "right", "xMin": 0.5, "xMax": 1, "yMin": 0, "yMax": 0.5, "supportPrimitiveId": "floor" }
                ],
                "primitives": [{
                  "id": "floor", "primitive": "oriented-box", "role": "static",
                  "x": 0, "y": -0.5, "width": 2, "height": 1, "rotationDegrees": 0
                }]
              }]
            }
            """));

        Assert.Throws<InvalidDataException>(() => ArenaCatalog.Load(stream));
    }

    [Fact]
    public void UnsupportedSpawnReferenceIsRejected()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ValidCatalog.Replace(
            "\"supportPrimitiveId\": \"floor\"",
            "\"supportPrimitiveId\": \"missing\"",
            StringComparison.Ordinal)));

        Assert.Throws<InvalidDataException>(() => ArenaCatalog.Load(stream));
    }

    private const string ValidCatalog = """
        {
          "targetBuild": "21020021",
          "catalogCount": 1,
          "maps": [{
            "id": "fixture",
            "cameraBounds": { "xMin": -2, "xMax": 2, "yMin": -2, "yMax": 2 },
            "collisionBounds": { "xMin": -2, "xMax": 2, "yMin": -2, "yMax": 2 },
            "killBoundaryY": -3,
            "spawnRegions": [
              { "id": "left", "xMin": -1, "xMax": -0.5, "yMin": 0, "yMax": 0.5, "supportPrimitiveId": "floor" },
              { "id": "right", "xMin": 0.5, "xMax": 1, "yMin": 0, "yMax": 0.5, "supportPrimitiveId": "floor" }
            ],
            "primitives": [{
              "id": "floor", "primitive": "oriented-box", "role": "static",
              "x": 0, "y": -0.5, "width": 2, "height": 1, "rotationDegrees": 0
            }]
          }]
        }
        """;
}
