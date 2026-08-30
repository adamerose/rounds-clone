using System.Collections.ObjectModel;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Rounds.EvidenceLauncher;
using Rounds.Game;

namespace Rounds.Sim.Tests;

public sealed class Win32EvidenceBuildCreateImageTests
{
    private const string Root = @"C:\repo";

    [Fact]
    public void ExactSuccessPinsAbiRequestAdoptsBeforeCleanupClosesChildEndsThenBindsImage()
    {
        var api = new FakeApi();
        using var executable = Executable(api);
        using var pipes = Pipes(api);
        var factory = Factory(api);

        using var process = factory.Create(Frozen(), executable, pipes);

        Assert.Equal(104, Marshal.SizeOf<Win32StartupInfo>());
        Assert.Equal(112, Marshal.SizeOf<Win32StartupInfoEx>());
        Assert.Equal(104, Marshal.OffsetOf<Win32StartupInfoEx>(nameof(Win32StartupInfoEx.AttributeList)).ToInt32());
        Assert.Equal(24, Marshal.SizeOf<Win32ProcessInformation>());
        var call = Assert.IsType<EvidenceBuildRawCreateCall>(api.LastCall);
        Assert.Equal(BaseProjectileEvidenceLaunchPlanner.MsBuildPath, call.ApplicationName);
        Assert.Equal(Root, call.CurrentDirectory);
        Assert.True(call.InheritHandles);
        Assert.True(call.ProcessSecurityAttributesNull);
        Assert.True(call.ThreadSecurityAttributesNull);
        Assert.Equal(EvidenceBuildCreatePolicy.ExactFlags, call.CreationFlags);
        Assert.False(call.CreationFlags.HasFlag(EvidenceCreateProcessFlags.CreateNewProcessGroup));
        Assert.Equal(112u, call.StartupInfo.StartupInfo.Size);
        Assert.Null(call.StartupInfo.StartupInfo.Reserved);
        Assert.Null(call.StartupInfo.StartupInfo.Desktop);
        Assert.Null(call.StartupInfo.StartupInfo.Title);
        Assert.Equal(EvidenceBuildCreatePolicy.StartfUseStdHandles, call.StartupInfo.StartupInfo.Flags);
        Assert.Equal((nint)10, call.StartupInfo.StartupInfo.StandardInput);
        Assert.Equal((nint)21, call.StartupInfo.StartupInfo.StandardOutput);
        Assert.Equal((nint)31, call.StartupInfo.StartupInfo.StandardError);
        Assert.Equal(new nint[] { 10, 21, 31 }, call.ChildHandles);
        Assert.Equal('\0', call.MutableCommandLine[^1]);
        Assert.DoesNotContain('\0', call.MutableCommandLine[..^1]);
        Assert.Equal('\0', call.UnicodeEnvironmentBlock[^1]);
        Assert.Equal('\0', call.UnicodeEnvironmentBlock[^2]);
        Assert.NotEqual(call.CommandLineAddress, call.EnvironmentAddress);
        Assert.Equal(ExpectedIdentity(), process.MatchedIdentity);
        process.Borrow(borrow =>
        {
            Assert.Equal((nint)200, borrow.ProcessHandle);
            Assert.Equal((nint)201, borrow.ThreadHandle);
            Assert.True(borrow.PreJobArmed);
            Assert.True(borrow.PreResumeArmed);
        });
        Assert.True(api.Events.IndexOf("invoke") < api.Events.IndexOf("dispose-pin:environment"));
        Assert.True(api.Events.IndexOf("free:500") < api.Events.IndexOf("close:10"));
        Assert.True(api.Events.IndexOf("close:31") < api.Events.IndexOf("image-open"));
        Assert.Equal(2, api.ImageReads);
        Assert.Equal(0, api.TerminateCalls);
    }

