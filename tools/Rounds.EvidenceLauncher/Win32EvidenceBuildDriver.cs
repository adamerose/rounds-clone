using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using Rounds.Game;

namespace Rounds.EvidenceLauncher;

internal sealed record EvidenceBuildInputIdentity(
    string Path,
    bool Exists,
    bool IdentityBound,
    bool ReparseFreeAncestors,
    string Sha256,
    EvidenceBuildContentHashKind HashKind);

internal enum EvidenceBuildContentHashKind
{
    RawBytesSha256,
    RepositoryAndPackageRootNormalizedJsonSha256,
}

internal sealed record EvidenceBuildPackageIdentity(
    string PackageId,
    string Version,
    string ArchivePath,
    string ExtractedRoot,
    bool Exists,
    bool IdentityBound,
    bool ReparseFreeAncestors,
    bool RetainedExtractedDirectoryAndEntries,
    string RetainedExtractedDirectoryIdentity,
    string RetainedExtractedEntriesIdentity,
    int ExtractedEntryCount,
    string ArchiveSha256,
    string ExtractedTreeSha256,
    string ExtractedTreeHashAlgorithm);

internal sealed record EvidenceBuildPrerequisiteAttestation(
    string GlobalJsonSdkVersion,
    string GlobalJsonSha256,
    bool RollForwardDisabled,
    bool AllowPrereleaseDisabled,
    bool GlobalJsonIdentityBound,
    string SdkDirectory,
    bool SdkDirectoryExists,
    bool SdkDirectoryIdentityBound,
    string SdkContentSha256,
    string SdkTreeHashAlgorithm,
    string ReferencePackDirectory,
    string ReferencePackVersion,
    bool ReferencePackDirectoryExists,
    bool ReferencePackDirectoryIdentityBound,
    string ReferencePackContentSha256,
    string ReferencePackTreeHashAlgorithm,
    IReadOnlyList<EvidenceBuildInputIdentity> RequiredInputs,
    IReadOnlyList<EvidenceBuildPackageIdentity> RequiredPackages);

internal sealed record EvidenceTrustedDirectoryIdentity(
    string RequestedPath,
    string CanonicalPath,
    bool Exists,
    bool IdentityBound,
    bool ReparseFreeAncestors,
    string OpenedHandleIdentity);

internal sealed record EvidenceBuildProvenanceSnapshot(
    EvidenceCandidateIdentity Candidate,
    string RetainedInputsAndPackageTreesIdentity);

internal static class EvidenceBuildManifest
{
    internal const string SdkTreeSha256 = "6eb01c93e060c47279dae895a8635d5758aba49fbd9de68f716786496d92c093";
    internal const string ReferencePackTreeSha256 = "6a711682d9729183e36aa0c5878af274a62285e1cd516637912861c63651434f";
    internal const string ExactTreeHashAlgorithm = "ordinal-relative-path-nul-raw-bytes-sha256-v1";
    private const string GlobalJsonSha256 = "248c17bb46fd7ff31402c62dc1870e90005fc1d9bcbc9a31cefcc78691149d76";

    private static readonly IReadOnlyList<(string Path, string Sha256)> Inputs = Array.AsReadOnly(
    new (string Path, string Sha256)[]
    {
        ("global.json", GlobalJsonSha256),
        (@"game\Rounds.Game.csproj", "e32100c13c6e55c3aadcb3c0aeba5b2a090b742e452c9ed7c699db2ae3ebc8d4"),
        (@"game\packages.lock.json", "a51a8e19de69f77ccb5c087d49124a5663cc09a413d9b249212fdfd157fc1ca7"),
        (@"game\.godot\mono\temp\obj\project.assets.json", "5a5952fe97201dad1c0c5cf7356b109e178ed8f5af77a0ee3b7219520a0ffae9"),
        (@"src\Rounds.Replay\Rounds.Replay.csproj", "49ccf48dabec7660e4257ff89138cb137f9fa745cedbecf0df071e4109ccc8a4"),
        (@"src\Rounds.Replay\packages.lock.json", "dff17049cec93587e8253599f209cf95e55c83ed75d4fe3a5eebc9ae3385ee1e"),
        (@"src\Rounds.Replay\obj\project.assets.json", "f5660f35d8bb445bec208532dfebe33904eb8ffd5446d245bffe4d4151f15b35"),
        (@"src\Rounds.Sim\Rounds.Sim.csproj", "5b480ec4dbd8edf9f9aa00c75b488ca6bd525964ab8d6e4d899b0d12319f8262"),
        (@"src\Rounds.Sim\packages.lock.json", "22308ad23ccd417b5d96020fb92e203b85fad8c3788b3feadb2ca403d8ad596c"),
        (@"src\Rounds.Sim\obj\project.assets.json", "b3c494940b89d446f397e9062613f8cd498c0e180016af5c22ab00467fb0caf3"),
    });

