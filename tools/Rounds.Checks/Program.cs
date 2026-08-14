namespace Rounds.Checks;

internal static class Program
{
    public static int Main(string[] args)
    {
        var repository = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        var failures = DeterminismBoundaryChecker.CheckSimulation(
            Path.Combine(repository, "src", "Rounds.Sim"));
        if (failures.Count > 0)
        {
            foreach (var failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            return 1;
        }

        Console.WriteLine("determinism boundary check passed");
        return 0;
    }
}
