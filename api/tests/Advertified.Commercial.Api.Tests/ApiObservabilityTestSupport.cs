using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Advertified.Commercial.Api.Tests;

internal sealed class CaptureLogProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLog> records = new();

    internal IReadOnlyCollection<CapturedLog> Records => records.ToArray();

    public ILogger CreateLogger(string categoryName) =>
        new CaptureLogger(categoryName, records);

    public void Dispose()
    {
    }

    private sealed class CaptureLogger(
        string category,
        ConcurrentQueue<CapturedLog> records) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            NoopScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);
            records.Enqueue(new CapturedLog(
                category,
                logLevel,
                eventId,
                formatter(state, exception),
                properties));
        }
    }

    private sealed class NoopScope : IDisposable
    {
        internal static readonly NoopScope Instance = new();

        public void Dispose()
        {
        }
    }
}

internal sealed record CapturedLog(
    string Category,
    LogLevel Level,
    EventId EventId,
    string Message,
    IReadOnlyDictionary<string, object?> Properties);

internal sealed class AspNetRequestMetricObserver : IDisposable
{
    private const string MeterName = "Microsoft.AspNetCore.Hosting";
    private const string InstrumentName = "http.server.request.duration";
    private readonly TaskCompletionSource<MetricSnapshot> completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly MeterListener listener = new();

    internal AspNetRequestMetricObserver()
    {
        listener.InstrumentPublished = (instrument, activeListener) =>
        {
            if (instrument.Meter.Name == MeterName && instrument.Name == InstrumentName)
            {
                activeListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>(RecordMeasurement);
        listener.Start();
    }

    internal Task<MetricSnapshot> WaitAsync() =>
        completion.Task.WaitAsync(TimeSpan.FromSeconds(5));

    public void Dispose() => listener.Dispose();

    private void RecordMeasurement(
        Instrument instrument,
        double measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
    {
        var copiedTags = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            copiedTags[tag.Key] = tag.Value;
        }

        completion.TrySetResult(new MetricSnapshot(measurement, copiedTags));
    }
}

internal sealed record MetricSnapshot(
    double Value,
    IReadOnlyDictionary<string, object?> Tags);

internal sealed class AspNetRequestActivityObserver : IDisposable
{
    private readonly string expectedCorrelationId;
    private readonly TaskCompletionSource<ActivitySnapshot> completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ActivityListener listener = new();

    internal AspNetRequestActivityObserver(string expectedCorrelationId)
    {
        this.expectedCorrelationId = expectedCorrelationId;
        listener.ShouldListenTo = source => source.Name == "Microsoft.AspNetCore";
        listener.Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
            ActivitySamplingResult.AllDataAndRecorded;
        listener.SampleUsingParentId = static (ref ActivityCreationOptions<string> _) =>
            ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStopped = RecordActivity;
        ActivitySource.AddActivityListener(listener);
    }

    internal Task<ActivitySnapshot> WaitAsync() =>
        completion.Task.WaitAsync(TimeSpan.FromSeconds(5));

    public void Dispose() => listener.Dispose();

    private void RecordActivity(Activity activity)
    {
        var correlationId = activity.GetTagItem("advertified.correlation_id")?.ToString();
        if (string.Equals(correlationId, expectedCorrelationId, StringComparison.Ordinal))
        {
            completion.TrySetResult(new ActivitySnapshot(
                activity.TraceId.ToString(),
                correlationId!));
        }
    }
}

internal sealed record ActivitySnapshot(string TraceId, string CorrelationId);
