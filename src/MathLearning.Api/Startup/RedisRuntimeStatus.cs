namespace MathLearning.Api.Startup;

public sealed record RedisHealthSnapshot(
    string Mode,
    bool Required,
    bool Configured,
    bool Connected,
    string? FailureReason);

public sealed class RedisRuntimeStatus
{
    private readonly object _sync = new();
    private string _mode = "Uninitialized";
    private bool _configured;
    private bool _connected;
    private string? _failureReason;

    public RedisRuntimeStatus(bool required)
    {
        Required = required;
    }

    public bool Required { get; }

    public void MarkRedis(bool connected)
    {
        lock (_sync)
        {
            _configured = true;
            _connected = connected;
            _mode = connected ? "Redis" : "RedisReconnecting";
            _failureReason = connected ? null : "ConnectionNotEstablished";
        }
    }

    public void MarkRedisConnected()
    {
        lock (_sync)
        {
            _configured = true;
            _connected = true;
            _mode = "Redis";
            _failureReason = null;
        }
    }

    public void MarkRedisDisconnected(string reason)
    {
        lock (_sync)
        {
            _configured = true;
            _connected = false;
            _mode = "RedisReconnecting";
            _failureReason = reason;
        }
    }

    public void MarkDbFallback(string reason)
    {
        lock (_sync)
        {
            _configured = false;
            _connected = false;
            _mode = "DbFallback";
            _failureReason = reason;
        }
    }

    public RedisHealthSnapshot Snapshot()
    {
        lock (_sync)
        {
            return new RedisHealthSnapshot(_mode, Required, _configured, _connected, _failureReason);
        }
    }
}
