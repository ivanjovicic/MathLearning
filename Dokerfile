# -------- BUILD STAGE --------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Kopiraj sve fajlove u kontejner
COPY . .

# Restore samo admin projekt
RUN dotnet restore ./src/MathLearning.Admin/MathLearning.Admin.csproj

# Publish
RUN dotnet publish ./src/MathLearning.Admin/MathLearning.Admin.csproj -c Release -o /app/publish


# -------- RUNTIME STAGE --------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Kopiraj publish-ovane fajlove
COPY --from=build /app/publish .

# Render koristi PORT varijablu
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

ENTRYPOINT ["dotnet", "MathLearning.Admin.dll"]
