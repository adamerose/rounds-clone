using System.Collections.ObjectModel;
using System.Globalization;
using Rounds.Game;

namespace Rounds.EvidenceLauncher;

internal enum EvidenceLauncherMode
{
    Plan,
    Execute,
}

internal readonly record struct EvidenceLauncherCommand(
    EvidenceLauncherMode? Mode,
    string? Refusal)
{
    public bool Accepted => Mode.HasValue && Refusal is null;

    public static EvidenceLauncherCommand Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count != 1)
        {
            return new(null, "usage: Rounds.EvidenceLauncher plan|execute");
        }

        return arguments[0] switch
        {
            "plan" => new(EvidenceLauncherMode.Plan, null),
            "execute" => new(EvidenceLauncherMode.Execute, null),
            _ => new(null, "usage: Rounds.EvidenceLauncher plan|execute"),
        };
    }
}

internal static class EvidenceBuildContract
{
    internal static EvidenceBuildInvocation Create(BaseProjectileEvidenceLaunchPlan plan) =>
        new(
            BaseProjectileEvidenceLaunchPlanner.MsBuildPath,
            plan.RepositoryRoot,
            Array.AsReadOnly(new[]
            {
                @"game\Rounds.Game.csproj",
                "/t:Rebuild",
                "/p:Configuration=Debug",
                "/p:Restore=false",
                "/p:UseSharedCompilation=false",
                "/p:BuildProjectReferences=true",
                "/m:1",
                "/nr:false",
                "/v:minimal",
                "/warnaserror",
            }),
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["DOTNET_PROCESSOR_COUNT"] = "2",
                ["MSBUILDDISABLENODEREUSE"] = "1",
                ["MSBuildEnableWorkloadResolver"] = "false",
                ["MSBuildSDKsPath"] = plan.Environment["MSBuildSDKsPath"],
            }));
}

internal sealed record EvidenceBuildInvocation(
    string Executable,
    string WorkingDirectory,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment);

internal sealed record EvidenceBuildAttestation(
    EvidenceBuildInvocation Invocation,
    string CandidateCommit,
    bool DeletedPriorOutput,
    bool RecreatedRuntimeAssembly,
    bool ZeroWarnings,
    string RuntimeAssemblySha256,
    string RuntimeAssemblyMvid);

internal interface IEvidenceBuildDriver
{
    EvidenceBuildAttestation RebuildAndAttest(EvidenceBuildInvocation required);
}

[Flags]
internal enum EvidenceCreateProcessFlags : uint
{
    CreateSuspended = 0x00000004,
    CreateNewProcessGroup = 0x00000200,
    CreateUnicodeEnvironment = 0x00000400,
    CreateNoWindow = 0x08000000,
    BelowNormalPriorityClass = 0x00004000,
    ExtendedStartupInfoPresent = 0x00080000,
}

internal enum EvidenceChildHandle
{
    StandardInputRead,
    StandardOutputWrite,
    StandardErrorWrite,
    AcknowledgementRead,
}

internal sealed record EvidenceChildHandleDescriptor(
    EvidenceChildHandle Kind,
    bool Inheritable);

internal interface IEvidenceDesktopLease : IDisposable
{
    string Name { get; }
}

internal interface IEvidenceLaunchHandleLease : IDisposable
{
    IReadOnlyList<EvidenceChildHandleDescriptor> ChildHandles { get; }

    bool ParentEndpointsAreNonInheritable { get; }

    string AcknowledgementReadHandleValue { get; }

    void WriteAcknowledgementAndClose(byte value);
}

internal interface IEvidenceProcessLease : IDisposable
{
}

internal interface IEvidenceJobLease : IDisposable
{
}

internal sealed record EvidenceCreateProcessContract(
    EvidenceCreateProcessFlags Flags,
    string Desktop,
    string CommandLine,
    IReadOnlyDictionary<string, string> UnicodeEnvironment,
    bool UseShell,
    IReadOnlyList<EvidenceChildHandleDescriptor> InheritedHandles);

internal readonly record struct EvidenceDeadlineToken(long Value);

internal sealed record EvidenceNativePreflight(
    EvidenceMonitorFacts Monitor,
    string InputDesktopIdentity,
    bool OutputRootAbsent,
    IReadOnlyList<EvidenceAncestorIdentityFacts> OutputAncestors);

