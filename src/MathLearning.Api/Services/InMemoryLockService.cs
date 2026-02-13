using System.Collections.Concurrent;

namespace MathLearning.Api.Services;

public class InMemoryLockService
{
    private sealed class LockEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public long ExpiresAtTicks;
        public int HoldCount;
    }

    private readonly ConcurrentDictionary<string, LockEntry> _locks = new();

    public async Task<bool> TryAcquireAsync(string key, TimeSpan ttl)
    {
        var entry = _locks.GetOrAdd(key, _ => new LockEntry());
        var acquired = await entry.Semaphore.WaitAsync(0);
        if (!acquired) return false;

        Interlocked.Increment(ref entry.HoldCount);

        var expiresAtTicks = DateTime.UtcNow.Add(ttl).Ticks;
        Volatile.Write(ref entry.ExpiresAtTicks, expiresAtTicks);

        _ = Task.Delay(ttl).ContinueWith(_ => CleanupIfExpiredAndIdle(key, entry, expiresAtTicks));
        return true;
    }

    public Task<bool> ReleaseAsync(string key)
    {
        if (_locks.TryGetValue(key, out var entry))
        {
            var released = false;
            try
            {
                entry.Semaphore.Release();
                released = true;
            }
            catch
            {
                /* ignore */
            }

            if (released)
            {
                Interlocked.Decrement(ref entry.HoldCount);
                CleanupIfExpiredAndIdle(key, entry, Volatile.Read(ref entry.ExpiresAtTicks));
            }
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    private void CleanupIfExpiredAndIdle(string key, LockEntry entry, long expectedExpiresAtTicks)
    {
        // Don't clean up if this entry was extended/reused since we scheduled this cleanup.
        if (Volatile.Read(ref entry.ExpiresAtTicks) != expectedExpiresAtTicks)
            return;

        if (Volatile.Read(ref entry.HoldCount) > 0)
            return;

        if (DateTime.UtcNow.Ticks < expectedExpiresAtTicks)
            return;

        if (_locks.TryRemove(new KeyValuePair<string, LockEntry>(key, entry)))
        {
            entry.Semaphore.Dispose();
        }
    }
}
