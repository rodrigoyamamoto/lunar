using System.Diagnostics;

namespace Lunar.Tests.Telemetry;

/// <summary>
/// A test helper that subscribes to ActivitySource events and captures
/// all created Activities for assertion.
/// </summary>
public sealed class TestActivityListener : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _activities = new();
    private readonly object _lock = new();

    public IReadOnlyList<Activity> Activities
    {
        get
        {
            lock (_lock)
            {
                return _activities.ToList();
            }
        }
    }

    public TestActivityListener(params string[] sourceNames)
    {
        var sourceSet = new HashSet<string>(sourceNames);

        _listener = new ActivityListener
        {
            ShouldListenTo = source =>
                sourceSet.Contains(source.Name),

            SampleUsingParentId = (ref ActivityCreationOptions<string> options) =>
                ActivitySamplingResult.AllDataAndRecorded,

            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                ActivitySamplingResult.AllDataAndRecorded,

            ActivityStopped = activity =>
            {
                lock (_lock)
                {
                    _activities.Add(activity);
                }
            }
        };

        ActivitySource.AddActivityListener(_listener);
    }


    public void Dispose()
    {
        _listener.Dispose();
    }
}