internal sealed record EvidenceProtocolCapture(
    string StandardOutput,
    string StandardError,
    bool TimedOut,
    bool StandardOutputCapExceeded,
    bool StandardErrorCapExceeded);

internal sealed record EvidencePublishedFrameValidation(
    string OutputRoot,
    string FrameName,
    string PngSha256,
    int Width,
    int Height,
    bool Rgba8,
    bool RootIdentityBound,
    bool FrameIdentityBound,
    bool RootLeaseObserved,
    bool FrameLeaseObserved,
    bool ContainsOnlyExpectedFrame);

internal sealed record EvidenceProcessTermination(
    bool Exited,
    int ExitCode,
    bool Forced);

internal interface IEvidenceNativeBoundary
{
    T RunOnDedicatedWorker<T>(Func<T> operation);

    string ReadInputDesktopIdentity();

    EvidenceNativePreflight RevalidatePreflight(BaseProjectileEvidenceLaunchPlan plan);

    IEvidenceDesktopLease CreatePrivateDesktop(string name);

    IEvidenceLaunchHandleLease CreateHandleAllowlist();

    IEvidenceProcessLease CreateSuspendedProcess(
        BaseProjectileEvidenceLaunchPlan plan,
        IEvidenceDesktopLease desktop,
        IEvidenceLaunchHandleLease handles,
        EvidenceCreateProcessContract contract);

    IEvidenceJobLease CreateJob();

    void ConfigureJob(IEvidenceJobLease job, BaseProjectileEvidenceJobLimits limits);

    void AssignProcess(IEvidenceJobLease job, IEvidenceProcessLease process);

    EvidenceDeadlineToken ResumePrimaryThreadAndStartDeadline(
        IEvidenceProcessLease process,
        TimeSpan deadline);

    EvidenceProtocolCapture CaptureProtocol(
        IEvidenceProcessLease process,
        EvidenceDeadlineToken deadline,
        int standardOutputCapBytes,
        int standardErrorCapBytes);

    EvidencePublishedFrameValidation ValidatePublishedFrame(
        BaseProjectileEvidenceLaunchPlan plan,
        DebugBaseProjectileEvidenceAttestation attestation);

    EvidenceProcessTermination WaitForProcessExit(
        IEvidenceProcessLease process,
        EvidenceDeadlineToken deadline);

    bool WaitForEmptyJob(IEvidenceJobLease job, EvidenceDeadlineToken deadline);

    bool ForegroundObserverSawJobWindow(IEvidenceJobLease job);
}

internal readonly record struct EvidenceLaunchResult(
    bool Success,
    string Code,
    string? PreservedUnprovenResidueRoot)
{
    internal static EvidenceLaunchResult Passed() => new(true, "success", null);

    internal static EvidenceLaunchResult Failed(string code, string? residue = null) =>
        new(false, code, residue);
}

