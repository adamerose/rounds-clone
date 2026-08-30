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

internal static class EvidenceLauncherArchitecture
{
    internal const int RequiredPointerSize = 8;
    internal const string Refusal = "unsupported-architecture-x64-required";

    internal static string? RefusalForPointerSize(int pointerSize) =>
        pointerSize == RequiredPointerSize ? null : Refusal;
}

internal static class EvidenceLauncherEntry
{
    internal static int Run(
        IReadOnlyList<string> arguments,
        int pointerSize,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(error);

        var architectureRefusal = EvidenceLauncherArchitecture.RefusalForPointerSize(pointerSize);
        if (architectureRefusal is not null)
        {
            error.Write(architectureRefusal + "\n");
            return 2;
        }

        var command = EvidenceLauncherCommand.Parse(arguments);
        if (!command.Accepted)
        {
            error.Write(command.Refusal + "\n");
            return 2;
        }

        // The executable is intentionally inert until the native fact collector and
        // Win32 boundary are independently reviewed. Unit tests drive the coordinator
        // only through injected fakes; merely starting this tool cannot launch a child.
        error.Write("native-boundary-not-installed\n");
        return 2;
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
    EvidenceCandidateIdentity Candidate,
    EvidenceOpenedExecutableIdentity MsBuild,
    EvidenceOpenedExecutableIdentity BuildProcessImage,
    EvidenceRuntimeAssemblyIdentity RuntimeAssembly,
    bool ZeroWarnings,
    bool DeletedPriorOutput);

internal sealed record EvidenceCandidateIdentity(
    string RepositoryRoot,
    string Commit,
    bool CleanHead,
    bool IdentityBound,
    string RepositoryHandleIdentity);

internal sealed record EvidenceOpenedExecutableIdentity(
    string Path,
    bool Exists,
    bool IdentityBound,
    bool IsReparsePoint,
    string OpenedHandleIdentity,
    string Sha256,
    string FileVersion,
    string ProductVersion);

internal sealed record EvidenceRuntimeAssemblyIdentity(
    string Path,
    bool Exists,
    bool IdentityBound,
    bool IsReparsePoint,
    bool RecreatedByImmediateRebuild,
    string OpenedHandleIdentity,
    string Sha256,
    string Mvid);

internal interface IEvidenceBuildDriver
{
    IEvidenceExecutableLease OpenMsBuildExecutable(EvidenceBuildInvocation required);

    EvidenceBuildAttestation RebuildAndAttest(
        EvidenceBuildInvocation required,
        IEvidenceExecutableLease msBuildExecutable);
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

internal interface IEvidenceExecutableLease : IDisposable
{
    EvidenceOpenedExecutableIdentity Identity { get; }
}

internal interface IEvidenceLaunchHandleLease : IDisposable
{
    IReadOnlyList<EvidenceChildHandleDescriptor> ChildHandles { get; }

    bool ParentEndpointsAreNonInheritable { get; }

    string AcknowledgementReadHandleValue { get; }

    void CompleteSuccessfulProcessCreation();

    void WriteAcknowledgementAndClose(byte value);
}

internal abstract class EvidenceProcessLease : IDisposable
{
    private bool _disposed;
    private bool _assignedToKillOnCloseJob;
    private bool _terminationFallbackDisarmed;

    public void MarkAssignedToKillOnCloseJob()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_assignedToKillOnCloseJob)
        {
            throw new InvalidOperationException("Process termination ownership was already transferred.");
        }
        _assignedToKillOnCloseJob = true;
        OnTerminationTransferredToJob();
    }

