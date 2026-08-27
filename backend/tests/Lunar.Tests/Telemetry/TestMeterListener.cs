using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Lunar.Application;
using Lunar.Infrastructure;

namespace Lunar.Tests.Telemetry;

/// <summary>
/// A test helper that listens to Meter instruments and captures
/// all measurements for assertion.
/// </summary>
public sealed class TestMeterListener : IDisposable
{
    private readonly MeterListener _listener;
    private readonly ConcurrentBag<MeasurementRecord> _measurements = new();

    public IReadOnlyList<MeasurementRecord> AllMeasurements => _measurements.ToList();

    public TestMeterListener(params string[] meterNames)
    {
        var meterSet = new HashSet<string>(meterNames);

        _listener = new MeterListener();

        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (meterSet.Contains(instrument.Meter.Name))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _listener.SetMeasurementEventCallback<int>(OnMeasurement);
        _listener.SetMeasurementEventCallback<long>(OnMeasurement);
        _listener.SetMeasurementEventCallback<double>(OnMeasurement);
        _listener.SetMeasurementEventCallback<decimal>(OnMeasurement);
        _listener.SetMeasurementEventCallback<float>(OnMeasurement);
        _listener.SetMeasurementEventCallback<short>(OnMeasurement);
        _listener.SetMeasurementEventCallback<byte>(OnMeasurement);

        _listener.Start();

        // Explicitly enable measurement events for already-published static
        // instruments. MeterListener.InstrumentPublished is only called for
        // instruments created after Start(), but our telemetry instruments are
        // static singletons created at type initialization time.
        EnableStaticInstruments();
    }


    private void EnableStaticInstruments()
    {
        // Application-layer instruments
        _listener.EnableMeasurementEvents(ApplicationTelemetry.GenerationAttempts);
        _listener.EnableMeasurementEvents(ApplicationTelemetry.GenerationDuration);
        _listener.EnableMeasurementEvents(ApplicationTelemetry.CapabilityExecutionDuration);
        _listener.EnableMeasurementEvents(ApplicationTelemetry.ArtifactContentPersistenceDuration);

        // Infrastructure-layer instruments
        _listener.EnableMeasurementEvents(InfrastructureTelemetry.ProviderRequests);
        _listener.EnableMeasurementEvents(InfrastructureTelemetry.ProviderRequestDuration);
        _listener.EnableMeasurementEvents(InfrastructureTelemetry.ProviderOutputSize);
    }


    private void OnMeasurement<T>(
        Instrument instrument,
        T value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
    {
        var tagDict = new Dictionary<string, object?>();
        foreach (var tag in tags)
        {
            tagDict[tag.Key] = tag.Value;
        }

        _measurements.Add(new MeasurementRecord(
            instrument.Meter.Name,
            instrument.Name,
            instrument.GetType().Name,
            Convert.ToDouble(value),
            tagDict));
    }


    public List<MeasurementRecord> GetCounterValues(string instrumentName)
    {
        return _measurements.Where(m => m.InstrumentName == instrumentName).ToList();
    }


    public List<MeasurementRecord> GetHistogramValues(string instrumentName)
    {
        return _measurements.Where(m => m.InstrumentName == instrumentName).ToList();
    }


    public void Dispose()
    {
        _listener.Dispose();
    }
}

public sealed record MeasurementRecord(
    string MeterName,
    string InstrumentName,
    string InstrumentType,
    double Value,
    IReadOnlyDictionary<string, object?> Tags);
