namespace Rounds.Checks;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--capture-evidence")
        {
            if (args is not ["--capture-evidence", var manifestPath])
            {
                Console.Error.WriteLine("usage: Rounds.Checks --capture-evidence <external-manifest.json>");
                return 2;
            }

            string json;
            try
            {
                json = File.ReadAllText(manifestPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"capture evidence could not be read: {exception.Message}");
                return 2;
            }

            var captureFailures = CaptureEvidenceValidator.Validate(json);
            if (captureFailures.Count == 0)
            {
                Console.WriteLine("capture evidence passed");
                return 0;
            }

            foreach (var failure in captureFailures) Console.Error.WriteLine(failure);
            return 1;
        }

        var repository = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        var failures = DeterminismBoundaryChecker.CheckSimulation(
                Path.Combine(repository, "src", "Rounds.Sim"))
            .Concat(SpecChecker.CheckRepository(repository))
            .Concat(ProductIdentityChecker.CheckRepository(repository))
            .ToArray();
        if (failures.Length > 0)
        {
            foreach (var failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            return 1;
        }

        Console.WriteLine("repository checks passed");
        return 0;
    }
}
