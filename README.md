# Reelio Blog API

A social blogging platform backend built with ASP.NET Core and GraphQL. Users create text, image and video posts; follow each other; react and reply; exchange direct messages in real time; and get AI-assisted features like caption suggestions, content moderation and personalized recommendations.

The GraphQL endpoint exposes the full API. In development it runs with local file storage and Banana Cake Pop; in production it uses UploadThing for file storage.

## Features

- **Posts & reels** — text, image and video posts plus short-video reels; view/share counters, hashtag extraction and `@mentions` (with automatic notifications).
- **Reactions & replies** — react to posts, reels, replies and messages; nested reply threads.
- **Social graph** — follow/unfollow, followers/following lists, presence (online/offline, last seen) via SignalR.
- **Messaging** — direct messages with file/image attachments, reply-to-message, typing indicators, recording indicators, read receipts and Agora video-call token generation.
- **Notifications** — in-app notifications with read/unread state, pushed in real time over GraphQL subscriptions.
- **AI** — caption generation, an assistant chat, content moderation, and auto-generated avatars from initials.
- **Recommendations** — ML-based post/reel/user recommendations and user interactions tracking.
- **Vector search** — content is embedded (text and video frames) and upserted to Pinecone; background services handle vectorization and AI usage resets.
- **Music search** — track lookup across Spotify, Deezer and Jamendo.
- **Auth** — JWT login/register, email verification codes, password reset.

## Tech stack

| Layer | Tech |
| --- | --- |
| Runtime | .NET 8 / ASP.NET Core |
| GraphQL | HotChocolate 16 (Queries, Mutations, Subscriptions, DataLoaders, pagination/filtering/sorting) |
| Database | MySQL via Pomelo Entity Framework Core (pooled DbContext factory, automatic migrations on startup) |
| Auth | JWT bearer tokens |
| Real time | GraphQL in-memory subscriptions + SignalR presence hub |
| AI | OpenAI, Google Gemini (Google.GenAI), Microsoft.ML / ML.NET Recommender |
| Vector search | Pinecone (Pinecone.Client / Pinecone.NET) |
| Storage | `IFileStorage` — local `wwwroot` in Development, UploadThing CDN in Production |
| Media | Xabe.FFmpeg, Accord.Video.FFMPEG, SkiaSharp, SixLabors.ImageSharp |
| Misc | FluentValidation, AutoMapper, Polly (retries), Scrypt (password hashing), DotNetEnv |

## Project structure

```
Configurations/        EF Core entity configurations
Context/               EF Core DbContext
Entities/              Domain entities
Enums/                 Shared enums
Dtos/                  Request/response DTOs (incl. ML model types under Dtos/ML)
External/              Third-party integrations (Gemini, OpenAI, Pinecone, Spotify, Deezer, Jamendo, video embedding)
GraphQL/
  Queries/             Query type extensions
  Mutations/           Mutation type extensions
  Subscriptions/       Subscription type extensions
  Types/               GraphQL object types
  Resolvers/           Field resolvers
  DataLoaders/         HotChocolate DataLoaders
  Events/              Subscription event payloads
BackgroundServices/    Vectorization + AI usage reset hosted services
Repositories/          Repository + Unit of Work pattern
Services/              Application services (auth, posts, reels, messaging, AI, recommendations, ...)
Storage/               IFileStorage + Local/UploadThing implementations and validation
Settings/              Strongly-typed settings (email, Gemini, Spotify)
Extensions/            Startup extensions (migration, AutoMapper profiles)
Hubs/                  SignalR presence hub
Validators/            FluentValidation validators
Migrations/            EF Core migrations
```

## Getting started

### Prerequisites

- .NET 8 SDK
- MySQL 8 (local or Docker)
- API keys for the services you want to use (OpenAI, Gemini, Pinecone, Spotify, UploadThing)

### Setup

```bash
cp .env.example .env     # fill in real values
dotnet run
```

On startup the app:

1. Loads `.env` via DotNetEnv (values override `appsettings.json`).
2. Runs EF Core migrations against MySQL.
3. Serves the GraphQL endpoint at `http://localhost:5250/gql` (`https://localhost:7008/gql`) with Banana Cake Pop enabled.

### Environment variables

All settings follow the `Section__Key` convention (e.g. `Jwt__Key` → `Jwt:Key`). See `.env.example` for the full template.

