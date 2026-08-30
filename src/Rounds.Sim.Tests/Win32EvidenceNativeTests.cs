using System.Runtime.InteropServices;
using Rounds.EvidenceLauncher;
using Rounds.Game;

namespace Rounds.Sim.Tests;

public sealed class Win32EvidenceNativeTests
{
    [Fact]
    public void Interop_layouts_match_64_bit_windows_abi()
    {
        Assert.Equal(8, IntPtr.Size);
        Assert.Equal(24, Marshal.SizeOf<Win32SecurityAttributes>());
        Assert.Equal(104, Marshal.SizeOf<Win32StartupInfo>());
        Assert.Equal(112, Marshal.SizeOf<Win32StartupInfoEx>());
        Assert.Equal(24, Marshal.SizeOf<Win32ProcessInformation>());
        Assert.Equal(104, Marshal.SizeOf<Win32MonitorInfoEx>());
        Assert.Equal(64, Marshal.SizeOf<Win32JobBasicLimitInformation>());
        Assert.Equal(144, Marshal.SizeOf<Win32JobExtendedLimitInformation>());
        Assert.Equal(24, Marshal.SizeOf<Win32FileIdInfo>());
        Assert.Equal(104, Marshal.OffsetOf<Win32StartupInfoEx>(nameof(Win32StartupInfoEx.AttributeList)).ToInt32());
    }

    [Fact]
    public void Constants_pin_extended_startup_handle_list_and_job_controls()
    {
        Assert.Equal(0x00020002U, Win32EvidenceConstants.ProcThreadAttributeHandleList);
        Assert.Equal(
            EvidenceCreateProcessFlags.CreateSuspended |
            EvidenceCreateProcessFlags.CreateNoWindow |
            EvidenceCreateProcessFlags.CreateNewProcessGroup |
            EvidenceCreateProcessFlags.BelowNormalPriorityClass |
            EvidenceCreateProcessFlags.ExtendedStartupInfoPresent |
            EvidenceCreateProcessFlags.CreateUnicodeEnvironment,
            Win32EvidenceConstants.RequiredCreateProcessFlags);
        Assert.Equal(0x00002000U, Win32EvidenceConstants.JobObjectLimitKillOnJobClose);
        Assert.Equal(0x00000100U, Win32EvidenceConstants.JobObjectLimitProcessMemory);
        Assert.Equal(0x00000200U, Win32EvidenceConstants.JobObjectLimitJobMemory);
    }

    [Fact]
    public void Armed_process_lease_terminates_waits_and_closes_both_handles_exactly_once()
    {
        var api = new FakeApi();
        var lease = new Win32ProcessLease(api, 101, 102);

        lease.Dispose();
        lease.Dispose();

        Assert.Equal(
            new[] { "terminate:101:1", "wait:101:4294967295", "close:102", "close:101" },
            api.Events);
    }

    [Fact]
    public void Verified_process_lease_disarms_fallback_without_skipping_handle_close()
    {
        var api = new FakeApi();
        var lease = new Win32ProcessLease(api, 201, 202);
        lease.MarkAssignedToKillOnCloseJob();
        lease.DisarmTerminationFallbackAfterVerifiedExitAndEmptyJob();

        lease.Dispose();

        Assert.Equal(new[] { "close:202", "close:201" }, api.Events);
    }

    [Fact]
    public void Desktop_executable_and_job_leases_close_exactly_once()
    {
        var api = new FakeApi();
        var executable = new Win32ExecutableLease(api, 301, ExecutableIdentity());
        var desktop = new Win32DesktopLease(api, 302, "RoundsEvidence-0123456789abcdef0123456789abcdef");
        var job = new Win32JobLease(api, 303);

        executable.Dispose();
        executable.Dispose();
        desktop.Dispose();
        desktop.Dispose();
        job.Dispose();
        job.Dispose();

        Assert.Equal(new[] { "close:301", "desktop-close:302", "close:303" }, api.Events);
    }

    [Fact]
    public void Handle_bundle_exposes_exact_allowlist_and_writes_one_ack_before_closing_all_handles()
    {
        var api = new FakeApi();
        var lease = new Win32LaunchHandleLease(
            api,
            new nint[] { 401, 402, 403, 404, 405, 406 },
            acknowledgementRead: 406,
            acknowledgementWrite: 407);

        lease.WriteAcknowledgementAndClose(DebugEvidenceCaptureProtocol.EvidenceAcknowledgement);
        lease.Dispose();
        lease.Dispose();

        Assert.Equal(
            new[]
            {
                EvidenceChildHandle.StandardInputRead,
                EvidenceChildHandle.StandardOutputWrite,
                EvidenceChildHandle.StandardErrorWrite,
                EvidenceChildHandle.AcknowledgementRead,
            },
            lease.ChildHandles.Select(handle => handle.Kind));
        Assert.All(lease.ChildHandles, handle => Assert.True(handle.Inheritable));
        Assert.Equal("406", lease.AcknowledgementReadHandleValue);
        Assert.Equal("write:407:06", api.Events[0]);
        Assert.Equal("close:407", api.Events[1]);
        Assert.Equal(1, api.Events.Count(value => value == "close:407"));
        Assert.Equal(8, api.Events.Count);
    }

    private static EvidenceOpenedExecutableIdentity ExecutableIdentity() =>
        new(
            @"C:\candidate\Godot.exe",
            true,
            true,
            false,
            "volume:1:file:2",
            new string('a', 64),
            "4.7.1",
            "4.7.1.stable.mono.official");

    private sealed class FakeApi : IWin32EvidenceApi
    {
        public List<string> Events { get; } = new();

        public bool CloseKernelHandle(nint handle)
        {
            Events.Add($"close:{handle}");
            return true;
        }

        public bool CloseDesktop(nint desktop)
        {
            Events.Add($"desktop-close:{desktop}");
            return true;
        }

        public bool TerminateProcess(nint process, uint exitCode)
        {
            Events.Add($"terminate:{process}:{exitCode}");
            return true;
        }

        public uint WaitForSingleObject(nint handle, uint milliseconds)
        {
            Events.Add($"wait:{handle}:{milliseconds}");
            return Win32EvidenceConstants.WaitObject0;
        }

        public bool WriteFile(nint handle, ReadOnlySpan<byte> data, out uint written)
        {
            Events.Add($"write:{handle}:{Convert.ToHexString(data).ToLowerInvariant()}");
            written = checked((uint)data.Length);
            return true;
        }
    }
}
