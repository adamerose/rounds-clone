namespace Rounds.Game;

internal interface IAgentPlaytestFrameSource
{
    byte[] CapturePng(int sequence, bool terminal);
}

internal sealed class AgentPlaytestRendererUnavailableFrameSource : IAgentPlaytestFrameSource
{
    public byte[] CapturePng(int sequence, bool terminal)
    {
        _ = terminal;
        throw new AgentPlaytestFailure(sequence, "renderer", "renderer-unavailable", "No renderer-backed frame source is available.");
    }
}

internal interface IAgentPlaytestTurnTransport
{
    ValueTask<byte[]?> ReadRequestAsync(TimeSpan remaining, CancellationToken cancellationToken);
    void WriteResponse(AgentPlaytestResponse response);
    void WriteDiagnostic(string message);
}

internal interface IAgentPlaytestBoundedStreamReader
{
    ValueTask<int> ReadAsync(
        Stream input,
        Memory<byte> buffer,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

internal sealed class AgentPlaytestBoundedStreamReader : IAgentPlaytestBoundedStreamReader
{
    public static AgentPlaytestBoundedStreamReader Instance { get; } = new();

    public async ValueTask<int> ReadAsync(
        Stream input,
        Memory<byte> buffer,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (timeout <= TimeSpan.Zero)
        {
            throw new TimeoutException("The in-route playtest deadline expired while reading standard input.");
        }
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(timeout);
        try
        {
            var pending = input.ReadAsync(buffer, timeoutCancellation.Token).AsTask();
            return await pending.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The in-route playtest deadline expired while reading standard input.");
        }
        catch (TimeoutException)
        {
            timeoutCancellation.Cancel();
            throw;
        }
    }
}

internal sealed class AgentPlaytestStandardStreamTransport : IAgentPlaytestTurnTransport
{
    private const int MaximumRequestLineBytes = 16 * 1024;
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly TextWriter _error;
    private readonly IAgentPlaytestBoundedStreamReader _reader;

    public AgentPlaytestStandardStreamTransport(
        Stream input,
        Stream output,
        TextWriter error,
        IAgentPlaytestBoundedStreamReader? reader = null)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _error = error ?? throw new ArgumentNullException(nameof(error));
        _reader = reader ?? AgentPlaytestBoundedStreamReader.Instance;
    }

    public ValueTask<byte[]?> ReadRequestAsync(TimeSpan remaining, CancellationToken cancellationToken) =>
        ReadLineAsync(_input, remaining, cancellationToken);

    public void WriteResponse(AgentPlaytestResponse response)
    {
        var bytes = AgentPlaytestNdjson.SerializeResponse(response);
        _output.Write(bytes);
        _output.Flush();
    }

    public void WriteDiagnostic(string message) => _error.WriteLine(message);

    private async ValueTask<byte[]?> ReadLineAsync(
        Stream input,
        TimeSpan remaining,
        CancellationToken cancellationToken)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        using var line = new MemoryStream();
        var oneByte = new byte[1];
        while (line.Length <= MaximumRequestLineBytes)
        {
            var readRemaining = remaining - started.Elapsed;
            if (readRemaining <= TimeSpan.Zero)
            {
                throw new TimeoutException("The in-route playtest deadline expired while reading standard input.");
            }
            var count = await _reader.ReadAsync(input, oneByte, readRemaining, cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                return line.Length == 0 ? null : line.ToArray();
            }
            line.WriteByte(oneByte[0]);
            if (oneByte[0] == '\n')
            {
                return line.ToArray();
            }
        }
        return line.ToArray();
    }
}

internal sealed class AgentPlaytestProtocolLoop
{
    private readonly AgentPlaytestSession _session = new();
    private readonly AgentPlaytestTraceRecorder _trace = new(1UL);
    private readonly AgentPlaytestArtifactOwner _artifacts;
    private readonly IAgentPlaytestFrameSource _frames;
    private readonly IAgentPlaytestRgba8Decoder _decoder;
    private readonly Func<TimeSpan> _elapsed;
    private readonly AgentPlaytestManifestContext _manifestContext;
    private readonly List<AgentPlaytestFrameResponse> _publishedFrames = [];

