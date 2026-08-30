using System.ComponentModel;
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
        Assert.Equal(5_000U, Win32EvidenceConstants.TerminationFallbackWaitMilliseconds);
    }

    [Fact]
    public void Armed_process_lease_terminates_waits_and_closes_both_handles_exactly_once()
    {
        var api = new FakeApi();
        var lease = new Win32ProcessLease(api, 101, 102);

        lease.Dispose();
        lease.Dispose();

        Assert.Equal(
            new[] { "terminate:101:1", "wait:101:5000", "close:102", "close:101" },
            api.Events);
    }

    [Theory]
    [InlineData(false, Win32EvidenceConstants.WaitObject0)]
    [InlineData(true, Win32EvidenceConstants.WaitTimeout)]
    public void Failed_termination_or_wait_is_bounded_and_still_closes_process_handles(
        bool terminateResult,
        uint waitResult)
    {
        var api = new FakeApi
        {
            TerminateResult = terminateResult,
            WaitResult = waitResult,
        };
        var lease = new Win32ProcessLease(api, 111, 112);

        Assert.Throws<Win32Exception>(() => lease.Dispose());

        Assert.Equal(
            new[] { "terminate:111:1", "wait:111:5000", "close:112", "close:111" },
            api.Events);
        Assert.DoesNotContain($"wait:111:{Win32EvidenceConstants.Infinite}", api.Events);
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
    public void Successful_process_transition_closes_only_child_copies_to_enable_pipe_eof()
    {
        var api = new FakeApi();
        var lease = new Win32LaunchHandleLease(
            api,
            standardInputRead: 401,
            standardOutputRead: 402,
            standardOutputWrite: 403,
            standardErrorRead: 404,
            standardErrorWrite: 405,
            acknowledgementRead: 406,
            acknowledgementWrite: 407);

        lease.CompleteSuccessfulProcessCreation();
        lease.CompleteSuccessfulProcessCreation();
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
        Assert.Equal(
            new[] { "close:401", "close:403", "close:405", "close:406" },
            api.Events.Take(4));
        Assert.Equal("write:407:06", api.Events[4]);
        Assert.Equal("close:407", api.Events[5]);
        Assert.Equal(new[] { "close:402", "close:404" }, api.Events.Skip(6));
        Assert.Equal(1, api.Events.Count(value => value == "close:407"));
        Assert.Equal(8, api.Events.Count);
        Assert.Throws<ObjectDisposedException>(lease.CompleteSuccessfulProcessCreation);
    }

    [Fact]
    public void Acknowledgement_writer_closes_even_when_injected_write_throws()
    {
        var api = new FakeApi { ThrowOnWrite = true };
        var lease = new Win32LaunchHandleLease(
            api,
            standardInputRead: 501,
            standardOutputRead: 502,
            standardOutputWrite: 503,
            standardErrorRead: 504,
            standardErrorWrite: 505,
            acknowledgementRead: 506,
            acknowledgementWrite: 507);

        Assert.Throws<IOException>(() =>
            lease.WriteAcknowledgementAndClose(DebugEvidenceCaptureProtocol.EvidenceAcknowledgement));
        lease.Dispose();

        Assert.Equal("write:507:06", api.Events[0]);
        Assert.Equal("close:507", api.Events[1]);
        Assert.Equal(1, api.Events.Count(value => value == "close:507"));
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
        public bool TerminateResult { get; init; } = true;
        public uint WaitResult { get; init; } = Win32EvidenceConstants.WaitObject0;
        public bool ThrowOnWrite { get; init; }

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
            return TerminateResult;
        }

        public uint WaitForSingleObject(nint handle, uint milliseconds)
        {
            Events.Add($"wait:{handle}:{milliseconds}");
            return WaitResult;
        }

        public bool WriteFile(nint handle, ReadOnlySpan<byte> data, out uint written)
        {
            Events.Add($"write:{handle}:{Convert.ToHexString(data).ToLowerInvariant()}");
            if (ThrowOnWrite) throw new IOException("injected write failure");
            written = checked((uint)data.Length);
            return true;
        }
    }
}
