# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only project files first (faster caching)
COPY src/MathLearning.Api/*.csproj src/MathLearning.Api/
COPY src/MathLearning.Application/*.csproj src/MathLearning.Application/
COPY src/MathLearning.Domain/*.csproj src/MathLearning.Domain/
COPY src/MathLearning.Infrastructure/*.csproj src/MathLearning.Infrastructure/

# DO NOT include Admin — it should not be built or restored
# COPY src/MathLearning.Admin/*.csproj src/MathLearning.Admin/  <-- leave this commented

# Restore dependencies for API project (restores others automatically)
RUN dotnet restore src/MathLearning.Api/MathLearning.Api.csproj

# Copy all source code
COPY src/ src/

# Publish API project only
RUN dotnet publish src/MathLearning.Api/MathLearning.Api.csproj -c Release -o /app/publish

# --- Runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "MathLearning.Api.dll"]
