using System.Collections.Concurrent;
using System.Net;

namespace Eventy.ConcurrencyTests.Engine;

/// <summary>
/// Executes an action N times concurrently with a deterministic
/// phase-aware barrier. Unlike the previous version (which relied on
/// Task.Delay to "guess" when workers were mid-flight), this version
/// provides an explicit <c>onMidFlight</c> callback that is invoked
/// AFTER all workers have passed the start barrier but BEFORE any of
/// them sends its booking request.
///
/// This guarantees the toggle happens in the exact race window:
///
///   Worker thread:  GetByIdAsync ──→ barrier ──→ [onMidFlight fires here] ──→ SendAsync
///   Toggle thread:                                      ↑ fires here
///
/// The callback runs on a separate thread and is awaited by all
/// workers before they proceed, so the toggle is fully committed
/// before any booking request leaves the wire.
/// </summary>
public class ConcurrentExecutor
{
    /// <summary>
    /// Executes <paramref name="workerCount"/> workers concurrently.
    /// All workers start at the same instant (async barrier). The
    /// <paramref name="onMidFlight"/> callback is invoked exactly once,
    /// between the barrier release and the first worker action — this is
    /// the deterministic hook for injecting a toggle command.
    /// </summary>
    public async Task<ConcurrentResult> ExecuteAsync(
        int workerCount,
        Func<int, Task<HttpResponseMessage>> action,
        Func<Task>? onMidFlight = null)
    {
        var goTcs = new TaskCompletionSource();
        var readyLock = new object();
        var readyCount = 0;
        var midFlightTriggered = false;
        var midFlightDone = new TaskCompletionSource();
        var results = new ConcurrentBag<ConcurrentResponse>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var tasks = Enumerable.Range(0, workerCount).Select(i => Task.Run(async () =>
        {
            // ── Phase 1: Ready barrier ──
            bool allReady;
            lock (readyLock)
            {
                readyCount++;
                allReady = readyCount == workerCount;
            }

            if (allReady)
                goTcs.TrySetResult();

            await goTcs.Task.WaitAsync(cts.Token);

            // ── Phase 2: Mid-flight hook ──
            // The first worker to reach this point triggers the
            // onMidFlight callback (if provided). All workers wait
            // until the callback completes before proceeding.
            lock (readyLock)
            {
                if (!midFlightTriggered)
                {
                    midFlightTriggered = true;
                    if (onMidFlight is not null)
                        _ = onMidFlight().ContinueWith(
                            t => midFlightDone.TrySetResult(),
                            TaskScheduler.Default);
                    else
                        midFlightDone.TrySetResult();
                }
            }

            await midFlightDone.Task.WaitAsync(cts.Token);

            // ── Phase 3: Execute the action ──
            var response = await action(i);
            results.Add(new ConcurrentResponse(i, response.StatusCode));
        }, cts.Token));

        await Task.WhenAll(tasks);

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

    /// <summary>
    /// Count of responses that returned a concurrency conflict (409)
    /// or internal server error (500) — these indicate the fencing
    /// token or optimistic concurrency check actually fired.
    /// </summary>
    public int ConcurrencyConflictCount => _results.Count(r =>
        r.StatusCode is HttpStatusCode.Conflict
        or HttpStatusCode.InternalServerError);

    public IReadOnlyCollection<ConcurrentResponse> AllResponses => _results;
    public IEnumerable<ConcurrentResponse> Successes => _results.Where(r => IsSuccessCode(r.StatusCode));
    public IEnumerable<ConcurrentResponse> Failures => _results.Where(r => !IsSuccessCode(r.StatusCode));
}
