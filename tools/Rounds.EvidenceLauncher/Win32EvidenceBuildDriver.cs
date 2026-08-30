using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using Rounds.Game;

namespace Rounds.EvidenceLauncher;

internal sealed record EvidenceBuildInputIdentity(
    string Path,
    bool Exists,
    bool IdentityBound,
    bool ReparseFreeAncestors,
    string Sha256);

internal sealed record EvidenceBuildPackageIdentity(
    string PackageId,
    string Version,
    string CachePath,
    bool Exists,
    bool IdentityBound,
    bool ReparseFreeAncestors,
    string Sha256);

internal sealed record EvidenceBuildPrerequisiteAttestation(
    string GlobalJsonSdkVersion,
    string GlobalJsonSha256,
    bool RollForwardDisabled,
    bool AllowPrereleaseDisabled,
    bool GlobalJsonIdentityBound,
    string SdkDirectory,
    bool SdkDirectoryExists,
    bool SdkDirectoryIdentityBound,
    string ReferencePackDirectory,
    string ReferencePackVersion,
    bool ReferencePackDirectoryExists,
    bool ReferencePackDirectoryIdentityBound,
    IReadOnlyList<EvidenceBuildInputIdentity> RequiredInputs,
    IReadOnlyList<EvidenceBuildPackageIdentity> RequiredPackages);

internal sealed record EvidenceBuildOutputState(
    string Path,
    bool Exists,
    bool IdentityBound,
    bool IsReparsePoint,
    bool ReparseFreeAncestors,
    string OpenedHandleIdentity,
    long Length,
    long ChangeTime)
{
    internal static EvidenceBuildOutputState Missing(string path) =>
        new(path, false, false, false, false, string.Empty, 0, 0);
}

internal sealed record EvidenceBuildJobLimits(
    uint AffinityMask,
    int ActiveProcessLimit,
    long ProcessCommitBytes,
    long JobCommitBytes,
    bool KillOnJobClose);

internal sealed record EvidenceBuildProcessRequest(
    EvidenceBuildInvocation Invocation,
    IReadOnlyDictionary<string, string> EffectiveEnvironment,
    bool InheritAmbientEnvironment,
    bool StartSuspended,
    EvidenceBuildJobLimits JobLimits,
    bool UseShellExecute,
    bool CreateNoWindow,
    bool HiddenWindow,
    bool BelowNormalPriority,
    TimeSpan Deadline,
    int StandardOutputCapBytes,
    int StandardErrorCapBytes);

internal sealed record EvidenceBuildProcessResult(
    EvidenceOpenedExecutableIdentity ProcessImage,
    IReadOnlyDictionary<string, string> EffectiveEnvironment,
    int ExitCode,
    bool TimedOut,
    bool StandardOutputReachedEof,
    bool StandardErrorReachedEof,
    bool StandardOutputExceededCap,
    bool StandardErrorExceededCap,
    bool ProcessImageMatchedBeforeResume,
    bool AssignedToJobBeforeResume,
    bool PipesDrainedConcurrently,
    bool JobEmpty,
    bool WarningCountParsed,
    int WarningCount,
    byte[] StandardOutput,
    byte[] StandardError);

internal interface IEvidenceMsBuildExecutableFactory
{
    IEvidenceExecutableLease OpenPinnedMsBuild();
}

internal interface IEvidenceBuildRepositoryInspector
{
    EvidenceCandidateIdentity ReadCleanCandidate(string exactRepositoryRoot);
}

internal interface IEvidenceBuildPrerequisiteInspector
{
    EvidenceBuildPrerequisiteAttestation Read(string exactRepositoryRoot);
}

internal interface IEvidenceBuildEnvironmentFactory
{
    IReadOnlyDictionary<string, string> CreateSanitized(EvidenceBuildInvocation required);
}

internal interface IEvidenceBuildOutputApi
{
    EvidenceBuildOutputState Read(string exactRuntimeAssemblyPath);

    void Delete(string exactRuntimeAssemblyPath);
}

internal interface IEvidenceBuildProcessRunner
{
    EvidenceBuildProcessResult Run(
        EvidenceBuildProcessRequest request,
        IEvidenceExecutableLease retainedExecutable);
}