    internal const string ExtractedPackageTreeHashAlgorithm =
        "ordinal-relative-path-nul-raw-bytes-sha256-v1;exclude=.nupkg,.nupkg.sha512,.nupkg.metadata";

    private static readonly IReadOnlyList<(string Id, string ArchiveSha256, string TreeSha256, int EntryCount)> Packages = Array.AsReadOnly(
    new (string Id, string ArchiveSha256, string TreeSha256, int EntryCount)[]
    {
        ("Godot.NET.Sdk", "1f93837c9b8df052596203a0882818381cb5d64cd7f86f9a46cb67184d8287ff", "7e429186fd97b24988331956a05320422aca0b361c323b3d4ca488881e271035", 8),
        ("Godot.SourceGenerators", "6b3e98ab8e94bad4d2f65de559bdcf0637fd6ca084cdf0ac1a6d8a17542bb4f1", "fd1b09a6dc019edeb66cc5549e15f08a7522b8a925504c37b81b0a10fdd7c84c", 4),
        ("GodotSharp", "f0b366029c9859355cacc25ccc2e4f19bd2dee7e16d5c22b82d7c736ff208068", "313732c38a52f8f52781de18f1d732cc8ad1c9d80e62df4ee4f56dda43f5885b", 4),
        ("GodotSharpEditor", "8ced4bfd55968cf4f835035b7e8d8149ff535e4bf200496491f8e8d93a91b682", "0d1de4b17579be531c5c786b0fa398e3bd24841a6f156a64dc76f54b506d887e", 4),
    });

    internal static EvidenceBuildPrerequisiteAttestation Create(string exactRepositoryRoot)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(exactRepositoryRoot));
        var inputs = Inputs.Select(value => new EvidenceBuildInputIdentity(
            Path.GetFullPath(Path.Combine(root, value.Path)), true, true, true, value.Sha256,
            value.Path.EndsWith("project.assets.json", StringComparison.Ordinal)
                ? EvidenceBuildContentHashKind.RepositoryAndPackageRootNormalizedJsonSha256
                : EvidenceBuildContentHashKind.RawBytesSha256)).ToArray();
        var packages = Packages.Select(value => new EvidenceBuildPackageIdentity(
            value.Id,
            "4.7.1",
            Path.GetFullPath(Path.Combine(root, @".tools\nuget-packages", value.Id.ToLowerInvariant(), "4.7.1", $"{value.Id.ToLowerInvariant()}.4.7.1.nupkg")),
            Path.GetFullPath(Path.Combine(root, @".tools\nuget-packages", value.Id.ToLowerInvariant(), "4.7.1")),
            true, true, true, true,
            $"retained-directory:{value.Id.ToLowerInvariant()}",
            $"retained-entries:{value.Id.ToLowerInvariant()}",
            value.EntryCount,
            value.ArchiveSha256, value.TreeSha256,
            ExtractedPackageTreeHashAlgorithm)).ToArray();
        return new EvidenceBuildPrerequisiteAttestation(
            "8.0.423", GlobalJsonSha256, true, true, true,
            Path.GetFullPath(Path.Combine(root, @".tools\dotnet\sdk\8.0.423\Sdks")), true, true,
            SdkTreeSha256, ExactTreeHashAlgorithm,
            Path.GetFullPath(Path.Combine(root, @".tools\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.29\ref\net8.0")),
            "8.0.29", true, true, ReferencePackTreeSha256, ExactTreeHashAlgorithm,
            Array.AsReadOnly(inputs), Array.AsReadOnly(packages));
    }
}

