# syntax=docker/dockerfile:1

# Smaller runtime by default; override with --build-arg if you hit compatibility issues.
ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:8.0-jammy
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled

# --- Build stage ---
FROM ${SDK_IMAGE} AS build
WORKDIR /src

# Copy only project files first (faster caching)
COPY Directory.Build.props ./
COPY src/MathLearning.Api/*.csproj src/MathLearning.Api/
COPY src/MathLearning.Application/*.csproj src/MathLearning.Application/
COPY src/MathLearning.Domain/*.csproj src/MathLearning.Domain/
COPY src/MathLearning.Infrastructure/*.csproj src/MathLearning.Infrastructure/

RUN dotnet restore src/MathLearning.Api/MathLearning.Api.csproj

# Copy all source code
COPY src/ src/

# Publish API project only
# Notes:
# - UseAppHost=false reduces output size (no native apphost).
# - DebugSymbols/DebugType disabled for smaller artifacts.
RUN dotnet publish src/MathLearning.Api/MathLearning.Api.csproj -c Release -o /app/publish \
    -p:UseAppHost=false -p:DebugType=None -p:DebugSymbols=false

# --- Diagnostics target (bigger, for profiling) ---
# Build with: docker build --target diagnostics -t mathlearning-api:diag .
FROM ${SDK_IMAGE} AS diagnostics
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_GCConserveMemory=1 \
    DOTNET_GCHeapHardLimitPercent=70 \
    DOTNET_GCServer=0

# .NET diagnostic tools (for ad-hoc profiling inside the container via shell/ssh)
RUN dotnet tool install -g dotnet-counters \
 && dotnet tool install -g dotnet-dump \
 && dotnet tool install -g dotnet-trace
ENV PATH="${PATH}:/root/.dotnet/tools"

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "MathLearning.Api.dll"]

# --- Runtime stage (small, default) ---
FROM ${RUNTIME_IMAGE} AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    # Ultra low-memory defaults (override in fly.toml / env if needed)
    DOTNET_GCConserveMemory=1 \
    DOTNET_GCHeapHardLimitPercent=70 \
    DOTNET_GCServer=0 \
    # Chiseled images often run best with invariant globalization unless ICU is present.
    # If you need full cultures/sorting, override to 0 and use a runtime image with ICU.
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "MathLearning.Api.dll"]