internal interface IEvidenceRuntimeAssemblyLease : IDisposable
{
    EvidenceRuntimeAssemblyIdentity Identity { get; }

    IReadOnlyList<EvidenceRuntimeAssemblyIdentity> RuntimeClosure { get; }

    bool ReparseFreeAncestorChains { get; }
}

internal interface IEvidenceRuntimeAssemblyFactory
{
    IEvidenceRuntimeAssemblyLease OpenRecreatedClosure(
        IReadOnlyList<string> exactRuntimeAssemblyPaths,
        string priorOpenedHandleIdentity);
}

internal interface IEvidenceRuntimeAncestorLease : IDisposable
{
    bool ExactReparseFreeChains { get; }
}

internal interface IEvidenceRuntimeAncestorFactory
{
    IEvidenceRuntimeAncestorLease OpenRetainedChains(IReadOnlyList<string> exactAssemblyPaths);
}

internal sealed class Win32MsBuildExecutableFactory(Win32ExecutableIdentityFactory files) :
    IEvidenceMsBuildExecutableFactory
{
    public IEvidenceExecutableLease OpenPinnedMsBuild() =>
        files.OpenExpected(Win32ExecutableProfile.MsBuild());
}

internal sealed class Win32EvidenceBuildDriver(
    IEvidenceMsBuildExecutableFactory executables,
    IEvidenceBuildRepositoryInspector repository,
    IEvidenceBuildPrerequisiteInspector prerequisites,
    IEvidenceBuildEnvironmentFactory environments,
    IEvidenceBuildOutputApi outputs,
    IEvidenceBuildProcessRunner processes,
    IEvidenceRuntimeAssemblyFactory runtimeAssemblies) : IEvidenceBuildDriver
{
    internal static readonly TimeSpan ExactBuildDeadline = TimeSpan.FromMinutes(5);
    internal const int ExactOutputCapBytes = 4 * 1024 * 1024;
    internal const int ExactErrorCapBytes = 4 * 1024 * 1024;
    private const string GameProjectArgument = @"game\Rounds.Game.csproj";

    public IEvidenceExecutableLease OpenMsBuildExecutable(EvidenceBuildInvocation required)
    {
        ValidateInvocation(required);
        var lease = executables.OpenPinnedMsBuild();
        try
        {
            ValidateMsBuildIdentity(lease.Identity, required.Executable);
            return lease;
        }
        catch (Exception failure)
        {
            try
            {
                lease.Dispose();
            }
            catch (Exception closeFailure)
            {
                throw new AggregateException(failure, closeFailure);
            }
            ExceptionDispatchInfo.Capture(failure).Throw();
            throw;
        }
    }

    public IEvidenceBuildAttestationLease RebuildAndAttest(
        EvidenceBuildInvocation required,
        IEvidenceExecutableLease msBuildExecutable)
    {
        ValidateInvocation(required);
        ArgumentNullException.ThrowIfNull(msBuildExecutable);
        ValidateMsBuildIdentity(msBuildExecutable.Identity, required.Executable);

        var candidateBefore = repository.ReadCleanCandidate(required.WorkingDirectory);
        ValidateCandidate(candidateBefore, required.WorkingDirectory);
        ValidatePrerequisites(prerequisites.Read(required.WorkingDirectory), required);
        var effectiveEnvironment = environments.CreateSanitized(required);
        ValidateEffectiveEnvironment(effectiveEnvironment, required);

        var runtimePath = Path.GetFullPath(Path.Combine(
            required.WorkingDirectory,
            @"game\.godot\mono\temp\bin\Debug\Rounds.Game.dll"));
        var prior = outputs.Read(runtimePath);
        ValidatePriorOutput(prior, runtimePath);
        outputs.Delete(runtimePath);
        var deleted = outputs.Read(runtimePath);
        if (deleted.Exists || !string.Equals(deleted.Path, runtimePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The prior runtime assembly was not proven absent before rebuild.");
        }

        var candidateBeforeSpawn = repository.ReadCleanCandidate(required.WorkingDirectory);
        ValidateCandidate(candidateBeforeSpawn, required.WorkingDirectory);
        if (candidateBeforeSpawn != candidateBefore)
        {
            throw new InvalidOperationException("Candidate identity changed before build process creation.");
        }

        var request = new EvidenceBuildProcessRequest(
            required,
            effectiveEnvironment,
            InheritAmbientEnvironment: false,
            StartSuspended: true,
            new EvidenceBuildJobLimits(
                0x3,
                1,
                768L * 1024 * 1024,
                1024L * 1024 * 1024,
                KillOnJobClose: true),
            UseShellExecute: false,
            CreateNoWindow: true,
            HiddenWindow: true,
            BelowNormalPriority: true,
            ExactBuildDeadline,
            ExactOutputCapBytes,
            ExactErrorCapBytes);
        var result = processes.Run(request, msBuildExecutable);
        ValidateProcessResult(result, msBuildExecutable.Identity, effectiveEnvironment);

        var candidateAfter = repository.ReadCleanCandidate(required.WorkingDirectory);
        ValidateCandidate(candidateAfter, required.WorkingDirectory);
        if (candidateAfter != candidateBefore)
        {
            throw new InvalidOperationException("Candidate identity changed during the immediate rebuild.");
        }

        var recreated = outputs.Read(runtimePath);
        ValidateRecreatedOutput(recreated, prior, runtimePath);
        string[] runtimePaths =
        [
            runtimePath,
            Path.Combine(Path.GetDirectoryName(runtimePath)!, "Rounds.Replay.dll"),
            Path.Combine(Path.GetDirectoryName(runtimePath)!, "Rounds.Sim.dll"),
        ];
        var runtimeLease = runtimeAssemblies.OpenRecreatedClosure(
            runtimePaths,
            prior.OpenedHandleIdentity);
        try
        {
            var runtime = runtimeLease.Identity;
            if (!ValidRuntime(runtime, recreated, runtimePath) ||
                !runtimeLease.ReparseFreeAncestorChains ||
                !ValidRuntimeClosure(runtimeLease.RuntimeClosure, runtimePaths))
            {
                throw new InvalidOperationException("Recreated runtime assembly closure did not match its retained files.");
            }

            var attestation = new EvidenceBuildAttestation(
                required,
                effectiveEnvironment,
                candidateAfter,
                msBuildExecutable.Identity,
                result.ProcessImage,
                runtime,
                runtimeLease.RuntimeClosure,
                ZeroWarnings: true,
                DeletedPriorOutput: true);
            return new Win32EvidenceBuildAttestationLease(runtimeLease, attestation);
        }
        catch (Exception failure)
        {
            try
            {
                runtimeLease.Dispose();
            }
            catch (Exception cleanup)
            {
                throw new AggregateException(failure, cleanup);
            }
            ExceptionDispatchInfo.Capture(failure).Throw();
            throw;
        }
    }

    private static void ValidateInvocation(EvidenceBuildInvocation required)
    {
        ArgumentNullException.ThrowIfNull(required);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(required.WorkingDirectory));
        var expectedSdk = Path.GetFullPath(Path.Combine(root, @".tools\dotnet\sdk\8.0.423\Sdks"));
        string[] expectedArguments =
        [
            GameProjectArgument,
            "/noAutoResponse",
            "/t:Rebuild",
            "/p:Configuration=Debug",
            "/p:Restore=false",
            "/p:UseSharedCompilation=false",
            "/p:BuildProjectReferences=true",
            "/m:1",
            "/nr:false",
            "/v:minimal",
            "/warnaserror",
        ];
        var expectedEnvironment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DOTNET_PROCESSOR_COUNT"] = "2",
            ["MSBUILDDISABLENODEREUSE"] = "1",
            ["MSBuildEnableWorkloadResolver"] = "false",
            ["MSBuildSDKsPath"] = expectedSdk,
        };
        if (!string.Equals(required.Executable, BaseProjectileEvidenceLaunchPlanner.MsBuildPath, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(required.WorkingDirectory, root, StringComparison.OrdinalIgnoreCase) ||
            !required.Arguments.SequenceEqual(expectedArguments, StringComparer.Ordinal) ||
            required.Environment.Count != expectedEnvironment.Count ||
            expectedEnvironment.Any(pair => !required.Environment.TryGetValue(pair.Key, out var actual) ||
                !string.Equals(pair.Value, actual, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Build invocation did not match the pinned rebuild contract.");
        }
    }

    private static void ValidateMsBuildIdentity(
        EvidenceOpenedExecutableIdentity identity,
        string expectedPath)
    {
        if (!identity.Exists || !identity.IdentityBound || identity.IsReparsePoint ||
            string.IsNullOrWhiteSpace(identity.OpenedHandleIdentity) ||
            !string.Equals(identity.Path, expectedPath, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(identity.Sha256, BaseProjectileEvidenceLaunchPlanner.MsBuildSha256, StringComparison.Ordinal) ||
            !string.Equals(identity.FileVersion, BaseProjectileEvidenceLaunchPlanner.MsBuildFileVersion, StringComparison.Ordinal) ||
            !string.Equals(identity.ProductVersion, BaseProjectileEvidenceLaunchPlanner.MsBuildProductVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Opened MSBuild lease did not match its pinned identity.");
        }
    }

    private static void ValidateCandidate(EvidenceCandidateIdentity candidate, string root)
    {
        if (!string.Equals(candidate.RepositoryRoot, root, StringComparison.OrdinalIgnoreCase) ||
            candidate.Commit.Length != 40 || !candidate.Commit.All(LowerHex) ||
            !candidate.CleanHead || !candidate.IdentityBound ||
            string.IsNullOrWhiteSpace(candidate.RepositoryHandleIdentity))
        {
            throw new InvalidOperationException("Repository candidate was not a clean identity-bound commit.");
        }
    }

    private static void ValidatePrerequisites(
        EvidenceBuildPrerequisiteAttestation actual,
        EvidenceBuildInvocation required)
    {
        var expectedSdk = required.Environment["MSBuildSDKsPath"];
        var packRoot = Path.GetFullPath(Path.Combine(
            required.WorkingDirectory,
            @".tools\dotnet\packs\Microsoft.NETCore.App.Ref"));
        var reference = Path.GetFullPath(actual.ReferencePackDirectory);
        var relativeReference = Path.GetRelativePath(packRoot, reference);
        string[] requiredRelativeInputs =
        [
            "global.json",
            @"game\Rounds.Game.csproj",
            @"game\packages.lock.json",
            @"game\obj\project.assets.json",
            @"src\Rounds.Replay\Rounds.Replay.csproj",
            @"src\Rounds.Replay\packages.lock.json",
            @"src\Rounds.Replay\obj\project.assets.json",
            @"src\Rounds.Sim\Rounds.Sim.csproj",
            @"src\Rounds.Sim\packages.lock.json",
            @"src\Rounds.Sim\obj\project.assets.json",
        ];
        string[] requiredPackages = ["Godot.NET.Sdk", "Godot.SourceGenerators", "GodotSharp", "GodotSharpEditor"];
        if (!string.Equals(actual.GlobalJsonSdkVersion, BaseProjectileEvidenceLaunchPlanner.SdkVersion, StringComparison.Ordinal) ||
            !LowerSha(actual.GlobalJsonSha256) ||
            !actual.RollForwardDisabled || !actual.AllowPrereleaseDisabled || !actual.GlobalJsonIdentityBound ||
            !string.Equals(Path.GetFullPath(actual.SdkDirectory), expectedSdk, StringComparison.OrdinalIgnoreCase) ||
            !actual.SdkDirectoryExists || !actual.SdkDirectoryIdentityBound ||
            Path.IsPathFullyQualified(relativeReference) || relativeReference == ".." ||
            relativeReference.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            !reference.EndsWith(Path.Combine("ref", "net8.0"), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(actual.ReferencePackVersion, "8.0.29", StringComparison.Ordinal) ||
            !actual.ReferencePackDirectoryExists || !actual.ReferencePackDirectoryIdentityBound ||
            actual.RequiredInputs.Count != requiredRelativeInputs.Length ||
            requiredRelativeInputs.Any(relative => !actual.RequiredInputs.Any(input =>
                string.Equals(input.Path, Path.GetFullPath(Path.Combine(required.WorkingDirectory, relative)), StringComparison.OrdinalIgnoreCase) &&
                input.Exists && input.IdentityBound && input.ReparseFreeAncestors && LowerSha(input.Sha256))) ||
            actual.RequiredPackages.Count != requiredPackages.Length ||
            requiredPackages.Any(id => !actual.RequiredPackages.Any(package =>
                string.Equals(package.PackageId, id, StringComparison.Ordinal) &&
                string.Equals(package.Version, "4.7.1", StringComparison.Ordinal) &&
                Path.IsPathFullyQualified(package.CachePath) && package.Exists && package.IdentityBound &&
                package.ReparseFreeAncestors && LowerSha(package.Sha256))))
        {
            throw new InvalidOperationException("Locked SDK, reference-pack, or assets prerequisites were not exact.");
        }
    }

    private static void ValidateEffectiveEnvironment(
        IReadOnlyDictionary<string, string> actual,
        EvidenceBuildInvocation required)
    {
        string[] exactKeys =
        [
            "SystemRoot", "WINDIR", "TEMP", "TMP",
            "DOTNET_PROCESSOR_COUNT", "MSBUILDDISABLENODEREUSE",
            "MSBuildEnableWorkloadResolver", "MSBuildSDKsPath",
            "DOTNET_CLI_UI_LANGUAGE", "VSLANG", "NUGET_PACKAGES",
            "DOTNET_CLI_HOME", "MSBuildUserExtensionsPath",
        ];
        var root = required.WorkingDirectory;
        if (actual.Count != exactKeys.Length || exactKeys.Any(key => !actual.ContainsKey(key)) ||
            required.Environment.Any(pair => !actual.TryGetValue(pair.Key, out var value) || value != pair.Value) ||
            string.IsNullOrWhiteSpace(actual["SystemRoot"]) || actual["SystemRoot"] != actual["WINDIR"] ||
            string.IsNullOrWhiteSpace(actual["TEMP"]) || actual["TEMP"] != actual["TMP"] ||
            actual["DOTNET_CLI_UI_LANGUAGE"] != "en-US" || actual["VSLANG"] != "1033" ||
            actual["NUGET_PACKAGES"] != Path.GetFullPath(Path.Combine(root, @".tools\nuget-packages")) ||
            actual["DOTNET_CLI_HOME"] != Path.GetFullPath(Path.Combine(root, @".tools\dotnet-home")) ||
            actual["MSBuildUserExtensionsPath"] != Path.GetFullPath(Path.Combine(root, @".tools\empty\msbuild-user")))
        {
            throw new InvalidOperationException("Effective build environment was not the exact sanitized allowlist.");
        }
    }

    private static void ValidatePriorOutput(EvidenceBuildOutputState prior, string path)
    {
        if (!prior.Exists || !prior.IdentityBound || prior.IsReparsePoint ||
            !prior.ReparseFreeAncestors ||
            string.IsNullOrWhiteSpace(prior.OpenedHandleIdentity) || prior.Length <= 0 ||
            !string.Equals(prior.Path, path, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Prior runtime output was not an exact identity-bound file.");
        }
    }

    private static void ValidateProcessResult(
        EvidenceBuildProcessResult result,
        EvidenceOpenedExecutableIdentity expectedImage,
        IReadOnlyDictionary<string, string> expectedEnvironment)
    {
        if (result.ProcessImage != expectedImage ||
            result.EffectiveEnvironment.Count != expectedEnvironment.Count ||
            expectedEnvironment.Any(pair => !result.EffectiveEnvironment.TryGetValue(pair.Key, out var value) || value != pair.Value) ||
            result.ExitCode != 0 || result.TimedOut ||
            !result.StandardOutputReachedEof || !result.StandardErrorReachedEof ||
            result.StandardOutputExceededCap || result.StandardErrorExceededCap ||
            !result.ProcessImageMatchedBeforeResume || !result.AssignedToJobBeforeResume ||
            !result.PipesDrainedConcurrently || !result.JobEmpty ||
            !result.WarningCountParsed || result.WarningCount != 0 ||
            result.StandardOutput.Length > ExactOutputCapBytes ||
            result.StandardError.Length > ExactErrorCapBytes)
        {
            throw new InvalidOperationException("Pinned MSBuild process did not complete with exact bounded zero-warning attribution.");
        }
    }

    private static void ValidateRecreatedOutput(
        EvidenceBuildOutputState recreated,
        EvidenceBuildOutputState prior,
        string path)
    {
        if (!recreated.Exists || !recreated.IdentityBound || recreated.IsReparsePoint ||
            !recreated.ReparseFreeAncestors ||
            recreated.Length <= 0 || string.IsNullOrWhiteSpace(recreated.OpenedHandleIdentity) ||
            string.Equals(recreated.OpenedHandleIdentity, prior.OpenedHandleIdentity, StringComparison.Ordinal) ||
            !string.Equals(recreated.Path, path, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Immediate rebuild did not recreate a distinct identity-bound runtime output.");
        }
    }

    private static bool ValidRuntime(
        EvidenceRuntimeAssemblyIdentity runtime,
        EvidenceBuildOutputState recreated,
        string path) =>
        runtime.Exists && runtime.IdentityBound && !runtime.IsReparsePoint &&
        runtime.RecreatedByImmediateRebuild &&
        string.Equals(runtime.Path, path, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(runtime.OpenedHandleIdentity, recreated.OpenedHandleIdentity, StringComparison.Ordinal) &&
        runtime.Sha256.Length == 64 && runtime.Sha256.All(LowerHex) &&
        runtime.Mvid.Length == 32 && runtime.Mvid.All(LowerHex);

    private static bool ValidRuntimeClosure(
        IReadOnlyList<EvidenceRuntimeAssemblyIdentity> closure,
        IReadOnlyList<string> expectedPaths) =>
        closure.Count == expectedPaths.Count && expectedPaths.Select((path, index) =>
            closure[index].Exists && closure[index].IdentityBound && !closure[index].IsReparsePoint &&
            closure[index].RecreatedByImmediateRebuild &&
            string.Equals(closure[index].Path, path, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(closure[index].OpenedHandleIdentity) &&
            closure[index].Sha256.Length == 64 && closure[index].Sha256.All(LowerHex) &&
            closure[index].Mvid.Length == 32 && closure[index].Mvid.All(LowerHex)).All(value => value) &&
        closure.Select(value => value.OpenedHandleIdentity).Distinct(StringComparer.Ordinal).Count() == closure.Count;

    private static bool LowerSha(string value) => value.Length == 64 && value.All(LowerHex);

    private static bool LowerHex(char value) =>
        value is >= '0' and <= '9' or >= 'a' and <= 'f';
}

internal sealed class Win32RuntimeAssemblyFactory(
    IWin32RetainedFileApi api,
    IEvidenceRuntimeAncestorFactory ancestors) :
    IEvidenceRuntimeAssemblyFactory
{
    internal const long MaximumRuntimeAssemblyBytes = 512L * 1024 * 1024;

    public IEvidenceRuntimeAssemblyLease OpenRecreatedClosure(
        IReadOnlyList<string> exactRuntimeAssemblyPaths,
        string priorOpenedHandleIdentity)
    {
        ArgumentNullException.ThrowIfNull(exactRuntimeAssemblyPaths);
        if (exactRuntimeAssemblyPaths.Count != 3)
        {
            throw new ArgumentException("Runtime closure must contain Game, Replay, and Sim assemblies.", nameof(exactRuntimeAssemblyPaths));
        }
        IEvidenceRuntimeAncestorLease? ancestorLease = null;
        var leases = new List<IEvidenceRuntimeAssemblyLease>();
        try
        {
            ancestorLease = ancestors.OpenRetainedChains(exactRuntimeAssemblyPaths);
            if (!ancestorLease.ExactReparseFreeChains)
            {
                throw new InvalidOperationException("Runtime assembly ancestor chains were not exact and reparse-free.");
            }
            for (var index = 0; index < exactRuntimeAssemblyPaths.Count; index++)
            {
                leases.Add(OpenOne(
                    exactRuntimeAssemblyPaths[index],
                    index == 0 ? priorOpenedHandleIdentity : string.Empty));
            }
            return new Win32RuntimeAssemblyClosureLease(leases, ancestorLease);
        }
        catch (Exception failure)
        {
            Exception? cleanup = null;
            for (var index = leases.Count - 1; index >= 0; index--)
            {
                try { leases[index].Dispose(); }
                catch (Exception exception) { cleanup = cleanup is null ? exception : new AggregateException(cleanup, exception); }
            }
            try { ancestorLease?.Dispose(); }
            catch (Exception exception) { cleanup = cleanup is null ? exception : new AggregateException(cleanup, exception); }
            throw cleanup is null ? failure : new AggregateException(failure, cleanup);
        }
    }

    private IEvidenceRuntimeAssemblyLease OpenOne(
        string exactRuntimeAssemblyPath,
        string priorOpenedHandleIdentity)
    {
        var expected = Win32ExecutableIdentityFactory.NormalizeRequestedPath(exactRuntimeAssemblyPath);
        var handle = api.OpenReadNoReplace(
            expected,
            Win32EvidenceConstants.GenericRead,
            Win32EvidenceConstants.FileShareRead,
            Win32EvidenceConstants.OpenExisting,
            Win32EvidenceConstants.FileAttributeNormal | Win32EvidenceConstants.FileFlagOpenReparsePoint);
        if (handle is 0 or -1) throw new InvalidOperationException("Opening the recreated runtime assembly failed.");
        try
        {
            var before = api.ReadSnapshot(handle);
            ValidateSnapshot(before, expected);
            var openedIdentity = FormatIdentity(before);
            if (string.Equals(openedIdentity, priorOpenedHandleIdentity, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Recreated runtime assembly reused the prior file identity.");
            }
            string hash;
            string mvid;
            using (var stream = api.OpenReadStream(handle))
            {
                if (!stream.CanRead || !stream.CanSeek || stream.Length != before.Length)
                {
                    throw new InvalidOperationException("Runtime assembly retained stream shape was invalid.");
                }
                stream.Position = 0;
                hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
                stream.Position = 0;
                using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
                if (!pe.HasMetadata) throw new InvalidOperationException("Runtime assembly did not contain managed metadata.");
                MetadataReader metadata = pe.GetMetadataReader();
                if (!metadata.IsAssembly ||
                    !string.Equals(
                        metadata.GetString(metadata.GetAssemblyDefinition().Name),
                        Path.GetFileNameWithoutExtension(expected),
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Runtime assembly definition name did not match its exact output path.");
                }
                var module = metadata.GetModuleDefinition();
                var moduleId = metadata.GetGuid(module.Mvid);
                if (moduleId == Guid.Empty) throw new InvalidOperationException("Runtime assembly MVID was empty.");
                mvid = moduleId.ToString("N");
            }
            var after = api.ReadSnapshot(handle);
            if (!SameSnapshot(before, after) ||
                !string.Equals(
                    Win32ExecutableIdentityFactory.NormalizeFinalPath(after.FinalPath),
                    expected,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Runtime assembly changed during retained-handle attestation.");
            }
            return new Win32RuntimeAssemblyLease(
                api,
                handle,
                new EvidenceRuntimeAssemblyIdentity(
                    expected,
                    Exists: true,
                    IdentityBound: true,
                    IsReparsePoint: false,
                    RecreatedByImmediateRebuild: true,
                    openedIdentity,
                    hash,
                    mvid));
        }
        catch (Exception failure)
        {
            try
            {
                if (!api.CloseKernelHandle(handle)) throw new InvalidOperationException("Closing refused runtime handle failed.");
            }
            catch (Exception closeFailure)
            {
                throw new AggregateException(failure, closeFailure);
            }
            ExceptionDispatchInfo.Capture(failure).Throw();
            throw;
        }
    }

    private static void ValidateSnapshot(Win32RetainedFileSnapshot snapshot, string expected)
    {
        if (!string.Equals(
                Win32ExecutableIdentityFactory.NormalizeFinalPath(snapshot.FinalPath),
                expected,
                StringComparison.OrdinalIgnoreCase) ||
            snapshot.Directory || snapshot.DeletePending || snapshot.LinkCount != 1 ||
            snapshot.ReparseTag != 0 || snapshot.Length <= 0 || snapshot.Length > MaximumRuntimeAssemblyBytes ||
            snapshot.FileId.Length != 32 || !snapshot.FileId.All(value =>
                value is >= '0' and <= '9' or >= 'a' and <= 'f') ||
            snapshot.FileId.All(value => value == '0'))
        {
            throw new InvalidOperationException("Runtime assembly retained snapshot was invalid.");
        }
    }

    private static bool SameSnapshot(Win32RetainedFileSnapshot left, Win32RetainedFileSnapshot right) =>
        left.VolumeSerialNumber == right.VolumeSerialNumber && left.FileId == right.FileId &&
        left.Length == right.Length && left.ChangeTime == right.ChangeTime &&
        left.Attributes == right.Attributes && left.ReparseTag == right.ReparseTag &&
        left.LinkCount == right.LinkCount && left.DeletePending == right.DeletePending &&
        left.Directory == right.Directory;

    private static string FormatIdentity(Win32RetainedFileSnapshot snapshot) =>
        $"volume:{snapshot.VolumeSerialNumber:x16}:file:{snapshot.FileId}";
}

internal sealed class Win32RuntimeAssemblyLease(
    IWin32KernelHandleCloser closer,
    nint handle,
    EvidenceRuntimeAssemblyIdentity identity) : IEvidenceRuntimeAssemblyLease
{
    private nint _handle = handle is not 0 and not -1
        ? handle
        : throw new ArgumentOutOfRangeException(nameof(handle));

    public EvidenceRuntimeAssemblyIdentity Identity { get; } = identity;

    public IReadOnlyList<EvidenceRuntimeAssemblyIdentity> RuntimeClosure => [Identity];

    public bool ReparseFreeAncestorChains => false;

    public void Dispose()
    {
        var owned = Interlocked.Exchange(ref _handle, 0);
        if (owned != 0 && !closer.CloseKernelHandle(owned))
        {
            throw new InvalidOperationException("Closing retained runtime assembly handle failed.");
        }
    }
}

internal sealed class Win32RuntimeAssemblyClosureLease : IEvidenceRuntimeAssemblyLease
{
    private IReadOnlyList<IEvidenceRuntimeAssemblyLease>? _leases;
    private IEvidenceRuntimeAncestorLease? _ancestors;

    internal Win32RuntimeAssemblyClosureLease(
        IReadOnlyList<IEvidenceRuntimeAssemblyLease> leases,
        IEvidenceRuntimeAncestorLease ancestors)
    {
        if (leases.Count != 3) throw new ArgumentException("Runtime closure requires three leases.", nameof(leases));
        _leases = leases;
        _ancestors = ancestors;
        RuntimeClosure = Array.AsReadOnly(leases.Select(lease => lease.Identity).ToArray());
    }

    public EvidenceRuntimeAssemblyIdentity Identity => RuntimeClosure[0];

    public IReadOnlyList<EvidenceRuntimeAssemblyIdentity> RuntimeClosure { get; }

    public bool ReparseFreeAncestorChains => _ancestors?.ExactReparseFreeChains == true;

    public void Dispose()
    {
        var leases = Interlocked.Exchange(ref _leases, null);
        if (leases is null) return;
        Exception? failure = null;
        for (var index = leases.Count - 1; index >= 0; index--)
        {
            try { leases[index].Dispose(); }
            catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
        }
        var ancestorLease = Interlocked.Exchange(ref _ancestors, null);
        try { ancestorLease?.Dispose(); }
        catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

internal sealed class Win32EvidenceBuildAttestationLease(
    IEvidenceRuntimeAssemblyLease runtime,
    EvidenceBuildAttestation attestation) : IEvidenceBuildAttestationLease
{
    private IEvidenceRuntimeAssemblyLease? _runtime = runtime;

    public EvidenceBuildAttestation Attestation { get; } = attestation;

    public void Dispose() => Interlocked.Exchange(ref _runtime, null)?.Dispose();
}
