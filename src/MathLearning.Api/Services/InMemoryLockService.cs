using System.Collections.Concurrent;

namespace MathLearning.Api.Services;

public class InMemoryLockService
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<bool> TryAcquireAsync(string key, TimeSpan ttl)
    {
        var sem = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        var acquired = await sem.WaitAsync(0);
        if (!acquired) return false;

        _ = Task.Delay(ttl).ContinueWith(_ => ReleaseIfIdle(key));
        return true;
    }

    public Task<bool> ReleaseAsync(string key)
    {
        if (_locks.TryGetValue(key, out var sem))
        {
            try { sem.Release(); } catch { /* ignore */ }
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    private void ReleaseIfIdle(string key)
    {
        if (_locks.TryGetValue(key, out var sem) && sem.CurrentCount == 0)
        {
            try { sem.Release(); } catch { }
        }
    }
}