    public void DisarmTerminationFallbackAfterVerifiedExitAndEmptyJob()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_assignedToKillOnCloseJob)
        {
            throw new InvalidOperationException("Process was not assigned to the kill-on-close job.");
        }
        _terminationFallbackDisarmed = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        try
        {
            if (!_terminationFallbackDisarmed)
            {
                TerminateAndWaitForExit();
            }
        }
        finally
        {
            ReleaseProcessAndThreadHandles();
        }
    }

    protected abstract void TerminateAndWaitForExit();

    protected virtual void OnTerminationTransferredToJob()
    {
    }

    protected abstract void ReleaseProcessAndThreadHandles();
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
    IReadOnlyList<EvidenceAncestorIdentityFacts> OutputAncestors,
    EvidenceCandidateIdentity Candidate,
    EvidenceOpenedExecutableIdentity Godot,
    EvidenceRuntimeAssemblyIdentity RuntimeAssembly);

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

internal interface IEvidenceForegroundObserverLease : IDisposable
{
    bool StopAndReadSawJobWindow();
}

internal interface IEvidenceNativeBoundary
{
    T RunOnDedicatedWorker<T>(Func<T> operation);

    string ReadInputDesktopIdentity();

    EvidenceNativePreflight RevalidatePreflight(BaseProjectileEvidenceLaunchPlan plan);

    IEvidenceExecutableLease OpenGodotExecutable(BaseProjectileEvidenceLaunchPlan plan);

    IEvidenceDesktopLease CreatePrivateDesktop(string name);

    IEvidenceLaunchHandleLease CreateHandleAllowlist();

    EvidenceProcessLease CreateSuspendedProcess(
        BaseProjectileEvidenceLaunchPlan plan,
        IEvidenceDesktopLease desktop,
        IEvidenceLaunchHandleLease handles,
        IEvidenceExecutableLease executable,
        EvidenceCreateProcessContract contract);

    EvidenceOpenedExecutableIdentity ReadSuspendedProcessImageIdentity(
        EvidenceProcessLease process);

    IEvidenceJobLease CreateJob();

    void ConfigureJob(IEvidenceJobLease job, BaseProjectileEvidenceJobLimits limits);

    void AssignProcess(IEvidenceJobLease job, EvidenceProcessLease process);

    IEvidenceForegroundObserverLease StartForegroundObserver(IEvidenceJobLease job);

    EvidenceDeadlineToken ResumePrimaryThreadAndStartDeadline(
        EvidenceProcessLease process,
        TimeSpan deadline);

    EvidenceProtocolCapture CaptureProtocol(
        EvidenceProcessLease process,
        EvidenceDeadlineToken deadline,
        int standardOutputCapBytes,
        int standardErrorCapBytes);

    EvidencePublishedFrameValidation ValidatePublishedFrame(
        BaseProjectileEvidenceLaunchPlan plan,
        DebugBaseProjectileEvidenceAttestation attestation);

    EvidenceProcessTermination WaitForProcessExit(
        EvidenceProcessLease process,
        EvidenceDeadlineToken deadline);

    bool WaitForEmptyJob(IEvidenceJobLease job, EvidenceDeadlineToken deadline);

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
        EvidenceBuildAttestation? attestation = null;
        IEvidenceExecutableLease? msBuildExecutable = null;
        EvidenceOpenedExecutableIdentity? msBuildLeaseIdentity = null;
        try
        {
            msBuildExecutable = build.OpenMsBuildExecutable(requiredBuild);
            msBuildLeaseIdentity = msBuildExecutable.Identity;
            attestation = build.RebuildAndAttest(requiredBuild, msBuildExecutable);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return EvidenceLaunchResult.Failed("build-driver");
        }
        finally
        {
            try
            {
                msBuildExecutable?.Dispose();
            }
            catch (Exception)
            {
                // A build lease cleanup failure leaves attribution untrustworthy.
                attestation = null;
            }
        }
        if (attestation is null || msBuildLeaseIdentity is null ||
            !BuildMatches(plan, requiredBuild, msBuildLeaseIdentity, attestation))
        {
            return EvidenceLaunchResult.Failed("build-attribution");
        }