internal sealed record EvidenceBuildOutputState(
    string Path,
    bool Exists,
    bool IdentityBound,
    bool IsReparsePoint,
    bool ReparseFreeAncestors,
    string OpenedHandleIdentity,
    long Length,
    long ChangeTime,
    bool DeletePending,
    uint LinkCount,
    bool Directory)
{
    internal static EvidenceBuildOutputState Missing(string path) =>
        new(path, false, false, false, false, string.Empty, 0, 0, false, 0, false);
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

internal interface IEvidenceBuildProvenanceLease : IDisposable
{
    EvidenceCandidateIdentity Candidate { get; }

    EvidenceBuildPrerequisiteAttestation Prerequisites { get; }

    EvidenceTrustedDirectoryIdentity SystemRoot { get; }

    EvidenceTrustedDirectoryIdentity TemporaryDirectory { get; }

    bool RetainsExactRepositoryInputAndOutputAncestorChains { get; }

    EvidenceBuildProvenanceSnapshot Revalidate();
}

internal interface IEvidenceBuildProvenanceFactory
{
    IEvidenceBuildProvenanceLease OpenRetained(string exactRepositoryRoot);
}

internal interface IEvidenceBuildEnvironmentFactory
{
    IReadOnlyDictionary<string, string> CreateSanitized(
        EvidenceBuildInvocation required,
        EvidenceTrustedDirectoryIdentity systemRoot,
        EvidenceTrustedDirectoryIdentity temporaryDirectory);
}

internal interface IEvidenceBuildOutputApi
{
    IEvidencePriorOutputLease OpenPrior(string exactRuntimeAssemblyPath);

    EvidenceBuildOutputState ReadRecreated(string exactRuntimeAssemblyPath);
}

internal sealed record EvidencePriorOutputDeletionProof(
    string Path,
    string DeletedOpenedHandleIdentity,
    bool ExactRetainedIdentityDisposition,
    bool ExactPathAbsent,
    bool AncestorIdentityStillRetained);

internal interface IEvidencePriorOutputLease : IDisposable
{
    EvidenceBuildOutputState State { get; }

    bool RetainsExactFileAndAncestorIdentity { get; }

    EvidencePriorOutputDeletionProof DeleteRetainedIdentityAndProveAbsent();
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
        IReadOnlyList<string> priorOpenedHandleIdentities);
}

