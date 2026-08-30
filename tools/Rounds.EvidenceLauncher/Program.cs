namespace Rounds.EvidenceLauncher;

internal static class Program
{
    public static int Main(string[] arguments)
    {
        var command = EvidenceLauncherCommand.Parse(arguments);
        if (!command.Accepted)
        {
            Console.Error.Write(command.Refusal + "\n");
            return 2;
        }

        // The executable is intentionally inert until the native fact collector and
        // Win32 boundary are independently reviewed. Unit tests drive the coordinator
        // only through injected fakes; merely starting this tool cannot launch a child.
        Console.Error.Write("native-boundary-not-installed\n");
        return 2;
    }
}
