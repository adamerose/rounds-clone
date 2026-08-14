using Rounds.Checks;

namespace Rounds.Sim.Tests;

public sealed class DeterminismBoundaryCheckerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"rounds-check-{Guid.NewGuid():N}");

    [Fact]
    public void EachLockedRuleRejectsARepresentativeViolation()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Math"));
        File.WriteAllText(Path.Combine(_root, "Bad.cs"), """
            using Godot;
            using static System.Math;
            class Bad
            {
                float value;
                Dictionary<int, int> values = new();
                int sample = Random.Shared.Next();
                long tick = Environment.TickCount64;
                double Curve(double x) => Sin(x);
                void Queue() => ThreadPool.QueueUserWorkItem(_ => { });
            }
            """);

        var failures = DeterminismBoundaryChecker.CheckSimulation(_root);

        Assert.Equal(
            ["DET001", "DET002", "DET003", "DET004", "DET005", "DET006", "DET007"],
            failures.Select(failure => failure[..6]));
    }

    [Fact]
    public void AliasedUnpinnedMathIsRejected()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(
            Path.Combine(_root, "Alias.cs"),
            "using M = System.Math; class Alias { double Curve(double x) => M.Sin(x); }");

        var failure = Assert.Single(DeterminismBoundaryChecker.CheckSimulation(_root));
        Assert.StartsWith("DET003", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void DedicatedTrigFileOwnsTheUnpinnedMathBoundary()
    {
        var math = Path.Combine(_root, "Math");
        Directory.CreateDirectory(math);
        File.WriteAllText(
            Path.Combine(math, "Trig.cs"),
            "using static System.Math; class Trig { double Curve(double x) => Sin(x); }");

        Assert.Empty(DeterminismBoundaryChecker.CheckSimulation(_root));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
