namespace Rounds.EvidenceLauncher;

internal static class Program
{
    public static int Main(string[] arguments) =>
        EvidenceLauncherEntry.Run(arguments, IntPtr.Size, Console.Error);
}