| Variable | Description |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Development` = local storage, `Production` = UploadThing |
| `ConnectionStrings__DefaultConnection` | MySQL connection string |
| `Jwt__Key` / `Jwt__Issuer` / `Jwt__Audience` | JWT signing key and token claims |
| `EmailSettings__*` | SMTP host, port, sender and password |
| `SpotifySettings__*` | Spotify API client id/secret |
| `GeminiSettings__ApiKey` | Google Gemini API key (AI features) |
| `OpenAI__ApiKey` | OpenAI API key (startup requires it) |
| `UploadThing__Secret` / `UploadThing__AppId` | UploadThing credentials (Production only) |
| `Pinecone__*` | Pinecone API key, environment, index and project |
| `Agora__*` | Optional Agora app id/certificate |
| `Storage__Validation__*` | Upload size limits (bytes) per file category |

## File storage

All file uploads/downloads go through `IFileStorage` (`Storage/IFileStorage.cs`). Providers are selected automatically by environment:

| Environment | Provider | Description |
| --- | --- | --- |
| Development | `LocalFileStorage` | Stores files under `wwwroot/uploads/...` and returns local URLs served by static files. No configuration required. |
| Production | `UploadThingStorage` | Uploads files to UploadThing and returns public CDN URLs. |

### Configuration

The `UploadThingStorage` provider authenticates every API request with the app's **API key (Secret)** in the `x-uploadthing-api-key` header. It reads the key from `UPLOADTHING_SECRET` (environment variable) or `UploadThing:Secret` in `appsettings.json`.

`UPLOADTHING_APP_ID` / `UploadThing:AppId` is the public app id; it is used only to build a fallback file URL (`https://{appId}.ufs.sh/f/{key}`) and is not a credential.

Large files are streamed to UploadThing's presigned URL (never buffered in memory), and transient API failures are retried with exponential backoff via Polly. Uploads are validated (size and content type) before any provider is called — see `Storage/FileValidator.cs` and `Storage/StorageValidationOptions.cs`.

### Using the storage

Services depend on `IFileStorage` (never on the concrete providers):

- `UploadAsync(IFile, subfolder)` — copy an uploaded GraphQL `File` into storage.
- `UploadAsync(byte[], subfolder, fileName)` — write raw bytes.
- `DeleteAsync(fileUrl)` — remove a file by its stored URL.
- `DownloadAsync(fileUrl)` — read a file back as bytes (used by captioning/moderation/embedding services).

## GraphQL API

Endpoint: `/gql` (Banana Cake Pop IDE enabled). Authorization uses the `Authorization: Bearer <token>` header for `[Authorize]` fields.

### Queries

- **Users** — profile by id/username, search, followers/following, current user.
- **Posts** — feed, by user, by tag, by id; view/share counts.
- **Reels** — feed, by user, by id.
- **Replies** — top-level and nested reply threads.
- **Reactions** — by post/reply, current user's reaction state.
- **Notifications** — current user's notifications.
- **Messaging** — conversations, messages, unread state.
- **Recommendations** — personalized posts, reels and user suggestions.
- **Music** — track search on Spotify, Deezer and Jamendo.
- **AI** — caption generation and assistant chat.

### Mutations

- **Auth** — login, register, email verification, forgot/reset password.
- **Users** — update profile (bio, username, picture, cover, background), follow/unfollow.
- **Posts** — create/update/delete, track views and shares.
- **Reels** — create/update/delete.
- **Replies** — create/update/delete.
- **Reactions** — create/delete (posts, replies, messages).
- **Interactions** — log/update user interactions and favorites.
- **Notifications** — mark as read.
- **Messaging** — send messages, generate video-call tokens, mark as read, delete.

### Subscriptions

Real-time events over WebSockets:

- `onMessageSent`, `userTyping`, `userRecording`, `onMessageRead` (per conversation).
- `onPostReactionAdded`, `onReelReactionAdded`, `onMessageReactionAdded`.
- Notifications are also pushed to subscribed clients.

Example — react to a post and receive the event:

```graphql
mutation {
  createReaction(input: { reaction: "LIKE", postId: "..." }) {
    data { id emoji }
  }
}

subscription {
  onPostReactionAdded(postId: "...") {
    reaction
    userId
    fullName
  }
}
```

### Real-time presence

A SignalR hub at `/hubs/presence` tracks online users. Connect with the JWT in the `access_token` query string; the server broadcasts `UserOnline`, `UserOffline` and `LastSeen` events.

## Docker

The repo ships a `Dockerfile` and `docker-compose.yml` that run the API next to MySQL. The container runs with `ASPNETCORE_ENVIRONMENT=Production` (UploadThing storage).

```bash
cp .env.example .env          # fill in real values
docker compose up --build
```

- API: http://localhost:8080/gql (Banana Cake Pop GraphQL IDE enabled)
- MySQL data is persisted in the `db_data` volume; the API runs EF Core migrations on startup.

Note: the API needs outbound internet access (UploadThing, OpenAI, Gemini, Pinecone). Video frame extraction via `Accord.Video.FFMPEG`/`System.Drawing` is Windows-only and will not work in a Linux container; enable Docker Desktop's WSL 2 integration so `docker` works in this shell.

## License

MIT — see [LICENSE.txt](LICENSE.txt).