    public AgentPlaytestProtocolLoop(
        AgentPlaytestArtifactOwner artifacts,
        IAgentPlaytestFrameSource frames,
        IAgentPlaytestRgba8Decoder decoder,
        Func<TimeSpan> elapsed,
        AgentPlaytestManifestContext manifestContext)
    {
        _artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
        _frames = frames ?? throw new ArgumentNullException(nameof(frames));
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _elapsed = elapsed ?? throw new ArgumentNullException(nameof(elapsed));
        _manifestContext = manifestContext ?? throw new ArgumentNullException(nameof(manifestContext));
    }

    public int Run(Stream standardInput, Stream standardOutput, TextWriter standardError) =>
        Run(new AgentPlaytestStandardStreamTransport(standardInput, standardOutput, standardError));

    public int Run(IAgentPlaytestTurnTransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);
        try
        {
            transport.WriteResponse(CaptureFrame(sequence: 0, terminal: false));
            while (!_session.IsTerminal)
            {
                var remaining = TimeSpan.FromSeconds(AgentPlaytestLimits.RouteTimeoutSeconds) - _elapsed();
                byte[]? line;
                try
                {
                    line = transport.ReadRequestAsync(remaining, CancellationToken.None).AsTask().GetAwaiter().GetResult();
                }
                catch (TimeoutException exception)
                {
                    throw new AgentPlaytestFailure(null, "lifecycle", "timeout", exception.Message);
                }
                if (line is null)
                {
                    throw new AgentPlaytestFailure(null, "terminal", "invalid-terminal", "Input ended before the supported terminal boundary.");
                }
                var parsed = AgentPlaytestNdjson.ParseRequest(line);
                if (!parsed.IsSuccess)
                {
                    var error = parsed.Error!;
                    throw new AgentPlaytestFailure(error.ErrorSequence, error.Stage, error.Code, "The request record is invalid.");
                }

                var accepted = _session.Apply(parsed.Request!, _elapsed());
                _trace.Record(accepted);
                var frameResponse = CaptureFrame(accepted.Sequence, accepted.Terminal);
                transport.WriteResponse(frameResponse);
            }

            var traceBytes = _trace.ToCanonicalBytes(requireTerminal: true);
            _artifacts.PublishTrace(traceBytes);
            var storedTraceBytes = _artifacts.ReadFinalTrace();
            var parsedTrace = AgentPlaytestTraceCodec.ParseCanonical(storedTraceBytes, requireTerminal: true);
            AgentPlaytestSession.VerifyFreshReplay(parsedTrace);
            var manifest = AgentPlaytestManifest.Create(
                _manifestContext,
                _publishedFrames,
                parsedTrace,
                storedTraceBytes);
            _artifacts.PublishManifest(AgentPlaytestManifestCodec.ToCanonicalBytes(manifest));
            return 0;
        }
        catch (AgentPlaytestFailure failure)
        {
            return Fail(transport, failure.Response, failure.Message);
        }
        catch (Exception exception)
        {
            return Fail(
                transport,
                AgentPlaytestErrors.Create(null, "simulation", "simulation-failed"),
                exception.Message);
        }
    }

    private AgentPlaytestFrameResponse CaptureFrame(int sequence, bool terminal)
    {
        var encoded = _frames.CapturePng(sequence, terminal);
        var published = _artifacts.PublishFrame(sequence, encoded, _decoder, terminal);
        _publishedFrames.Add(published.Response);
        return published.Response;
    }

    private int Fail(
        IAgentPlaytestTurnTransport transport,
        AgentPlaytestErrorResponse original,
        string diagnostic)
    {
        var response = original;
        try
        {
            _artifacts.CleanupFailedRun();
        }
        catch (Exception cleanupException)
        {
            diagnostic = $"{diagnostic} Cleanup failed: {cleanupException.Message}";
            response = AgentPlaytestErrors.Create(original.ErrorSequence, "lifecycle", "cleanup-failed");
        }
        transport.WriteDiagnostic(diagnostic);
        transport.WriteResponse(response);
        return 1;
    }
}
