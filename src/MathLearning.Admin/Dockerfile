ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:8.0-jammy
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled

FROM ${SDK_IMAGE} AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/MathLearning.Admin/*.csproj src/MathLearning.Admin/
COPY src/MathLearning.Application/*.csproj src/MathLearning.Application/
COPY src/MathLearning.Domain/*.csproj src/MathLearning.Domain/
COPY src/MathLearning.Infrastructure/*.csproj src/MathLearning.Infrastructure/
COPY src/MathLearning.Services/*.csproj src/MathLearning.Services/
COPY src/MathLearning.Core/*.csproj src/MathLearning.Core/

RUN dotnet restore src/MathLearning.Admin/MathLearning.Admin.csproj

COPY src/ src/

RUN dotnet publish src/MathLearning.Admin/MathLearning.Admin.csproj -c Release -o /app/publish \
    -p:UseAppHost=false -p:DebugType=None -p:DebugSymbols=false

FROM ${RUNTIME_IMAGE} AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:10000 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY --from=build /app/publish .

EXPOSE 10000
ENTRYPOINT ["dotnet", "MathLearning.Admin.dll"]