    [Theory]
    [InlineData("ambient")]
    [InlineData("suspended")]
    [InlineData("command")]
    [InlineData("environment")]
    [InlineData("working-directory")]
    [InlineData("application")]
    [InlineData("invocation-environment")]
    public void FrozenRequestDriftRefusesBeforeAnyNativeFacingEffect(string mutation)
    {
        var api = new FakeApi();
        using var executable = Executable(api);
        using var pipes = Pipes(api);
        var request = Mutate(Frozen(), mutation);
        var effectsBefore = api.CreateEffects;

        Assert.ThrowsAny<Exception>(() => Factory(api).Create(request, executable, pipes));

        Assert.Equal(effectsBefore, api.CreateEffects);
        Assert.Null(api.LastCall);
    }

    [Theory]
    [InlineData("query-success")]
    [InlineData("query-error")]
    [InlineData("query-zero")]
    [InlineData("query-large")]
    public void AttributeSizeQueryMustBeFalse122NonzeroAndBounded(string failure)
    {
        var api = new FakeApi { Failure = failure };
        using var executable = Executable(api);
        using var pipes = Pipes(api);

        Assert.ThrowsAny<Exception>(() => Factory(api).Create(Frozen(), executable, pipes));

        Assert.Null(api.LastCall);
        Assert.DoesNotContain(api.Events, item => item.StartsWith("alloc:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("list-allocate", "")]
    [InlineData("initialize", "free:500")]
    [InlineData("value-allocate", "delete,free:500")]
    [InlineData("write", "delete,free:600,free:500")]
    [InlineData("update", "delete,free:600,free:500")]
    [InlineData("command-pin", "delete,free:600,free:500")]
    [InlineData("environment-pin", "dispose-pin:command,delete,free:600,free:500")]
    public void AttributeAndPinFailuresUseExactReverseCleanup(string failure, string cleanupCsv)
    {
        var api = new FakeApi { Failure = failure };
        using var executable = Executable(api);
        using var pipes = Pipes(api);

        Assert.ThrowsAny<Exception>(() => Factory(api).Create(Frozen(), executable, pipes));

        var expected = string.IsNullOrEmpty(cleanupCsv) ? [] : cleanupCsv.Split(',');
        Assert.Equal(expected, api.Events.Where(item =>
            item == "delete" || item.StartsWith("free:", StringComparison.Ordinal) ||
            item.StartsWith("dispose-pin:", StringComparison.Ordinal)));
        Assert.Null(api.LastCall);
    }

    [Theory]
    [InlineData("command-pin-zero")]
    [InlineData("command-pin-count")]
    [InlineData("environment-pin-zero")]
    [InlineData("environment-pin-alias")]
    [InlineData("environment-pin-count")]
    public void InvalidPinFactsRefuseBeforeInvocationAndDisposeEveryAcquiredPin(string failure)
    {
        var api = new FakeApi { Failure = failure };
        using var executable = Executable(api);
        using var pipes = Pipes(api);

        Assert.ThrowsAny<Exception>(() => Factory(api).Create(Frozen(), executable, pipes));

        Assert.Null(api.LastCall);
        Assert.Contains("dispose-pin:command", api.Events);
        Assert.Equal(failure.StartsWith("environment", StringComparison.Ordinal),
            api.Events.Contains("dispose-pin:environment"));
        Assert.Equal(api.Events.IndexOf("delete"), api.Events.IndexOf("free:600") - 1);
        Assert.True(api.Events.IndexOf("free:600") < api.Events.IndexOf("free:500"));
    }

    [Fact]
    public void PostCreateCleanupAggregatesEveryFailureBeforeTerminatingOwnedChild()
    {
        var api = new FakeApi
        {
            Failure = "environment-dispose,command-dispose,delete,free-value,free-list",
        };
        api.CloseFailures.Add(21);
        using var executable = Executable(api);
        using var pipes = Pipes(api);

        var failure = Assert.ThrowsAny<Exception>(() => Factory(api).Create(Frozen(), executable, pipes));

        var messages = Flatten(failure).Select(item => item.Message).ToArray();
        Assert.Contains(messages, message => message.Contains("environment dispose", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("command dispose", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("delete failure", StringComparison.Ordinal));
        Assert.Equal(2, messages.Count(message => message.Contains("free failure", StringComparison.Ordinal)));
        Assert.Equal(1, api.TerminateCalls);
        Assert.Equal(1, api.CloseCalls.Count(handle => handle == 21));
        Assert.Contains((nint)201, api.CloseCalls);
        Assert.Contains((nint)200, api.CloseCalls);
        Assert.DoesNotContain("image-open", api.Events);
    }

    [Theory]
    [InlineData(false, 0, 0, 0, 0)]
    [InlineData(false, 200, 0, 300, 0)]
    [InlineData(false, 0, 201, 0, 301)]
    [InlineData(true, 200, 200, 300, 301)]
    [InlineData(true, 10, 201, 300, 301)]
    [InlineData(true, 101, 201, 300, 301)]
    [InlineData(true, 200, 31, 300, 301)]
    [InlineData(true, 200, 201, 0, 301)]
    public void RawResultCartesianNeverClosesProtectedAliasesAndNeverBindsImage(
        bool succeeded, int process, int thread, uint processId, uint threadId)
    {
        var api = new FakeApi
        {
            RawResult = new EvidenceBuildRawSuspendedProcessResult(
                succeeded, process, thread, processId, threadId, succeeded ? 0 : 5),
        };
        using var executable = Executable(api);
        using var pipes = Pipes(api);

        Assert.ThrowsAny<Exception>(() => Factory(api).Create(Frozen(), executable, pipes));

        Assert.DoesNotContain(api.CloseCalls, handle => handle is 10 or 20 or 21 or 30 or 31 or 100 or 101);
        Assert.DoesNotContain("image-open", api.Events);
        Assert.Equal(api.CloseCalls.Distinct().Count(), api.CloseCalls.Count);
    }

    [Theory]
    [InlineData("environment-dispose")]
    [InlineData("command-dispose")]
    [InlineData("delete")]
    [InlineData("free-value")]
    [InlineData("free-list")]
    public void CleanupFailureAfterNativeSuccessStillOwnsMarksAndTerminatesChild(string failure)
    {
        var api = new FakeApi { Failure = failure };
        using var executable = Executable(api);
        using var pipes = Pipes(api);

        Assert.ThrowsAny<Exception>(() => Factory(api).Create(Frozen(), executable, pipes));

        Assert.NotNull(api.LastCall);
        Assert.Equal(1, api.TerminateCalls);
        Assert.Equal(new nint[] { 10, 21, 31 }, api.CloseCalls.Take(3));
        Assert.Contains((nint)201, api.CloseCalls);
        Assert.Contains((nint)200, api.CloseCalls);
        Assert.DoesNotContain("image-open", api.Events);
    }

    [Theory]
    [InlineData("path")]
    [InlineData("exists")]
    [InlineData("bound")]
    [InlineData("reparse")]
    [InlineData("identity")]
    [InlineData("sha")]
    [InlineData("file-version")]
    [InlineData("product-version")]
    [InlineData("drift")]
    [InlineData("image-read")]
    [InlineData("image-dispose")]
    public void ImageMismatchDriftReadOrCloseFailureTerminatesStillSuspendedChild(string failure)
    {
        var api = new FakeApi { ImageFailure = failure };
        using var executable = Executable(api);
        using var pipes = Pipes(api);

        Assert.ThrowsAny<Exception>(() => Factory(api).Create(Frozen(), executable, pipes));

        Assert.Equal(1, api.TerminateCalls);
        Assert.Contains((nint)201, api.CloseCalls);
        Assert.Contains((nint)200, api.CloseCalls);
        Assert.Equal(1, api.ImageDisposeCalls);
    }

    [Fact]
    public void ChildEndMilestoneAmbiguityTerminatesAdoptedChildAndDoesNotQueryImage()
    {
        var api = new FakeApi();
        api.CloseFailures.Add(21);
        using var executable = Executable(api);
        using var pipes = Pipes(api);

        Assert.ThrowsAny<Exception>(() => Factory(api).Create(Frozen(), executable, pipes));

        Assert.Equal(1, api.TerminateCalls);
        Assert.DoesNotContain("image-open", api.Events);
        Assert.Equal(1, api.CloseCalls.Count(handle => handle == 21));
        Assert.True(EvidenceBuildProcessExitHandleRetention.Contains(21));
    }

    [Fact]
    public void ExecutableThenPipeBorrowLocksBlockConcurrentDisposeUntilRawCreateReturns()
    {
        var api = new FakeApi { GateInvoke = true };
        var executable = Executable(api);
        var pipes = Pipes(api);
        EvidenceBuildMatchedSuspendedProcessLease? result = null;
        Exception? createFailure = null;
        using var executableDisposeEntered = new ManualResetEventSlim();
        using var pipeDisposeEntered = new ManualResetEventSlim();
        var createThread = new Thread(() =>
        {
            try { result = Factory(api).Create(Frozen(), executable, pipes); }
            catch (Exception exception) { createFailure = exception; }
        }) { IsBackground = true };
        var executableDisposer = new Thread(() =>
        {
            executableDisposeEntered.Set();
            executable.Dispose();
        }) { IsBackground = true };
        var pipeDisposer = new Thread(() =>
        {
            pipeDisposeEntered.Set();
            pipes.Dispose();
        }) { IsBackground = true };
        createThread.Start();
        Assert.True(api.InvokeEntered.Wait(TimeSpan.FromSeconds(2)));
        executableDisposer.Start();
        pipeDisposer.Start();
        try
        {
            Assert.True(executableDisposeEntered.Wait(TimeSpan.FromSeconds(2)));
            Assert.True(pipeDisposeEntered.Wait(TimeSpan.FromSeconds(2)));
            Assert.False(executableDisposer.Join(TimeSpan.FromMilliseconds(50)));
            Assert.False(pipeDisposer.Join(TimeSpan.FromMilliseconds(50)));
        }
        finally
        {
            api.ReleaseInvoke.Set();
            createThread.Join(TimeSpan.FromSeconds(2));
            executableDisposer.Join(TimeSpan.FromSeconds(2));
            pipeDisposer.Join(TimeSpan.FromSeconds(2));
        }
        Assert.False(createThread.IsAlive);
        Assert.False(executableDisposer.IsAlive);
        Assert.False(pipeDisposer.IsAlive);
        Assert.Null(createFailure);
        result!.Dispose();
    }

    private static EvidenceBuildSuspendedCreateImageFactory Factory(FakeApi api) =>
        new(api, new FakeKernelOwner(), new FakeProcessOwner());

    private static IEnumerable<Exception> Flatten(Exception exception)
    {
        if (exception is AggregateException aggregate)
        {
            return aggregate.InnerExceptions.SelectMany(Flatten);
        }
        return [exception];
    }

    private static EvidenceFrozenBuildProcessRequest Frozen() =>
        EvidenceBuildProcessPrimitives.Compile(new EvidenceBuildProcessRequest(
            new EvidenceBuildInvocation(
                BaseProjectileEvidenceLaunchPlanner.MsBuildPath,
                Root,
                Arguments(),
                new ReadOnlyDictionary<string, string>(InvocationEnvironment())),
            new ReadOnlyDictionary<string, string>(EffectiveEnvironment()),
            false,
            true,
            new EvidenceBuildJobLimits(0x3, 1, 768L * 1024 * 1024, 1024L * 1024 * 1024, true),
            false,
            true,
            true,
            true,
            TimeSpan.FromMinutes(5),
            EvidenceBuildProcessPrimitives.StreamCapBytes,
            EvidenceBuildProcessPrimitives.StreamCapBytes));

    private static EvidenceFrozenBuildProcessRequest Mutate(
        EvidenceFrozenBuildProcessRequest request,
        string mutation) => mutation switch
        {
            "ambient" => request with { InheritAmbientEnvironment = true },
            "suspended" => request with { StartSuspended = false },
            "command" => request with { CommandLine = request.CommandLine + " drift" },
            "environment" => request with { UnicodeEnvironmentBlock = request.UnicodeEnvironmentBlock + "x" },
            "working-directory" => request with { WorkingDirectory = @"C:\other" },
            "application" => request with { ApplicationName = @"C:\other\MSBuild.exe" },
            "invocation-environment" => request with
            {
                Invocation = request.Invocation with
                {
                    Environment = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
                    {
                        ["DOTNET_PROCESSOR_COUNT"] = "1",
                    }),
                },
            },
            _ => throw new InvalidOperationException(),
        };

    private static EvidenceBuildRetainedExecutableLease Executable(FakeApi api)
    {
        var paths = new Stack<string>();
        var current = Directory.GetParent(BaseProjectileEvidenceLaunchPlanner.MsBuildPath);
        while (current is not null)
        {
            paths.Push(current.FullName);
            current = current.Parent;
        }
        var pathArray = paths.ToArray();
        var handles = Enumerable.Range(0, pathArray.Length).Select(index => (nint)(101 + index)).ToArray();
        var ancestors = pathArray.Select((path, index) =>
            new EvidenceBuildExecutableAncestorIdentity(path, $"ancestor-{index}", true, true, true)).ToArray();
        return new EvidenceBuildRetainedExecutableLease(
            api,
            new FakeKernelOwner(),
            100,
            ExpectedIdentity(),
            handles,
            ancestors);
    }

    private static EvidenceBuildPipeHandleBundle Pipes(FakeApi api) =>
        new EvidenceBuildPipeHandleFactory(api, new FakeKernelOwner()).Create();

    private static EvidenceOpenedExecutableIdentity ExpectedIdentity() => new(
        BaseProjectileEvidenceLaunchPlanner.MsBuildPath,
        true,
        true,
        false,
        "volume:1:file:msbuild",
        new string('a', 64),
        "17.14.40.0",
        "17.14.40");

    private static string[] Arguments() =>
    [
        @"game\Rounds.Game.csproj", "/noAutoResponse", "/t:Rebuild", "/p:Configuration=Debug",
        "/p:Restore=false", "/p:UseSharedCompilation=false", "/p:BuildProjectReferences=true",
        "/m:1", "/nr:false", "/v:minimal", "/warnaserror",
    ];

    private static Dictionary<string, string> InvocationEnvironment() => new(StringComparer.Ordinal)
    {
        ["DOTNET_PROCESSOR_COUNT"] = "2",
        ["MSBUILDDISABLENODEREUSE"] = "1",
        ["MSBuildEnableWorkloadResolver"] = "false",
        ["MSBuildSDKsPath"] = @"C:\repo\.tools\dotnet\sdk\8.0.423\Sdks",
    };

    private static Dictionary<string, string> EffectiveEnvironment()
    {
        var values = InvocationEnvironment();
        values["SystemRoot"] = @"C:\Windows";
        values["WINDIR"] = @"C:\Windows";
        values["TEMP"] = @"C:\Temp";
        values["TMP"] = @"C:\Temp";
        values["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        values["VSLANG"] = "1033";
        values["NUGET_PACKAGES"] = @"C:\repo\.tools\nuget-packages";
        values["DOTNET_CLI_HOME"] = @"C:\repo\.tools\dotnet-home";
        values["MSBuildUserExtensionsPath"] = @"C:\repo\.tools\empty\msbuild-user";
        return values;
    }

    private sealed class FakeKernelOwner : IEvidenceBuildKernelHandleCleanupOwner
    {
        public void Retain(EvidenceBuildAmbiguousKernelHandle handle, Exception failure) { }
    }

    private sealed class FakeProcessOwner : IEvidenceBuildSuspendedProcessCleanupOwner
    {
        public void Retain(EvidenceBuildSuspendedProcessOwner owner, Exception failure) { }
    }

    private sealed class FakePin(FakeApi api, string name, nint address, int count) :
        IEvidenceBuildPinnedCharacterLease
    {
        private bool _disposed;
        public nint Address => !_disposed ? address : throw new ObjectDisposedException(nameof(FakePin));
        public int CharacterCount => count;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            api.Events.Add($"dispose-pin:{name}");
            if (api.HasFailure($"{name}-dispose")) throw new IOException($"{name} dispose");
        }
    }

    private sealed class FakeImageLease(FakeApi api, EvidenceOpenedExecutableIdentity expected) :
        IEvidenceBuildChildImageLease
    {
        private bool _disposed;
        public EvidenceOpenedExecutableIdentity ReadSnapshot()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            api.Events.Add("image-read");
            api.ImageReads++;
            if (api.ImageFailure == "image-read") throw new IOException("image read");
            var identity = expected;
            if (api.ImageReads == 2 && api.ImageFailure == "drift")
            {
                identity = identity with { OpenedHandleIdentity = "drift" };
            }
            return api.ImageFailure switch
            {
                "path" => identity with { Path = @"C:\other\MSBuild.exe" },
                "exists" => identity with { Exists = false },
                "bound" => identity with { IdentityBound = false },
                "reparse" => identity with { IsReparsePoint = true },
                "identity" => identity with { OpenedHandleIdentity = "other" },
                "sha" => identity with { Sha256 = new string('b', 64) },
                "file-version" => identity with { FileVersion = "other" },
                "product-version" => identity with { ProductVersion = "other" },
                _ => identity,
            };
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            api.Events.Add("image-dispose");
            api.ImageDisposeCalls++;
            if (api.ImageFailure == "image-dispose") throw new IOException("image dispose");
        }
    }

    private sealed class FakeApi : IEvidenceBuildCreateApi, IEvidenceBuildPipeHandleApi
    {
        private int _pipe;
        private int _allocation;
        private int _pin;
        private readonly Dictionary<nint, uint> _flags = [];
        private readonly HashSet<nint> _allocations = [];
        internal List<string> Events { get; } = [];
        internal List<nint> CloseCalls { get; } = [];
        internal HashSet<nint> CloseFailures { get; } = [];
        internal string? Failure { get; init; }
        internal string? ImageFailure { get; init; }
        internal bool GateInvoke { get; init; }
        internal ManualResetEventSlim InvokeEntered { get; } = new();
        internal ManualResetEventSlim ReleaseInvoke { get; } = new();
        internal EvidenceBuildRawSuspendedProcessResult RawResult { get; init; } =
            new(true, 200, 201, 300, 301, 0);
        internal EvidenceBuildRawCreateCall? LastCall { get; private set; }
        internal int ImageReads { get; set; }
        internal int ImageDisposeCalls { get; set; }
        internal int TerminateCalls { get; private set; }
        internal int CreateEffects => Events.Count(item =>
            item.StartsWith("query", StringComparison.Ordinal) ||
            item.StartsWith("alloc", StringComparison.Ordinal) || item == "invoke");
        internal bool HasFailure(string value) =>
            Failure?.Split(',').Contains(value, StringComparer.Ordinal) == true;

        public EvidenceBuildAttributeSizeQuery QueryAttributeListSize(int attributeCount)
        {
            Events.Add($"query:{attributeCount}");
            return Failure switch
            {
                "query-success" => new(true, 128, 0),
                "query-error" => new(false, 128, 5),
                "query-zero" => new(false, 0, 122),
                "query-large" => new(false, 4097, 122),
                _ => new(false, 128, 122),
            };
        }

        public nint Allocate(nuint bytes)
        {
            Events.Add($"alloc:{bytes}");
            _allocation++;
            if (HasFailure("list-allocate") && _allocation == 1 ||
                HasFailure("value-allocate") && _allocation == 2) return 0;
            var handle = _allocation == 1 ? (nint)500 : 600;
            _allocations.Add(handle);
            return handle;
        }

        public void Free(nint memory)
        {
            Events.Add($"free:{memory}");
            _allocations.Remove(memory);
            if (HasFailure("free-value") && memory == 600 || HasFailure("free-list") && memory == 500)
            {
                throw new IOException("free failure");
            }
        }

        public bool InitializeAttributeList(nint attributeList, int attributeCount, nuint bytes, out int error)
        {
            Events.Add($"initialize:{attributeList}:{attributeCount}:{bytes}");
            error = HasFailure("initialize") ? 5 : 0;
            return error == 0;
        }

        public void WriteHandleList(nint memory, ImmutableArray<nint> handles, nuint bytes)
        {
            Events.Add($"write:{memory}:{bytes}:{string.Join(',', handles)}");
            Assert.Contains(memory, _allocations);
            Assert.Equal((nuint)EvidenceBuildCreatePolicy.HandleListBytesX64, bytes);
            Assert.Equal(new nint[] { 10, 21, 31 }, handles);
            if (HasFailure("write")) throw new IOException("write failure");
        }

        public bool UpdateAttribute(nint attributeList, nuint attribute, nint value, nuint bytes,
            uint flags, out int error)
        {
            Events.Add($"update:{attributeList}:{attribute:x}:{value}:{bytes}:{flags}");
            Assert.Equal(EvidenceBuildCreatePolicy.HandleListAttribute, attribute);
            Assert.Equal(0u, flags);
            error = HasFailure("update") ? 5 : 0;
            return error == 0;
        }

        public void DeleteAttributeList(nint attributeList)
        {
            Events.Add("delete");
            if (HasFailure("delete")) throw new IOException("delete failure");
        }

        public IEvidenceBuildPinnedCharacterLease Pin(char[] characters)
        {
            _pin++;
            var name = _pin == 1 ? "command" : "environment";
            Events.Add($"pin:{name}:{characters.Length}");
            if (HasFailure($"{name}-pin")) throw new IOException($"{name} pin");
            var address = _pin == 1 ? (nint)700 : 800;
            var count = characters.Length;
            if (HasFailure($"{name}-pin-zero")) address = 0;
            if (HasFailure("environment-pin-alias")) address = 700;
            if (HasFailure($"{name}-pin-count")) count--;
            return new FakePin(this, name, address, count);
        }

        public EvidenceBuildRawSuspendedProcessResult CreateProcessW(EvidenceBuildRawCreateCall request)
        {
            Events.Add("invoke");
            LastCall = request;
            Assert.True(_allocations.SetEquals([500, 600]));
            Assert.Equal((nint)500, request.StartupInfo.AttributeList);
            Assert.Equal((nint)700, request.CommandLineAddress);
            Assert.Equal((nint)800, request.EnvironmentAddress);
            if (GateInvoke)
            {
                InvokeEntered.Set();
                ReleaseInvoke.Wait();
            }
            return RawResult;
        }

        public IEvidenceBuildChildImageLease OpenChildImage(
            EvidenceBuildSuspendedProcessBorrow process,
            EvidenceOpenedExecutableIdentity expectedIdentity)
        {
            Events.Add("image-open");
            Assert.Equal((nint)200, process.ProcessHandle);
            Assert.Equal((nint)201, process.ThreadHandle);
            return new FakeImageLease(this, expectedIdentity);
        }

        public nint OpenFile(string path, uint access, uint share, ref EvidenceBuildSecurityAttributes security,
            uint disposition, uint attributes, out int error)
        {
            _flags[10] = 1;
            error = 0;
            return 10;
        }

        public bool CreatePipe(out nint readHandle, out nint writeHandle,
            ref EvidenceBuildSecurityAttributes security, uint size, out int error)
        {
            _pipe++;
            readHandle = _pipe == 1 ? 20 : 30;
            writeHandle = _pipe == 1 ? 21 : 31;
            _flags[readHandle] = 1;
            _flags[writeHandle] = 1;
            error = 0;
            return true;
        }

        public bool SetHandleInformation(nint handle, uint mask, uint flags, out int error)
        {
            _flags[handle] = flags;
            error = 0;
            return true;
        }

        public bool GetHandleInformation(nint handle, out uint flags, out int error)
        {
            flags = _flags[handle];
            error = 0;
            return true;
        }

        public uint GetFileType(nint handle, out int error)
        {
            error = 0;
            return handle == 10 ? 2u : 3u;
        }

        public bool TerminateProcess(nint process, uint exitCode, out int error)
        {
            Events.Add($"terminate:{process}");
            TerminateCalls++;
            error = 0;
            return true;
        }

        public uint WaitForSingleObject(nint process, uint milliseconds, out int error)
        {
            Events.Add($"wait:{process}:{milliseconds}");
            error = 0;
            return 0;
        }

        public bool CloseHandle(nint handle, out int error)
        {
            Events.Add($"close:{handle}");
            CloseCalls.Add(handle);
            error = CloseFailures.Contains(handle) ? 6 : 0;
            return error == 0;
        }

        public bool PeekPipe(nint handle, out uint available, out int error) =>
            throw new NotSupportedException();
        public bool ReadFile(nint handle, byte[] buffer, out uint read, out int error) =>
            throw new NotSupportedException();
    }
}