internal interface IWin32RuntimeStreamApi
{
    IReadOnlyList<Win32PublishedStreamEntry> EnumerateStreams(nint retainedFileHandle);
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
    IEvidenceBuildProvenanceFactory provenance,
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
        IEvidenceBuildProvenanceLease? provenanceLease = null;
        IEvidenceRuntimeAssemblyLease? runtimeLease = null;
        var priorOutputLeases = new List<IEvidencePriorOutputLease>();
        try
        {
            provenanceLease = provenance.OpenRetained(required.WorkingDirectory);
            var candidateBefore = provenanceLease.Candidate;
            ValidateCandidate(candidateBefore, required.WorkingDirectory);
            ValidatePrerequisites(provenanceLease.Prerequisites, required);
            ValidateTrustedDirectory(provenanceLease.SystemRoot, "SystemRoot");
            ValidateTrustedDirectory(provenanceLease.TemporaryDirectory, "TEMP");
            if (!provenanceLease.RetainsExactRepositoryInputAndOutputAncestorChains)
            {
                throw new InvalidOperationException("Repository, build-input, and output ancestor chains were not retained continuously.");
            }
            var baseline = provenanceLease.Revalidate();
            ValidateProvenanceSnapshot(baseline, candidateBefore);

            var effectiveEnvironment = FreezeEnvironment(environments.CreateSanitized(
                required, provenanceLease.SystemRoot, provenanceLease.TemporaryDirectory));
            ValidateEffectiveEnvironment(
                effectiveEnvironment, required, provenanceLease.SystemRoot, provenanceLease.TemporaryDirectory);

            var runtimeDirectory = Path.GetFullPath(Path.Combine(
                required.WorkingDirectory, @"game\.godot\mono\temp\bin\Debug"));
            string[] runtimePaths =
            [
                Path.Combine(runtimeDirectory, "Rounds.Game.dll"),
                Path.Combine(runtimeDirectory, "Rounds.Replay.dll"),
                Path.Combine(runtimeDirectory, "Rounds.Sim.dll"),
            ];
            foreach (var path in runtimePaths)
            {
                priorOutputLeases.Add(outputs.OpenPrior(path));
            }
            var prior = priorOutputLeases.Select(value => value.State).ToArray();
            for (var index = 0; index < runtimePaths.Length; index++)
            {
                ValidatePriorOutput(prior[index], runtimePaths[index]);
                if (!priorOutputLeases[index].RetainsExactFileAndAncestorIdentity)
                {
                    throw new InvalidOperationException("Prior runtime file and ancestor identity were not retained continuously.");
                }
            }
            if (prior.Select(value => value.OpenedHandleIdentity).Distinct(StringComparer.Ordinal).Count() != runtimePaths.Length)
            {
                throw new InvalidOperationException("Prior runtime outputs did not have three distinct exact identities.");
            }
            RequireStableProvenance(provenanceLease, baseline, candidateBefore);
            for (var index = 0; index < priorOutputLeases.Count; index++)
            {
                ValidatePriorDeletion(
                    priorOutputLeases[index].DeleteRetainedIdentityAndProveAbsent(),
                    prior[index],
                    runtimePaths[index]);
            }
            RequireStableProvenance(provenanceLease, baseline, candidateBefore);

            var request = new EvidenceBuildProcessRequest(
                required,
                effectiveEnvironment,
                InheritAmbientEnvironment: false,
                StartSuspended: true,
                new EvidenceBuildJobLimits(0x3, 1, 768L * 1024 * 1024, 1024L * 1024 * 1024, true),
                UseShellExecute: false,
                CreateNoWindow: true,
                HiddenWindow: true,
                BelowNormalPriority: true,
                ExactBuildDeadline,
                ExactOutputCapBytes,
                ExactErrorCapBytes);
            var result = processes.Run(request, msBuildExecutable);
            ValidateProcessResult(result, msBuildExecutable.Identity, effectiveEnvironment);
            RequireStableProvenance(provenanceLease, baseline, candidateBefore);

            var recreated = runtimePaths.Select(outputs.ReadRecreated).ToArray();
            var priorIdentities = prior.Select(value => value.OpenedHandleIdentity).ToHashSet(StringComparer.Ordinal);
            for (var index = 0; index < runtimePaths.Length; index++)
            {
                ValidateRecreatedOutput(recreated[index], priorIdentities, runtimePaths[index]);
            }
            if (recreated.Select(value => value.OpenedHandleIdentity).Distinct(StringComparer.Ordinal).Count() != runtimePaths.Length)
            {
                throw new InvalidOperationException("Recreated runtime outputs did not have three distinct identities.");
            }
            RequireStableProvenance(provenanceLease, baseline, candidateBefore);
            runtimeLease = runtimeAssemblies.OpenRecreatedClosure(
                runtimePaths, prior.Select(value => value.OpenedHandleIdentity).ToArray());
            var runtime = runtimeLease.Identity;
            if (!ValidRuntime(runtime, recreated[0], runtimePaths[0]) ||
                !runtimeLease.ReparseFreeAncestorChains ||
                !ValidRuntimeClosure(runtimeLease.RuntimeClosure, runtimePaths) ||
                !runtimeLease.RuntimeClosure.Select((value, index) =>
                    value.OpenedHandleIdentity == recreated[index].OpenedHandleIdentity).All(value => value))
            {
                throw new InvalidOperationException("Recreated runtime assembly closure did not match its retained files.");
            }
            RequireStableProvenance(provenanceLease, baseline, candidateBefore);

            var attestation = new EvidenceBuildAttestation(
                required, effectiveEnvironment, candidateBefore,
                msBuildExecutable.Identity, result.ProcessImage, runtime,
                Array.AsReadOnly(runtimeLease.RuntimeClosure.ToArray()), true, true);
            var completed = new Win32EvidenceBuildAttestationLease(
                runtimeLease, priorOutputLeases.ToArray(), provenanceLease, attestation);
            runtimeLease = null;
            priorOutputLeases.Clear();
            provenanceLease = null;
            return completed;
        }
        catch (Exception failure)
        {
            Exception? cleanup = null;
            try { runtimeLease?.Dispose(); }
            catch (Exception exception) { cleanup = exception; }
            for (var index = priorOutputLeases.Count - 1; index >= 0; index--)
            {
                try { priorOutputLeases[index].Dispose(); }
                catch (Exception exception) { cleanup = cleanup is null ? exception : new AggregateException(cleanup, exception); }
            }
            try { provenanceLease?.Dispose(); }
            catch (Exception exception) { cleanup = cleanup is null ? exception : new AggregateException(cleanup, exception); }
            throw cleanup is null ? failure : new AggregateException(failure, cleanup);
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
        var expected = EvidenceBuildManifest.Create(required.WorkingDirectory);
        if (!SamePrerequisites(actual, expected))
        {
            throw new InvalidOperationException("Locked SDK, reference-pack, or assets prerequisites were not exact.");
        }
    }

