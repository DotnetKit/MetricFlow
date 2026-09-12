using DotnetKit.MetricFlow;
using DotnetKit.MetricFlow.Extensions;

namespace SimpleMetricCountersExample;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var tracker = new MetricTracker("ExecutionTimeMetricsTopic", new()
            {
                ["tenant_id"] = "TenantId1",
                ["session_id"] = Guid.NewGuid().ToString()
            })
            .AddExceptionCounter()
            .AddMemoryCounter();

        tracker.In("GlobalOperation");

        for (var i = 0; i < OperationCount; i++)
        {
            await ExecuteOperation1Async(tracker, i);
            await ExecuteOperation2Async(tracker, i);
        }

        tracker.Out("GlobalOperation");

        Console.WriteLine(tracker.ToString());
    }

    private const int OperationCount = 10; 
  
    internal static async Task ExecuteOperation1Async(MetricTracker tracker, int i)
    {
        // Operation1: tracks duration and memory allocation (metric name resolved dynamically via [CallerMemberName])
        using var op1 = tracker.Track(tags: new() { ["operation_id"] = $"{i}" });

        await Task.Delay(2);

        // Allocate memory to exercise MemoryCounter (16 KB - 160 KB)
        _ = AllocateMemory((i + 1) * 16, (byte)i);
    }

    internal static async Task ExecuteOperation2Async(MetricTracker tracker, int i)
    {
        // Operation2: tracks duration, memory allocation, and throws modulo-based exceptions (resolved via [CallerMemberName])
        try
        {
            using var op2 = tracker.Track(tags: new() { ["operation_id"] = $"{OperationCount - i}" });
            try
            {
                await Task.Delay(4);

                // Allocate memory to exercise MemoryCounter (32 KB - 320 KB)
                _ = AllocateMemory((OperationCount - i) * 32, (byte)i);

                // Throw exception based on modulo to exercise ExceptionCounter
                ThrowModuloException(i);
            }
            catch (Exception ex)
            {
                op2.SetException(ex);
                throw;
            }
        }
        catch
        {
            // Handled to allow benchmark loop to continue
        }
    }

    internal static byte[] AllocateMemory(int sizeInKilobytes, byte touchByte = 1)
    {
        var buffer = new byte[sizeInKilobytes * 1024];
        if (buffer.Length > 0)
        {
            buffer[0] = touchByte;
        }

        return buffer;
    }

    internal static void ThrowModuloException(int i)
    {
        if (i % 4 == 0)
        {
            throw (i % 2 == 0)
                ? new TimeoutException($"Operation2 timeout at iteration {i}")
                : new TaskCanceledException($"Operation2 canceled at iteration {i}");
        }
    }
}
