# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore dependencies first for better layer caching
COPY BlogGraphQlApp.csproj ./
RUN dotnet restore BlogGraphQlApp.csproj

COPY . ./
RUN dotnet publish BlogGraphQlApp.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# ffmpeg for video processing (Xabe.FFmpeg).
# Note: Accord.Video.FFMPEG + System.Drawing frame extraction are Windows-only and
# will not run in this Linux container; the Xabe.FFmpeg-based pipeline is the one
# that works here.
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "BlogGraphQlApp.dll"]
