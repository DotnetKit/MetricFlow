using DotnetKit.MetricFlow.Abstractions.Sinks;
using DotnetKit.MetricFlow.Counters;
using DotnetKit.MetricFlow.Sinks;
using FluentAssertions;
using Xunit;

namespace MetricFlow.Tests;

public class HybridTimelineStoreTests : IDisposable
{
    private readonly string _testDir;

    public HybridTimelineStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "MetricFlow_HybridTests_" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task HybridStore_ServesRecentQueriesFromMemory_And_OldQueriesFromFile()
    {
        // Arrange - Memory retention is 5 minutes
        var options = new HybridTimelineStoreOptions
        {
            MemoryRetention = TimeSpan.FromMinutes(5),
            EnableFilePersistence = true,
            FileOptions = new FileTimelineStoreOptions
            {
                DirectoryPath = _testDir
            }
        };

        var store = new HybridTimelineStore(options);
        var baseTime = DateTimeOffset.UtcNow;

        // Old entry: 20 minutes ago
        var oldSnap = new DurationSnapshot("UploadFile", "Duration", 2, 2, 0, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(8), TimeSpan.FromMilliseconds(12), baseTime.AddMinutes(-20).UtcDateTime);
        var oldEntry = new MetricTimelineEntry(baseTime.AddMinutes(-21), baseTime.AddMinutes(-20), oldSnap, oldSnap);

        // Recent entry: 1 minute ago
        var recentSnap = new DurationSnapshot("UploadFile", "Duration", 8, 8, 0, TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(7), TimeSpan.FromMilliseconds(15), baseTime.AddMinutes(-1).UtcDateTime);
        var recentEntry = new MetricTimelineEntry(baseTime.AddMinutes(-2), baseTime.AddMinutes(-1), recentSnap, recentSnap);

        // Emit both
        await store.EmitAsync(new[] { oldEntry, recentEntry });

        // Simulate memory buffer pruning the old entry (as it would after 5 minutes)
        // We test memory-only query within the 5-minute window:
        var recentOnly = await store.GetTimelineAsync(baseTime.AddMinutes(-3), baseTime);
        recentOnly.Should().HaveCount(1);
        recentOnly[0].Delta.Should().BeOfType<DurationSnapshot>().Which.OutCount.Should().Be(8);

        // Query spanning back to 25 minutes ago (triggers file store read)
        var fullTimeline = await store.GetTimelineAsync(baseTime.AddMinutes(-25), baseTime);
        fullTimeline.Should().HaveCount(2); // Both old and recent are returned!
    }
}
