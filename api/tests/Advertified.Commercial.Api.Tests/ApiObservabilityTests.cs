using System.Diagnostics;
using System.Globalization;
using System.Net;
using Advertified.Commercial.Api.Observability;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

[Collection(ApiObservabilityTestGroup.Name)]
public sealed class ApiObservabilityTests
{
    private const int CompletionEventId = 12_003;
    private const string UnavailableConnection =
        "Host=127.0.0.1;Port=1;Database=closed;Username=closed;Password=closed;Timeout=1";

    [Fact]
    public async Task MatchedRequestEmitsCorrelatedLogActivityAndFrameworkMetric()
    {
        const string correlationId = "ab000000-0000-0000-0000-000000000001";
        using var logs = new CaptureLogProvider();
        using var metrics = new AspNetRequestMetricObserver();
        using var activities = new AspNetRequestActivityObserver(correlationId);
        await using var factory = CreateFactory(logs);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-ID", correlationId);

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(correlationId, response.Headers.GetValues("X-Correlation-ID").Single());

        var log = SingleCompletion(logs);
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.Equal("GET", Property<string>(log, "Method"));
        Assert.Equal("/health/live", Property<string>(log, "RouteTemplate"));
        Assert.Equal(StatusCodes.Status200OK, Property<int>(log, "StatusCode"));
        var duration = Property<double>(log, "DurationMilliseconds");
        Assert.True(double.IsFinite(duration));
        Assert.True(duration >= 0);
        Assert.Equal(correlationId, Property<string>(log, "CorrelationId"));

        var traceId = Property<string>(log, "TraceId");
        AssertValidTraceId(traceId);
        var activity = await activities.WaitAsync();
        Assert.Equal(correlationId, activity.CorrelationId);
        Assert.Equal(traceId, activity.TraceId);

        var metric = await metrics.WaitAsync();
        Assert.True(double.IsFinite(metric.Value));
        Assert.True(metric.Value >= 0);
        Assert.Equal("GET", Tag<string>(metric, "http.request.method"));
        Assert.Equal("/health/live", Tag<string>(metric, "http.route"));
        Assert.Equal(StatusCodes.Status200OK, Tag<int>(metric, "http.response.status_code"));
        Assert.DoesNotContain(metric.Tags.Keys, key =>
            key.Contains("correlation", StringComparison.OrdinalIgnoreCase));

        AssertJsonConsoleConfiguration(factory.Services.GetRequiredService<IConfiguration>());
    }

    [Fact]
    public async Task UnmatchedRequestDoesNotExposeRawRequestValues()
    {
        const string correlationId = "ab000000-0000-0000-0000-000000000002";
        const string pathSecret = "tenant-secret-4fb89c";
        const string querySecret = "client-secret-328db1";
        using var logs = new CaptureLogProvider();
        using var metrics = new AspNetRequestMetricObserver();
        await using var factory = CreateFactory(logs);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            new HttpMethod("BREW"),
            $"/missing/{pathSecret}?client={querySecret}");
        request.Headers.Add("X-Correlation-ID", correlationId);

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var log = SingleCompletion(logs);
        Assert.Equal("_OTHER", Property<string>(log, "Method"));
        Assert.Equal("unmatched", Property<string>(log, "RouteTemplate"));
        Assert.Equal(StatusCodes.Status404NotFound, Property<int>(log, "StatusCode"));
        Assert.DoesNotContain(pathSecret, Flatten(log), StringComparison.Ordinal);
        Assert.DoesNotContain(querySecret, Flatten(log), StringComparison.Ordinal);

        var metric = await metrics.WaitAsync();
        Assert.Equal("_OTHER", Tag<string>(metric, "http.request.method"));
        Assert.Equal(StatusCodes.Status404NotFound, Tag<int>(metric, "http.response.status_code"));
        var flattenedTags = Flatten(metric);
        Assert.DoesNotContain(pathSecret, flattenedTags, StringComparison.Ordinal);
        Assert.DoesNotContain(querySecret, flattenedTags, StringComparison.Ordinal);
        Assert.DoesNotContain(correlationId, flattenedTags, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EscapingExceptionIsRecordedOnceAndRethrown()
    {
        using var logs = new CaptureLogProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var middleware = new RequestCompletionTelemetryMiddleware(
            _ => throw new InvalidOperationException("test-only failure"),
            loggerFactory.CreateLogger<RequestCompletionTelemetryMiddleware>());
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "ab000000-0000-0000-0000-000000000003",
        };
        context.Request.Method = HttpMethods.Get;

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        var log = SingleCompletion(logs);
        Assert.Equal(StatusCodes.Status500InternalServerError, Property<int>(log, "StatusCode"));
        Assert.DoesNotContain("test-only failure", log.Message, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> CreateFactory(CaptureLogProvider logs) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("ConnectionStrings:CommercialDatabase", UnavailableConnection);
            builder.UseDeterministicInventoryProtection();
            builder.UseSetting("Logging:LogLevel:Default", "Information");
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
        });

    private static CapturedLog SingleCompletion(CaptureLogProvider logs) =>
        Assert.Single(logs.Records, item => item.EventId.Id == CompletionEventId);

    private static T Property<T>(CapturedLog log, string name) =>
        (T)Convert.ChangeType(log.Properties[name]!, typeof(T), CultureInfo.InvariantCulture);

    private static T Tag<T>(MetricSnapshot metric, string name) =>
        (T)Convert.ChangeType(metric.Tags[name]!, typeof(T), CultureInfo.InvariantCulture);

    private static string Flatten(CapturedLog log) => string.Join(
        "|",
        [log.Message, .. log.Properties.Select(item => $"{item.Key}={item.Value}")]);

    private static string Flatten(MetricSnapshot metric) => string.Join(
        "|",
        metric.Tags.Select(item => $"{item.Key}={item.Value}"));

    private static void AssertValidTraceId(string traceId)
    {
        Assert.Equal(32, traceId.Length);
        Assert.NotEqual(default, ActivityTraceId.CreateFromString(traceId.AsSpan()));
    }

    private static void AssertJsonConsoleConfiguration(IConfiguration configuration)
    {
        Assert.Equal("json", configuration["Logging:Console:FormatterName"]);
        Assert.False(configuration.GetValue<bool>(
            "Logging:Console:FormatterOptions:IncludeScopes"));
        Assert.True(configuration.GetValue<bool>(
            "Logging:Console:FormatterOptions:UseUtcTimestamp"));
        Assert.False(configuration.GetValue<bool>(
            "Logging:Console:FormatterOptions:JsonWriterOptions:Indented"));
        Assert.Equal(
            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
            configuration["Logging:Console:FormatterOptions:TimestampFormat"]);
    }
}

internal static class ApiObservabilityTestGroup
{
    internal const string Name = "API observability";
}

[CollectionDefinition(ApiObservabilityTestGroup.Name, DisableParallelization = true)]
public sealed class ApiObservabilityTestGroupDefinition;
