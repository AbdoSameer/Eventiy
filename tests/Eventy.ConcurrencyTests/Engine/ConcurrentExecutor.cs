using System.Collections.Concurrent;
using System.Net;

namespace Eventy.ConcurrencyTests.Engine;

/// <summary>
/// Executes an action N times concurrently, using an async barrier to ensure
/// all workers start at nearly the same instant without blocking threads.
/// </summary>
public class ConcurrentExecutor
{
    public async Task<ConcurrentResult> ExecuteAsync(
        int workerCount,
        Func<int, Task<HttpResponseMessage>> action)
    {
        var goTcs = new TaskCompletionSource();
        var readyLock = new object();
        var readyCount = 0;
        var results = new ConcurrentBag<ConcurrentResponse>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var tasks = Enumerable.Range(0, workerCount).Select(i => Task.Run(async () =>
        {
            bool allReady;
            lock (readyLock)
            {
                readyCount++;
                allReady = readyCount == workerCount;
            }

            if (allReady)
                goTcs.TrySetResult();

            await goTcs.Task.WaitAsync(cts.Token);
            var response = await action(i);
            results.Add(new ConcurrentResponse(i, response.StatusCode));
        }, cts.Token));

        var taskArray = tasks.ToArray();
        await Task.WhenAll(taskArray);

        return new ConcurrentResult(results);
    }
}

public record ConcurrentResponse(int WorkerIndex, HttpStatusCode StatusCode);

public class ConcurrentResult
{
    private readonly IReadOnlyCollection<ConcurrentResponse> _results;

    public ConcurrentResult(IEnumerable<ConcurrentResponse> results)
    {
        _results = results.ToList().AsReadOnly();
    }

    public int TotalRequests => _results.Count;
    public int SuccessCount => _results.Count(r => IsSuccessCode(r.StatusCode));
    public int FailureCount => TotalRequests - SuccessCount;

    private static bool IsSuccessCode(HttpStatusCode code) =>
        code is HttpStatusCode.OK or HttpStatusCode.Created or HttpStatusCode.Accepted;

    public IReadOnlyCollection<ConcurrentResponse> AllResponses => _results;
    public IEnumerable<ConcurrentResponse> Successes => _results.Where(r => IsSuccessCode(r.StatusCode));
    public IEnumerable<ConcurrentResponse> Failures => _results.Where(r => !IsSuccessCode(r.StatusCode));
}