        var executionState = new EvidenceExecutionState();
        try
        {
            var result = native.RunOnDedicatedWorker(() => ExecuteNative(plan, attestation, executionState));
            return executionState.CleanupFailures.Count == 0
                ? result
                : EvidenceLaunchResult.Failed("cleanup", ResidueFor(executionState, plan));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return EvidenceLaunchResult.Failed(
                executionState.CleanupFailures.Count == 0 ? "native-boundary" : "cleanup",
                ResidueFor(executionState, plan));
        }
    }

    private EvidenceLaunchResult ExecuteNative(
        BaseProjectileEvidenceLaunchPlan plan,
        EvidenceBuildAttestation buildAttestation,
        EvidenceExecutionState executionState)
    {
        IEvidenceDesktopLease? desktop = null;
        IEvidenceLaunchHandleLease? handles = null;
        IEvidenceExecutableLease? executable = null;
        EvidenceProcessLease? process = null;
        IEvidenceJobLease? job = null;
        IEvidenceForegroundObserverLease? foregroundObserver = null;
        try
        {
            var inputDesktopBefore = native.ReadInputDesktopIdentity();
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
            executable = native.OpenGodotExecutable(plan);

            // This identity/topology snapshot is deliberately the final operation
            // before direct suspended process creation.
            var preflight = native.RevalidatePreflight(plan);
            if (!PreflightMatches(plan, buildAttestation, executable, inputDesktopBefore, preflight))
            {
                return EvidenceLaunchResult.Failed("preflight");
            }

            var processContract = new EvidenceCreateProcessContract(
                RequiredProcessFlags,
                plan.Desktop,
                plan.CommandLine,
                CreateProcessEnvironment(plan, handles),
                UseShell: false,
                Array.AsReadOnly(RequiredHandles));
            process = native.CreateSuspendedProcess(plan, desktop, handles, executable, processContract);
            handles.CompleteSuccessfulProcessCreation();
            if (native.ReadSuspendedProcessImageIdentity(process) != executable.Identity)
            {
                return EvidenceLaunchResult.Failed("child-image-identity");
            }
            job = native.CreateJob();
            native.ConfigureJob(job, plan.JobLimits);
            native.AssignProcess(job, process);
            process.MarkAssignedToKillOnCloseJob();
            foregroundObserver = native.StartForegroundObserver(job);
            var deadline = native.ResumePrimaryThreadAndStartDeadline(process, plan.Deadline);
            executionState.Resumed = true;

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
            executionState.AcknowledgementMayHaveReleasedLeases = true;
            handles.WriteAcknowledgementAndClose(DebugEvidenceCaptureProtocol.EvidenceAcknowledgement);

            var termination = native.WaitForProcessExit(process, deadline);
            executionState.TerminationObserved = termination.Exited && !termination.Forced;
            if (!termination.Exited || termination.Forced || termination.ExitCode != 0)
            {
                return EvidenceLaunchResult.Failed(
                    "process-exit",
                    plan.OutputRoot);
            }
            if (!native.WaitForEmptyJob(job, deadline))
            {
                return ForcedFailure("job-not-empty", plan);
            }
            process.DisarmTerminationFallbackAfterVerifiedExitAndEmptyJob();
            if (foregroundObserver.StopAndReadSawJobWindow())
            {
                return EvidenceLaunchResult.Failed("foreground-activation", plan.OutputRoot);
            }
            if (!string.Equals(
                    native.ReadInputDesktopIdentity(),
                    inputDesktopBefore,
                    StringComparison.Ordinal))
            {
                return EvidenceLaunchResult.Failed("input-desktop-changed", plan.OutputRoot);
            }
            return EvidenceLaunchResult.Passed();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return EvidenceLaunchResult.Failed(
                "native-boundary",
                executionState.AcknowledgementMayHaveReleasedLeases ||
                (executionState.Resumed && !executionState.TerminationObserved)
                    ? plan.OutputRoot
                    : null);
        }
        finally
        {
            // Closing the kill-on-close job is always the first cleanup after it exists.
            // No output cleanup operation is exposed by this boundary: resumed failures
            // with unproven ownership report the exact residue for attended recovery.
            DisposeAllBestEffort(
                executionState,
                job,
                foregroundObserver,
                process,
                handles,
                executable,
                desktop);
        }
    }

    private static EvidenceLaunchResult ForcedFailure(
        string code,
        BaseProjectileEvidenceLaunchPlan plan) =>
        EvidenceLaunchResult.Failed(code, plan.OutputRoot);

    private static bool BuildMatches(
        BaseProjectileEvidenceLaunchPlan plan,
        EvidenceBuildInvocation required,
        EvidenceOpenedExecutableIdentity msBuildLeaseIdentity,
        EvidenceBuildAttestation actual) =>
        InvocationMatches(required, actual.Invocation) &&
        ValidCandidate(plan, actual.Candidate) &&
        actual.MsBuild == msBuildLeaseIdentity &&
        ValidMsBuild(actual.MsBuild, required.Executable) &&
        actual.BuildProcessImage == actual.MsBuild &&
        ValidRuntimeAssembly(plan, actual.RuntimeAssembly) &&
        actual.DeletedPriorOutput && actual.ZeroWarnings;

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
        EvidenceBuildAttestation buildAttestation,
        IEvidenceExecutableLease executable,
        string inputDesktopBefore,
        EvidenceNativePreflight actual) =>
        string.Equals(inputDesktopBefore, plan.InputDesktopIdentity, StringComparison.Ordinal) &&
        string.Equals(actual.InputDesktopIdentity, inputDesktopBefore, StringComparison.Ordinal) &&
        string.Equals(actual.Monitor.DeviceName, BaseProjectileEvidenceLaunchPlanner.DisplayDevice, StringComparison.Ordinal) &&
        actual.Monitor.Ordinal == plan.Screen && actual.Monitor.PerMonitorV2DpiAware &&
        actual.Monitor.PhysicalBounds == plan.MonitorBounds &&
        actual.Monitor.PhysicalBounds.Contains(plan.WindowBounds) && actual.OutputRootAbsent &&
        actual.OutputAncestors.SequenceEqual(plan.OutputAncestors) &&
        actual.Candidate == buildAttestation.Candidate && ValidCandidate(plan, actual.Candidate) &&
        actual.Godot == executable.Identity && ValidGodot(plan, actual.Godot) &&
        actual.RuntimeAssembly == buildAttestation.RuntimeAssembly &&
        ValidRuntimeAssembly(plan, actual.RuntimeAssembly);

    private static bool ValidHandles(IEvidenceLaunchHandleLease handles) =>
        handles.ParentEndpointsAreNonInheritable &&
        ulong.TryParse(
            handles.AcknowledgementReadHandleValue,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var acknowledgementHandle) &&
        acknowledgementHandle != 0 &&
        string.Equals(
            acknowledgementHandle.ToString(CultureInfo.InvariantCulture),
            handles.AcknowledgementReadHandleValue,
            StringComparison.Ordinal) &&
        handles.ChildHandles.Count == RequiredHandles.Length &&
        handles.ChildHandles.SequenceEqual(RequiredHandles);

    private static bool ValidCandidate(
        BaseProjectileEvidenceLaunchPlan plan,
        EvidenceCandidateIdentity candidate) =>
        string.Equals(candidate.RepositoryRoot, plan.RepositoryRoot, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(candidate.Commit, plan.CandidateCommit, StringComparison.Ordinal) &&
        candidate.CleanHead && candidate.IdentityBound &&
        !string.IsNullOrWhiteSpace(candidate.RepositoryHandleIdentity);

    private static bool ValidMsBuild(EvidenceOpenedExecutableIdentity executable, string expectedPath) =>
        ValidOpenedExecutable(
            executable,
            expectedPath,
            BaseProjectileEvidenceLaunchPlanner.MsBuildSha256,
            BaseProjectileEvidenceLaunchPlanner.MsBuildFileVersion,
            BaseProjectileEvidenceLaunchPlanner.MsBuildProductVersion);

    private static bool ValidGodot(
        BaseProjectileEvidenceLaunchPlan plan,
        EvidenceOpenedExecutableIdentity executable) =>
        ValidOpenedExecutable(
            executable,
            plan.Executable,
            BaseProjectileEvidenceLaunchPlanner.GodotSha256,
            BaseProjectileEvidenceLaunchPlanner.GodotFileVersion,
            BaseProjectileEvidenceLaunchPlanner.GodotVersion);

    private static bool ValidOpenedExecutable(
        EvidenceOpenedExecutableIdentity executable,
        string expectedPath,
        string expectedSha256,
        string expectedFileVersion,
        string expectedProductVersion) =>
        executable.Exists && executable.IdentityBound && !executable.IsReparsePoint &&
        !string.IsNullOrWhiteSpace(executable.OpenedHandleIdentity) &&
        string.Equals(executable.Path, expectedPath, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(executable.Sha256, expectedSha256, StringComparison.Ordinal) &&
        string.Equals(executable.FileVersion, expectedFileVersion, StringComparison.Ordinal) &&
        string.Equals(executable.ProductVersion, expectedProductVersion, StringComparison.Ordinal);

    private static bool ValidRuntimeAssembly(
        BaseProjectileEvidenceLaunchPlan plan,
        EvidenceRuntimeAssemblyIdentity assembly) =>
        assembly.Exists && assembly.IdentityBound && !assembly.IsReparsePoint &&
        assembly.RecreatedByImmediateRebuild &&
        !string.IsNullOrWhiteSpace(assembly.OpenedHandleIdentity) &&
        string.Equals(assembly.Path, plan.RuntimeAssemblyPath, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(assembly.Sha256, plan.RuntimeAssemblySha256, StringComparison.Ordinal) &&
        string.Equals(assembly.Mvid, plan.RuntimeAssemblyMvid, StringComparison.Ordinal);

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

    private static string? ResidueFor(
        EvidenceExecutionState state,
        BaseProjectileEvidenceLaunchPlan plan) =>
        state.AcknowledgementMayHaveReleasedLeases ||
        (state.Resumed && !state.TerminationObserved)
            ? plan.OutputRoot
            : null;

    private static void DisposeAllBestEffort(
        EvidenceExecutionState state,
        IEvidenceJobLease? job,
        IEvidenceForegroundObserverLease? foregroundObserver,
        EvidenceProcessLease? process,
        IEvidenceLaunchHandleLease? handles,
        IEvidenceExecutableLease? executable,
        IEvidenceDesktopLease? desktop)
    {
        try
        {
            DisposeOne(state, "job", job);
        }
        finally
        {
            try
            {
                DisposeOne(state, "foreground-observer", foregroundObserver);
            }
            finally
            {
                try
                {
                    DisposeOne(state, "process", process);
                }
                finally
                {
                    try
                    {
                        DisposeOne(state, "handles", handles);
                    }
                    finally
                    {
                        try
                        {
                            DisposeOne(state, "executable", executable);
                        }
                        finally
                        {
                            DisposeOne(state, "desktop", desktop);
                        }
                    }
                }
            }
        }
    }

    private static void DisposeOne(
        EvidenceExecutionState state,
        string name,
        IDisposable? disposable)
    {
        try
        {
            disposable?.Dispose();
        }
        catch (Exception)
        {
            state.CleanupFailures.Add(name);
        }
    }

    private sealed class EvidenceExecutionState
    {
        public bool Resumed { get; set; }

        public bool TerminationObserved { get; set; }

        public bool AcknowledgementMayHaveReleasedLeases { get; set; }

        public List<string> CleanupFailures { get; } = new();
    }
}