    private static bool SamePrerequisites(
        EvidenceBuildPrerequisiteAttestation actual,
        EvidenceBuildPrerequisiteAttestation expected) =>
        actual.GlobalJsonSdkVersion == expected.GlobalJsonSdkVersion &&
        actual.GlobalJsonSha256 == expected.GlobalJsonSha256 &&
        actual.RollForwardDisabled && actual.AllowPrereleaseDisabled && actual.GlobalJsonIdentityBound &&
        ExactPath(actual.SdkDirectory, expected.SdkDirectory) && actual.SdkDirectoryExists &&
        actual.SdkDirectoryIdentityBound && actual.SdkContentSha256 == expected.SdkContentSha256 &&
        actual.SdkTreeHashAlgorithm == expected.SdkTreeHashAlgorithm &&
        ExactPath(actual.ReferencePackDirectory, expected.ReferencePackDirectory) &&
        actual.ReferencePackVersion == expected.ReferencePackVersion &&
        actual.ReferencePackDirectoryExists && actual.ReferencePackDirectoryIdentityBound &&
        actual.ReferencePackContentSha256 == expected.ReferencePackContentSha256 &&
        actual.ReferencePackTreeHashAlgorithm == expected.ReferencePackTreeHashAlgorithm &&
        SameInputs(actual.RequiredInputs, expected.RequiredInputs) &&
        SamePackages(actual.RequiredPackages, expected.RequiredPackages);

    private static bool SameInputs(
        IReadOnlyList<EvidenceBuildInputIdentity> actual,
        IReadOnlyList<EvidenceBuildInputIdentity> expected) =>
        actual.Count == expected.Count && expected.All(item => actual.Count(candidate =>
            ExactPath(candidate.Path, item.Path) && candidate.Sha256 == item.Sha256 && candidate.HashKind == item.HashKind &&
            candidate.Exists && candidate.IdentityBound && candidate.ReparseFreeAncestors) == 1);

    private static bool SamePackages(
        IReadOnlyList<EvidenceBuildPackageIdentity> actual,
        IReadOnlyList<EvidenceBuildPackageIdentity> expected) =>
        actual.Count == expected.Count && expected.All(item => actual.Count(candidate =>
            candidate.PackageId == item.PackageId && candidate.Version == item.Version &&
            ExactPath(candidate.ArchivePath, item.ArchivePath) &&
            ExactPath(candidate.ExtractedRoot, item.ExtractedRoot) &&
            candidate.ArchiveSha256 == item.ArchiveSha256 &&
            candidate.ExtractedTreeSha256 == item.ExtractedTreeSha256 &&
            candidate.ExtractedTreeHashAlgorithm == item.ExtractedTreeHashAlgorithm &&
            candidate.Exists && candidate.IdentityBound && candidate.ReparseFreeAncestors &&
            candidate.RetainedExtractedDirectoryAndEntries &&
            !string.IsNullOrWhiteSpace(candidate.RetainedExtractedDirectoryIdentity) &&
            !string.IsNullOrWhiteSpace(candidate.RetainedExtractedEntriesIdentity) &&
            candidate.ExtractedEntryCount == item.ExtractedEntryCount) == 1);

