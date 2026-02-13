# Docker Memory Profiling (MathLearning)

This repo ships two Docker targets:

- `runtime` (default): small production image
- `diagnostics`: larger image that includes `dotnet-counters`, `dotnet-dump`, `dotnet-trace`

## Build

```bash
# Production-like
docker build -t mathlearning-api:runtime .

# Diagnostics
docker build --target diagnostics -t mathlearning-api:diag .
```

## Run (example)

```bash
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__Default="Host=...;Port=...;Username=...;Password=...;Database=...;" \
  -e JwtSettings__SecretKey="..." \
  mathlearning-api:diag
```

## Collect basic memory counters

Inside the diagnostics container:

```bash
dotnet-counters ps
dotnet-counters monitor --process-id <PID> System.Runtime
```

## Take a dump (for offline analysis)

```bash
dotnet-dump collect --process-id <PID> --output /tmp/mathlearning.dmp
```

Then copy the dump out:

```bash
docker cp <container-id>:/tmp/mathlearning.dmp .
```

## Notes On "Ultra Low-Memory" Mode

The default `Dockerfile` sets conservative memory-focused env vars:

- `DOTNET_GCConserveMemory=1`
- `DOTNET_GCHeapHardLimitPercent=70`
- `DOTNET_GCServer=0`

Override them per-environment if throughput becomes an issue.