internal sealed class EvidenceLaunchOrchestrator(
    IEvidenceBuildDriver build,
    IEvidenceNativeBoundary native)
{
    private const EvidenceCreateProcessFlags RequiredProcessFlags =
        EvidenceCreateProcessFlags.CreateSuspended |
        EvidenceCreateProcessFlags.CreateNoWindow |
        EvidenceCreateProcessFlags.CreateNewProcessGroup |
        EvidenceCreateProcessFlags.BelowNormalPriorityClass |
        EvidenceCreateProcessFlags.ExtendedStartupInfoPresent |
        EvidenceCreateProcessFlags.CreateUnicodeEnvironment;

    private static readonly EvidenceChildHandleDescriptor[] RequiredHandles =
    {
        new(EvidenceChildHandle.StandardInputRead, true),
        new(EvidenceChildHandle.StandardOutputWrite, true),
        new(EvidenceChildHandle.StandardErrorWrite, true),
        new(EvidenceChildHandle.AcknowledgementRead, true),
    };

    public EvidenceLaunchResult Execute(BaseProjectileEvidenceLaunchPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var requiredBuild = EvidenceBuildContract.Create(plan);
        EvidenceBuildAttestation attestation;
        try
        {
            attestation = build.RebuildAndAttest(requiredBuild);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return EvidenceLaunchResult.Failed("build-driver");
        }
        if (!BuildMatches(plan, requiredBuild, attestation))
        {
            return EvidenceLaunchResult.Failed("build-attribution");
        }

        try
        {
            return native.RunOnDedicatedWorker(() => ExecuteNative(plan));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return EvidenceLaunchResult.Failed("native-boundary");
        }
    }

    private EvidenceLaunchResult ExecuteNative(BaseProjectileEvidenceLaunchPlan plan)
    {
        IEvidenceDesktopLease? desktop = null;
        IEvidenceLaunchHandleLease? handles = null;
        IEvidenceProcessLease? process = null;
        IEvidenceJobLease? job = null;
        var resumed = false;
        var terminationObserved = false;
        try
        {
            var inputDesktopBefore = native.ReadInputDesktopIdentity();
            var preflight = native.RevalidatePreflight(plan);
            if (!PreflightMatches(plan, inputDesktopBefore, preflight))
            {
                return EvidenceLaunchResult.Failed("preflight");
            }

            desktop = native.CreatePrivateDesktop(plan.Desktop);
            if (!string.Equals(desktop.Name, plan.Desktop, StringComparison.Ordinal))
            {
                return EvidenceLaunchResult.Failed("private-desktop");
            }
            handles = native.CreateHandleAllowlist();
            if (!ValidHandles(handles))
            {
                return EvidenceLaunchResult.Failed("handle-allowlist");
            }

            var processContract = new EvidenceCreateProcessContract(
                RequiredProcessFlags,
                plan.Desktop,
                plan.CommandLine,
                CreateProcessEnvironment(plan, handles),
                UseShell: false,
                Array.AsReadOnly(RequiredHandles));
            process = native.CreateSuspendedProcess(plan, desktop, handles, processContract);
            job = native.CreateJob();
            native.ConfigureJob(job, plan.JobLimits);
            native.AssignProcess(job, process);
            var deadline = native.ResumePrimaryThreadAndStartDeadline(process, plan.Deadline);
            resumed = true;

            var protocol = native.CaptureProtocol(
                process,
                deadline,
                plan.StandardOutputCapBytes,
                plan.StandardErrorCapBytes);
            if (protocol.TimedOut)
            {
                return ForcedFailure("deadline", plan);
            }
            if (protocol.StandardOutputCapExceeded || protocol.StandardErrorCapExceeded)
            {
                return ForcedFailure("pipe-cap", plan);
            }
            if (!BaseProjectileEvidenceLaunchPlanner.MarkerMatchesPlan(
                    protocol.StandardOutput,
                    plan,
                    out var marker))
            {
                return ForcedFailure("completion-marker", plan);
            }

            var frame = native.ValidatePublishedFrame(plan, marker);
            if (!ValidFrame(plan, marker, frame))
            {
                return ForcedFailure("parent-frame-validation", plan);
            }
            handles.WriteAcknowledgementAndClose(DebugEvidenceCaptureProtocol.EvidenceAcknowledgement);

            var termination = native.WaitForProcessExit(process, deadline);
            terminationObserved = termination.Exited && !termination.Forced;
            if (!termination.Exited || termination.Forced || termination.ExitCode != 0)
            {
                return EvidenceLaunchResult.Failed(
                    "process-exit",
                    termination.Forced || !termination.Exited ? plan.OutputRoot : null);
            }
            if (!native.WaitForEmptyJob(job, deadline))
            {
                return ForcedFailure("job-not-empty", plan);
            }
            if (native.ForegroundObserverSawJobWindow(job))
            {
                return EvidenceLaunchResult.Failed("foreground-activation");
            }
            if (!string.Equals(
                    native.ReadInputDesktopIdentity(),
                    inputDesktopBefore,
                    StringComparison.Ordinal))
            {
                return EvidenceLaunchResult.Failed("input-desktop-changed");
            }
            return EvidenceLaunchResult.Passed();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return EvidenceLaunchResult.Failed(
                "native-boundary",
                resumed && !terminationObserved ? plan.OutputRoot : null);
        }
        finally
        {
            // Closing the kill-on-close job is always the first cleanup after it exists.
            // No output cleanup operation is exposed by this boundary: resumed failures
            // with unproven ownership report the exact residue for attended recovery.
            job?.Dispose();
            process?.Dispose();
            handles?.Dispose();
            desktop?.Dispose();
        }
    }

    private static EvidenceLaunchResult ForcedFailure(
        string code,
        BaseProjectileEvidenceLaunchPlan plan) =>
        EvidenceLaunchResult.Failed(code, plan.OutputRoot);

    private static bool BuildMatches(
        BaseProjectileEvidenceLaunchPlan plan,
        EvidenceBuildInvocation required,
        EvidenceBuildAttestation actual) =>
        InvocationMatches(required, actual.Invocation) &&
        string.Equals(actual.CandidateCommit, plan.CandidateCommit, StringComparison.Ordinal) &&
        actual.DeletedPriorOutput && actual.RecreatedRuntimeAssembly && actual.ZeroWarnings &&
        string.Equals(actual.RuntimeAssemblySha256, plan.RuntimeAssemblySha256, StringComparison.Ordinal) &&
        string.Equals(actual.RuntimeAssemblyMvid, plan.RuntimeAssemblyMvid, StringComparison.Ordinal);

    private static bool InvocationMatches(EvidenceBuildInvocation expected, EvidenceBuildInvocation actual) =>
        string.Equals(expected.Executable, actual.Executable, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(expected.WorkingDirectory, actual.WorkingDirectory, StringComparison.OrdinalIgnoreCase) &&
        expected.Arguments.SequenceEqual(actual.Arguments, StringComparer.Ordinal) &&
        expected.Environment.Count == actual.Environment.Count &&
        expected.Environment.All(pair =>
            actual.Environment.TryGetValue(pair.Key, out var value) &&
            string.Equals(pair.Value, value, StringComparison.Ordinal));

    private static bool PreflightMatches(
        BaseProjectileEvidenceLaunchPlan plan,
        string inputDesktopBefore,
        EvidenceNativePreflight actual) =>
        string.Equals(inputDesktopBefore, plan.InputDesktopIdentity, StringComparison.Ordinal) &&
        string.Equals(actual.InputDesktopIdentity, inputDesktopBefore, StringComparison.Ordinal) &&
        string.Equals(actual.Monitor.DeviceName, BaseProjectileEvidenceLaunchPlanner.DisplayDevice, StringComparison.Ordinal) &&
        actual.Monitor.Ordinal == plan.Screen && actual.Monitor.PerMonitorV2DpiAware &&
        actual.Monitor.PhysicalBounds == plan.MonitorBounds &&
        actual.Monitor.PhysicalBounds.Contains(plan.WindowBounds) && actual.OutputRootAbsent &&
        actual.OutputAncestors.SequenceEqual(plan.OutputAncestors);

    private static bool ValidHandles(IEvidenceLaunchHandleLease handles) =>
        handles.ParentEndpointsAreNonInheritable &&
        ulong.TryParse(
            handles.AcknowledgementReadHandleValue,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var acknowledgementHandle) &&
        string.Equals(
            acknowledgementHandle.ToString(CultureInfo.InvariantCulture),
            handles.AcknowledgementReadHandleValue,
            StringComparison.Ordinal) &&
        handles.ChildHandles.Count == RequiredHandles.Length &&
        handles.ChildHandles.SequenceEqual(RequiredHandles);

    private static IReadOnlyDictionary<string, string> CreateProcessEnvironment(
        BaseProjectileEvidenceLaunchPlan plan,
        IEvidenceLaunchHandleLease handles)
    {
        var environment = new Dictionary<string, string>(plan.Environment, StringComparer.Ordinal)
        {
            [DebugEvidenceCaptureProtocol.EvidenceAckHandleEnvironmentVariable] =
                handles.AcknowledgementReadHandleValue,
        };
        return new ReadOnlyDictionary<string, string>(environment);
    }

    private static bool ValidFrame(
        BaseProjectileEvidenceLaunchPlan plan,
        DebugBaseProjectileEvidenceAttestation marker,
        EvidencePublishedFrameValidation frame) =>
        string.Equals(frame.OutputRoot, plan.OutputRoot, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(frame.FrameName, "frame-0000.png", StringComparison.Ordinal) &&
        string.Equals(frame.PngSha256, marker.PngSha256, StringComparison.Ordinal) &&
        frame.Width == DebugEvidenceCaptureProtocol.EvidenceViewportWidth &&
        frame.Height == DebugEvidenceCaptureProtocol.EvidenceViewportHeight && frame.Rgba8 &&
        frame.RootIdentityBound && frame.FrameIdentityBound &&
        frame.RootLeaseObserved && frame.FrameLeaseObserved && frame.ContainsOnlyExpectedFrame;
}