    private static void ValidateEffectiveEnvironment(
        IReadOnlyDictionary<string, string> actual,
        EvidenceBuildInvocation required,
        EvidenceTrustedDirectoryIdentity systemRoot,
        EvidenceTrustedDirectoryIdentity temporaryDirectory)
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
            !ExactPath(actual["SystemRoot"], systemRoot.CanonicalPath) ||
            !ExactPath(actual["WINDIR"], systemRoot.CanonicalPath) ||
            !ExactPath(actual["TEMP"], temporaryDirectory.CanonicalPath) ||
            !ExactPath(actual["TMP"], temporaryDirectory.CanonicalPath) ||
            actual["DOTNET_CLI_UI_LANGUAGE"] != "en-US" || actual["VSLANG"] != "1033" ||
            actual["NUGET_PACKAGES"] != Path.GetFullPath(Path.Combine(root, @".tools\nuget-packages")) ||
            actual["DOTNET_CLI_HOME"] != Path.GetFullPath(Path.Combine(root, @".tools\dotnet-home")) ||
            actual["MSBuildUserExtensionsPath"] != Path.GetFullPath(Path.Combine(root, @".tools\empty\msbuild-user")))
        {
            throw new InvalidOperationException("Effective build environment was not the exact sanitized allowlist.");
        }
    }

    private static IReadOnlyDictionary<string, string> FreezeEnvironment(
        IReadOnlyDictionary<string, string> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var frozen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in source)
        {
            if (string.IsNullOrEmpty(pair.Key) || pair.Value is null || !frozen.TryAdd(pair.Key, pair.Value))
            {
                throw new InvalidOperationException("Sanitized build environment was mutable, duplicate, or malformed.");
            }
        }
        return new ReadOnlyDictionary<string, string>(frozen);
    }

    private static void ValidateTrustedDirectory(EvidenceTrustedDirectoryIdentity value, string label)
    {
        if (!value.Exists || !value.IdentityBound || !value.ReparseFreeAncestors ||
            string.IsNullOrWhiteSpace(value.OpenedHandleIdentity) ||
            !Path.IsPathFullyQualified(value.RequestedPath) || !Path.IsPathFullyQualified(value.CanonicalPath) ||
            !ExactPath(value.RequestedPath, value.CanonicalPath))
        {
            throw new InvalidOperationException($"Trusted {label} directory identity was not exact and retained.");
        }
    }

    private static void ValidateProvenanceSnapshot(
        EvidenceBuildProvenanceSnapshot snapshot,
        EvidenceCandidateIdentity candidate)
    {
        if (snapshot.Candidate != candidate || string.IsNullOrWhiteSpace(snapshot.RetainedInputsAndPackageTreesIdentity))
        {
            throw new InvalidOperationException("Retained build provenance snapshot was incomplete.");
        }
    }

    private static void RequireStableProvenance(
        IEvidenceBuildProvenanceLease lease,
        EvidenceBuildProvenanceSnapshot baseline,
        EvidenceCandidateIdentity candidate)
    {
        var current = lease.Revalidate();
        ValidateProvenanceSnapshot(current, candidate);
        if (current != baseline)
        {
            throw new InvalidOperationException("Retained candidate or build-input provenance changed during rebuild.");
        }
    }

    private static bool ExactPath(string actual, string expected) =>
        string.Equals(Path.GetFullPath(actual), Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase);

    private static void ValidatePriorOutput(EvidenceBuildOutputState prior, string path)
    {
        if (!prior.Exists || !prior.IdentityBound || prior.IsReparsePoint ||
            !prior.ReparseFreeAncestors || prior.Directory || prior.DeletePending || prior.LinkCount != 1 ||
            string.IsNullOrWhiteSpace(prior.OpenedHandleIdentity) || prior.Length <= 0 || prior.ChangeTime <= 0 ||
            !string.Equals(prior.Path, path, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Prior runtime output was not an exact identity-bound file.");
        }
    }

    private static void ValidatePriorDeletion(
        EvidencePriorOutputDeletionProof proof,
        EvidenceBuildOutputState prior,
        string path)
    {
        if (!ExactPath(proof.Path, path) ||
            proof.DeletedOpenedHandleIdentity != prior.OpenedHandleIdentity ||
            !proof.ExactRetainedIdentityDisposition || !proof.ExactPathAbsent ||
            !proof.AncestorIdentityStillRetained)
        {
            throw new InvalidOperationException("Prior runtime output deletion was not bound to the retained identity and ancestor.");
        }
    }

    private static void ValidateProcessResult(
        EvidenceBuildProcessResult result,
        EvidenceOpenedExecutableIdentity expectedImage,
        IReadOnlyDictionary<string, string> expectedEnvironment)
    {
        var actualEnvironment = FreezeEnvironment(result.EffectiveEnvironment);
        if (result.ProcessImage != expectedImage ||
            actualEnvironment.Count != expectedEnvironment.Count ||
            expectedEnvironment.Any(pair => !actualEnvironment.TryGetValue(pair.Key, out var value) || value != pair.Value) ||
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
        IReadOnlySet<string> priorOpenedHandleIdentities,
        string path)
    {
        if (!recreated.Exists || !recreated.IdentityBound || recreated.IsReparsePoint ||
            !recreated.ReparseFreeAncestors || recreated.Directory || recreated.DeletePending || recreated.LinkCount != 1 ||
            recreated.Length <= 0 || recreated.ChangeTime <= 0 || string.IsNullOrWhiteSpace(recreated.OpenedHandleIdentity) ||
            priorOpenedHandleIdentities.Contains(recreated.OpenedHandleIdentity) ||
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
    IEvidenceRuntimeAncestorFactory ancestors,
    IWin32RuntimeStreamApi streams) :
    IEvidenceRuntimeAssemblyFactory
{
    internal const long MaximumRuntimeAssemblyBytes = 512L * 1024 * 1024;

    public IEvidenceRuntimeAssemblyLease OpenRecreatedClosure(
        IReadOnlyList<string> exactRuntimeAssemblyPaths,
        IReadOnlyList<string> priorOpenedHandleIdentities)
    {
        ArgumentNullException.ThrowIfNull(exactRuntimeAssemblyPaths);
        if (exactRuntimeAssemblyPaths.Count != 3 || priorOpenedHandleIdentities.Count != 3 ||
            priorOpenedHandleIdentities.Any(string.IsNullOrWhiteSpace) ||
            priorOpenedHandleIdentities.Distinct(StringComparer.Ordinal).Count() != 3)
        {
            throw new ArgumentException("Runtime closure must contain Game, Replay, and Sim assemblies.", nameof(exactRuntimeAssemblyPaths));
        }
        IEvidenceRuntimeAncestorLease? ancestorLease = null;
        var leases = new List<IEvidenceRuntimeAssemblyLease>();
        var priorIdentitySet = priorOpenedHandleIdentities.ToHashSet(StringComparer.Ordinal);
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
                    priorIdentitySet));
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
        IReadOnlySet<string> priorOpenedHandleIdentities)
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
            var alternateStreams = streams.EnumerateStreams(handle);
            if (alternateStreams.Count != 1 || alternateStreams[0].Name != "::$DATA" ||
                alternateStreams[0].Length != before.Length)
            {
                throw new InvalidOperationException("Runtime assembly contained an alternate data stream.");
            }
            var openedIdentity = FormatIdentity(before);
            if (priorOpenedHandleIdentities.Contains(openedIdentity))
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
    IReadOnlyList<IEvidencePriorOutputLease> priorOutputs,
    IEvidenceBuildProvenanceLease provenance,
    EvidenceBuildAttestation attestation) : IEvidenceBuildAttestationLease
{
    private IEvidenceRuntimeAssemblyLease? _runtime = runtime;
    private IReadOnlyList<IEvidencePriorOutputLease>? _priorOutputs = priorOutputs;
    private IEvidenceBuildProvenanceLease? _provenance = provenance;

    public EvidenceBuildAttestation Attestation { get; } = attestation;

    public void Dispose()
    {
        var runtimeLease = Interlocked.Exchange(ref _runtime, null);
        var priorLeases = Interlocked.Exchange(ref _priorOutputs, null);
        var provenanceLease = Interlocked.Exchange(ref _provenance, null);
        Exception? failure = null;
        try { runtimeLease?.Dispose(); }
        catch (Exception exception) { failure = exception; }
        if (priorLeases is not null)
        {
            for (var index = priorLeases.Count - 1; index >= 0; index--)
            {
                try { priorLeases[index].Dispose(); }
                catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
            }
        }
        try { provenanceLease?.Dispose(); }
        catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
