using Rounds.EvidenceLauncher;
using Rounds.Game;

namespace Rounds.Sim.Tests;

public sealed class Win32EvidenceFactoryTests
{
    private const string Nonce = "0123456789abcdef0123456789abcdef";
    private const string DesktopName = "RoundsEvidence-" + Nonce;

    [Fact]
    public void Topology_sets_and_checks_per_monitor_v2_before_exact_ordinal_enumeration()
    {
        var api = new FakeTopologyApi(
            new Win32MonitorSnapshot(@"\\.\DISPLAY1", new EvidencePixelBounds(0, 0, 100, 100)),
            new Win32MonitorSnapshot(@"\\.\DISPLAY2", new EvidencePixelBounds(100, 0, 100, 100)),
            new Win32MonitorSnapshot(@"\\.\DISPLAY3", new EvidencePixelBounds(200, 0, 100, 100)),
            new Win32MonitorSnapshot(
                BaseProjectileEvidenceLaunchPlanner.DisplayDevice,
                BaseProjectileEvidenceLaunchPlanner.RequiredMonitorBounds));

        var facts = new Win32TopologyFactory(api).ReadRequiredMonitor();

        Assert.Equal(new[] { "dpi-set", "dpi-check", "enumerate" }, api.Events);
        Assert.Equal(BaseProjectileEvidenceLaunchPlanner.DisplayDevice, facts.DeviceName);
        Assert.Equal(3, facts.Ordinal);
        Assert.Equal(BaseProjectileEvidenceLaunchPlanner.RequiredMonitorBounds, facts.PhysicalBounds);
        Assert.True(facts.PerMonitorV2DpiAware);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Topology_refuses_before_enumeration_when_effective_context_is_not_pmv2(
        bool setResult)
    {
        var api = new FakeTopologyApi { SetResult = setResult, CheckResult = false };

        Assert.Throws<InvalidOperationException>(() =>
            new Win32TopologyFactory(api).ReadRequiredMonitor());

        Assert.Equal(new[] { "dpi-set", "dpi-check" }, api.Events);
        Assert.DoesNotContain("enumerate", api.Events);
    }

    [Fact]
    public void Topology_accepts_set_failure_when_effective_context_is_exact_pmv2()
    {
        var api = new FakeTopologyApi(new Win32MonitorSnapshot(
            BaseProjectileEvidenceLaunchPlanner.DisplayDevice,
            BaseProjectileEvidenceLaunchPlanner.RequiredMonitorBounds))
        {
            SetResult = false,
            CheckResult = true,
        };

        var facts = new Win32TopologyFactory(api).ReadRequiredMonitor();

        Assert.True(facts.PerMonitorV2DpiAware);
        Assert.Equal(new[] { "dpi-set", "dpi-check", "enumerate" }, api.Events);
    }

    [Fact]
    public void Topology_collection_is_repeatable_after_process_dpi_was_already_established()
    {
        var api = new FakeTopologyApi(new Win32MonitorSnapshot(
            BaseProjectileEvidenceLaunchPlanner.DisplayDevice,
            BaseProjectileEvidenceLaunchPlanner.RequiredMonitorBounds))
        {
            SetResults = new[] { true, false },
            CheckResult = true,
        };
        var factory = new Win32TopologyFactory(api);

        var first = factory.ReadRequiredMonitor();
        var second = factory.ReadRequiredMonitor();

        Assert.Equal(first, second);
        Assert.Equal(
            new[] { "dpi-set", "dpi-check", "enumerate", "dpi-set", "dpi-check", "enumerate" },
            api.Events);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Topology_refuses_missing_or_duplicate_exact_display4(int matchingMonitorCount)
    {
        var monitors = Enumerable.Range(0, matchingMonitorCount)
            .Select(_ => new Win32MonitorSnapshot(
                BaseProjectileEvidenceLaunchPlanner.DisplayDevice,
                BaseProjectileEvidenceLaunchPlanner.RequiredMonitorBounds))
            .ToArray();
        var api = new FakeTopologyApi(monitors);

        Assert.Throws<InvalidOperationException>(() =>
            new Win32TopologyFactory(api).ReadRequiredMonitor());

        Assert.Equal(new[] { "dpi-set", "dpi-check", "enumerate" }, api.Events);
    }

    [Fact]
    public void Input_desktop_identity_is_read_from_opened_handle_and_always_closed()
    {
        var api = new FakeDesktopApi { InputDesktopName = "Default", InputDesktopHandle = 601 };
        var factory = new Win32DesktopFactory(api, new FakeWorkerContext(false));

        var identity = factory.ReadInputDesktopIdentity();

        Assert.Equal("Default", identity);
        Assert.Equal(new[] { "input-open", "name-read:601", "desktop-close:601" }, api.Events);
    }

    [Fact]
    public void Dedicated_worker_marks_only_its_background_thread_as_the_native_boundary()
    {
        var callerThread = Environment.CurrentManagedThreadId;
        var worker = new Win32DedicatedWorker();

        var observation = worker.Run(() => new
        {
            IsDedicated = worker.IsCurrentDedicatedWorker,
            Thread = Environment.CurrentManagedThreadId,
        });

        Assert.True(observation.IsDedicated);
        Assert.NotEqual(callerThread, observation.Thread);
        Assert.False(worker.IsCurrentDedicatedWorker);
    }

    [Fact]
    public void Input_desktop_read_failure_still_closes_the_exact_opened_handle()
    {
        var api = new FakeDesktopApi
        {
            InputDesktopHandle = 611,
            ThrowOnNameRead = true,
        };
        var factory = new Win32DesktopFactory(api, new FakeWorkerContext(false));

        Assert.Throws<IOException>(factory.ReadInputDesktopIdentity);

        Assert.Equal(new[] { "input-open", "name-read:611", "desktop-close:611" }, api.Events);
    }

    [Fact]
    public void Private_desktop_requires_worker_and_exact_nonce_before_any_native_call()
    {
        var api = new FakeDesktopApi();

        Assert.Throws<InvalidOperationException>(() =>
            new Win32DesktopFactory(api, new FakeWorkerContext(false))
                .CreatePrivateDesktopForNonce(Nonce));
        Assert.Empty(api.Events);

        Assert.Throws<ArgumentException>(() =>
            new Win32DesktopFactory(api, new FakeWorkerContext(true))
                .CreatePrivateDesktopForNonce(Nonce.ToUpperInvariant()));
        Assert.Empty(api.Events);
    }

    [Fact]
    public void Private_desktop_uses_exact_non_switching_access_and_lease_closes_exactly_once()
    {
        var api = new FakeDesktopApi
        {
            CreatedDesktopHandle = 621,
            CreatedDesktopName = DesktopName,
        };
        var factory = new Win32DesktopFactory(api, new FakeWorkerContext(true));

        var lease = factory.CreatePrivateDesktopForNonce(Nonce);
        lease.Dispose();
        lease.Dispose();

        Assert.Equal(DesktopName, lease.Name);
        Assert.Equal(
            new[]
            {
                $"desktop-create:{DesktopName}:{Win32EvidenceConstants.RequiredDesktopAccess}",
                "name-read:621",
                "desktop-close:621",
            },
            api.Events);
        Assert.Equal(0U, Win32EvidenceConstants.RequiredDesktopAccess & Win32EvidenceConstants.DesktopSwitchDesktop);
        Assert.Equal(
            Win32EvidenceConstants.DesktopCreateWindow |
            Win32EvidenceConstants.DesktopReadObjects |
            Win32EvidenceConstants.DesktopWriteObjects,
            Win32EvidenceConstants.RequiredDesktopAccess);
    }

    [Fact]
    public void Private_desktop_name_mismatch_closes_handle_and_exposes_no_lease()
    {
        var api = new FakeDesktopApi
        {
            CreatedDesktopHandle = 631,
            CreatedDesktopName = "unexpected",
        };
        var factory = new Win32DesktopFactory(api, new FakeWorkerContext(true));

        Assert.Throws<InvalidOperationException>(() =>
            factory.CreatePrivateDesktopForNonce(Nonce));

        Assert.Equal(
            new[]
            {
                $"desktop-create:{DesktopName}:{Win32EvidenceConstants.RequiredDesktopAccess}",
                "name-read:631",
                "desktop-close:631",
            },
            api.Events);
    }

    [Fact]
    public void Desktop_shim_exposes_no_activation_or_thread_desktop_mutation_operation()
    {
        var methods = typeof(IWin32DesktopApi).GetMethods().Select(method => method.Name).ToArray();

        Assert.DoesNotContain("SwitchDesktop", methods);
        Assert.DoesNotContain("SetThreadDesktop", methods);
        Assert.DoesNotContain("SetForegroundWindow", methods);
        Assert.DoesNotContain("ShowWindow", methods);
    }

    private sealed class FakeTopologyApi : IWin32TopologyApi
    {
        private readonly IReadOnlyList<Win32MonitorSnapshot> _monitors;

        internal FakeTopologyApi(params Win32MonitorSnapshot[] monitors)
        {
            _monitors = monitors;
        }

        internal List<string> Events { get; } = new();
        internal bool SetResult { get; init; } = true;
        internal IReadOnlyList<bool>? SetResults { get; init; }
        internal bool CheckResult { get; init; } = true;
        private int SetCallCount { get; set; }

        public bool SetPerMonitorV2DpiAwareness()
        {
            Events.Add("dpi-set");
            var result = SetResults is not null && SetCallCount < SetResults.Count
                ? SetResults[SetCallCount]
                : SetResult;
            SetCallCount++;
            return result;
        }

        public bool IsPerMonitorV2DpiAware()
        {
            Events.Add("dpi-check");
            return CheckResult;
        }

        public IReadOnlyList<Win32MonitorSnapshot> EnumerateMonitors()
        {
            Events.Add("enumerate");
            return _monitors;
        }
    }

    private sealed record FakeWorkerContext(bool IsCurrentDedicatedWorker) :
        IWin32DedicatedWorkerContext;

    private sealed class FakeDesktopApi : IWin32DesktopApi
    {
        internal List<string> Events { get; } = new();
        internal nint InputDesktopHandle { get; init; } = 600;
        internal string InputDesktopName { get; init; } = "Default";
        internal nint CreatedDesktopHandle { get; init; } = 620;
        internal string CreatedDesktopName { get; init; } = DesktopName;
        internal bool ThrowOnNameRead { get; init; }

        public nint OpenInputDesktop()
        {
            Events.Add("input-open");
            return InputDesktopHandle;
        }

        public nint CreateDesktop(string name, uint desiredAccess)
        {
            Events.Add($"desktop-create:{name}:{desiredAccess}");
            return CreatedDesktopHandle;
        }

        public string ReadDesktopName(nint desktop)
        {
            Events.Add($"name-read:{desktop}");
            if (ThrowOnNameRead) throw new IOException("injected name read failure");
            return desktop == InputDesktopHandle ? InputDesktopName : CreatedDesktopName;
        }

        public bool CloseDesktop(nint desktop)
        {
            Events.Add($"desktop-close:{desktop}");
            return true;
        }
    }
}
