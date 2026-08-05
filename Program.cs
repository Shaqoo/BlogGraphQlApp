using System.Security.Claims;
using System.Threading.RateLimiting;
using BlogGraphQlApp.BackgroundServices;
using BlogGraphQlApp.Config;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.Data;
using BlogGraphQlApp.Endpoints;
using BlogGraphQlApp.Extensions.Migration;
using BlogGraphQlApp.External;
using BlogGraphQlApp.GraphQL.DataLoaders;
using BlogGraphQlApp.GraphQL.Errors;
using BlogGraphQlApp.GraphQL.Mutations;
using BlogGraphQlApp.GraphQL.Queries;
using BlogGraphQlApp.GraphQL.Subscriptions;
using BlogGraphQlApp.GraphQL.Types;
using BlogGraphQlApp.Hubs;
using BlogGraphQlApp.Infrastructure;
using BlogGraphQlApp.Infrastructure.Services;
using BlogGraphQlApp.Repositories.Implementations;
using BlogGraphQlApp.Repositories.Interfaces;
using BlogGraphQlApp.Services.History;
using BlogGraphQlApp.Services.Daily;
using BlogGraphQlApp.Services.Groups;
using BlogGraphQlApp.Services.Implementations;
using BlogGraphQlApp.Services.Interfaces;
using BlogGraphQlApp.Services.Push;
using BlogGraphQlApp.Services.Video;
using BlogGraphQlApp.Settings;
using BlogGraphQlApp.Storage;
using Microsoft.Extensions.FileProviders;
using FluentValidation;
using FluentValidation.AspNetCore;
using HotChocolate.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using OpenAI;


// Load environment variables from a .env file if one exists (local development or
// container mounts). This must run before the host is built so the values are picked
// up by the configuration providers, where they override appsettings.json.
try
{
    DotNetEnv.Env.Load();
}
catch (Exception)
{
    // A missing or invalid .env file is not fatal; the app can still run from appsettings.
    Console.WriteLine("A missing or invalid .env file is not fatal; the app can still run from appsettings.");
}

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection(EmailSettings.SectionName));

// Daily.co video calls and web push notifications are configured via the
// Daily and WebPush sections. The Daily API key is a secret: only set it
// through the .env file or environment variables, never commit it.
builder.Services.Configure<DailySettings>(
    builder.Configuration.GetSection(DailySettings.SectionName));

builder.Services.Configure<VapidSettings>(
    builder.Configuration.GetSection(VapidSettings.SectionName));

builder.Services.Configure<SpotifySettings>(
    builder.Configuration.GetSection(SpotifySettings.SectionName));

builder.Services.Configure<GeminiSettings>(
    builder.Configuration.GetSection(GeminiSettings.SectionName));

builder.Services.AddSignalR();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many requests. Please slow down." }, token);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        if (context.WebSockets.IsWebSocketRequest)
            return RateLimitPartition.GetNoLimiter("websocket");

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAuthenticated = !string.IsNullOrEmpty(userId);
        var key = isAuthenticated ? $"user:{userId}" : $"ip:{GetClientIp(context)}";

        var limit = isAuthenticated ? 300
            : context.Request.Path.StartsWithSegments("/gql") ? 10
            : 100;

        return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});

static string GetClientIp(HttpContext context)
{
    var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(forwarded))
        return forwarded.Split(',')[0].Trim();
    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

builder.Services.AddPooledDbContextFactory<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
    mySqlOptions => mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,                // retry up to 5 times
            maxRetryDelay: TimeSpan.FromSeconds(10), // wait up to 10s between retries
            errorNumbersToAdd: null           // additional MySQL error codes (optional)
        )))
    .AddScoped<IUnitOfWork, UnitOfWork>()
    .AddScoped<IPostRepository,PostRepository>()
    .AddScoped<IUserService, UserService>()
    .AddScoped<IPostService, PostService>()
    .AddScoped<IReelService, ReelService>()
    .AddScoped<IReplyService, ReplyService>()
    .AddScoped<IReactionService, ReactionService>()
    .AddScoped<INotificationService, NotificationService>()
    .AddScoped<IUserInteractionService, UserInteractionService>()
    .AddScoped<IRecommendationService, RecommendationService>()
    .AddScoped<IUserFollowService, UserFollowService>()
    .AddScoped<IMessagingService, MessagingService>()
    .AddScoped<IAuthService, AuthService>()
    .AddSingleton<IAvatarGeneratorService,AvatarGeneratorService>()
    .AddScoped<IEmailService, EmailService>()
    .AddSingleton<SpotifyService>()
    .AddSingleton<DeezerService>()
    .AddSingleton<JamendoService>()
    .AddSingleton<PresenceTracker>()
    .AddScoped<ICacheService, InMemoryCacheService>()
    .AddScoped<IAiService,AiService>()
    .AddScoped<IUserRecommendationService,UserRecommendationService>()
    .AddSingleton<EmbeddingService>()
    .AddSingleton<PineconeService>()
    .AddSingleton<ContentVectorService>()
    .AddHostedService<VectorizationBackgroundService>()
    .AddHostedService<AIUsageResetService>();

// Real-time video calls (Daily.co), web push notifications and group chat.
// DailyCallService talks to the Daily REST API as a typed HttpClient; all
// other call/group services are scoped and share the unit of work.
builder.Services.AddHttpClient<IDailyCallService, DailyCallService>();
builder.Services.AddScoped<IWebPushService, WebPushService>();
builder.Services.AddScoped<IVideoCallService, VideoCallService>();
builder.Services.AddScoped<IGroupService, GroupService>();
builder.Services.AddScoped<IGroupMessageService, GroupMessageService>();
builder.Services.AddScoped<GroupPermissionService>();
builder.Services.AddScoped<IGroupCallService, GroupCallService>();
builder.Services.AddScoped<ICallHistoryService, CallHistoryService>();
builder.Services.AddScoped<DailyWebhookService>();
builder.Services.AddHostedService<DailyRoomCleanupService>();
builder.Services.AddHostedService<RefreshTokenCleanupService>();

// File storage is selected based on the runtime environment.
// - Development: files are stored under wwwroot/uploads and served locally.
// - Production:  files are uploaded to UploadThing using the UPLOADTHING_SECRET
//   environment variable (configure it on the hosting platform, never commit it).
builder.Services.Configure<StorageValidationOptions>(
    builder.Configuration.GetSection(StorageValidationOptions.SectionName));

builder.Services.AddHttpClient(UploadThingStorage.HttpClientName, client =>
{
    client.BaseAddress = new Uri("https://api.uploadthing.com");
    client.Timeout = TimeSpan.FromMinutes(10);
});

if (builder.Environment.IsDevelopment())
    builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
else
    builder.Services.AddSingleton<IFileStorage, UploadThingStorage>();

// The wwwroot folder is git-ignored, so ensure it exists up front. This guarantees
// Development uploads (LocalFileStorage) and static file serving work on a fresh clone.
var webRootPath = System.IO.Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(webRootPath);

builder.Services.AddSingleton(sp =>
{
    var apiKey = builder.Configuration["OpenAI:ApiKey"];
    if (string.IsNullOrEmpty(apiKey))
        throw new InvalidOperationException("OpenAI API key is missing in configuration.");

    return new OpenAIClient(apiKey);
});

builder.Services.AddHttpClient();

builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpClient<GeminiClient>();

builder.Services.AddMemoryCache();

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 200 * 1024 * 1024; 
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 200 * 1024 * 1024; 
});


builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Key"]!))
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Look for token in query string for SignalR
                var accessToken = context.Request.Query["access_token"];

                // If request is for the presence hub, extract it
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs/presence"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAutoMapper(typeof(Program));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "https://blog-frontend-ice5.vercel.app", "https://blog-frontend-ice5-4m8qn02qg-shakirullah-s-projects.vercel.app/")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
            //.WithExposedHeaders("Apollo-Require-Preflight"); ; 
    });
});

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services
    .AddGraphQLServer()
    .AddErrorFilter<GraphQLErrorFilter>()
    .ModifyCostOptions(opt =>
    {
        opt.MaxFieldCost = 2000;
        opt.EnforceCostLimits = true;
        opt.MaxTypeCost = 2000;
    })
    .AddAuthorization()
    .AddQueryType(d => d.Name("Query"))
        .AddTypeExtension<UserQueries>()
        .AddTypeExtension<PostQueries>()
        .AddTypeExtension<ReelQueries>()
        .AddTypeExtension<ReplyQueries>()
        .AddTypeExtension<RecommendationQuery>()
        .AddTypeExtension<NotificationQueries>()
        .AddTypeExtension<MessagingQueries>()
        .AddTypeExtension<MusicQueries>()
        .AddTypeExtension<AiQueries>()
        .AddTypeExtension<ReactionQueries>()
        .AddTypeExtension<VideoCallQueries>()
        .AddTypeExtension<GroupQueries>()
        .AddTypeExtension<GroupMessageQueries>()
        .AddTypeExtension<GroupCallQueries>()
    .AddMutationType(d => d.Name("Mutation"))
        .AddTypeExtension<AuthMutation>()
        .AddTypeExtension<UserMutation>()
        .AddTypeExtension<PostMutation>()
        .AddTypeExtension<ReelMutation>()
        .AddTypeExtension<ReplyMutation>()
        .AddTypeExtension<ReactionMutation>()
        .AddTypeExtension<UserInteractionMutation>()
        .AddTypeExtension<NotificationMutation>()
        .AddTypeExtension<UserFollowMutation>()
        .AddTypeExtension<MessagingMutation>()
        .AddTypeExtension<VideoCallMutations>()
        .AddTypeExtension<WebPushMutations>()
        .AddTypeExtension<GroupMutations>()
        .AddTypeExtension<GroupMessageMutations>()
        .AddTypeExtension<GroupCallMutations>()
    .AddSocketSessionInterceptor<SocketSessionInterceptor>()
    .AddSubscriptionType(d => d.Name("Subscription"))
        .AddTypeExtension<MessagingSubscription>()
        .AddTypeExtension<ReactionSubscription>()
        .AddTypeExtension<CallSubscription>()
        .AddTypeExtension<NotificationSubscription>()
    .AddType<UserType>()
    .AddType<ReelType>()
    .AddType<PostType>()
    .AddType<ReactionType>()
    .AddType<ReplyType>()
    .AddType<NotificationTypeGql>()
    .AddType<UploadType>()
    .AddType<VideoCallTypeGql>()
    .AddType<GroupTypeGql>()
    .AddType<GroupMemberTypeGql>()
    .AddType<GroupMessageTypeGql>()
    .AddType<GroupCallTypeGql>()
    .AddType<GroupMentionTypeGql>()
    .AddType<GroupCallParticipantTypeGql>()
    .AddType<GroupJoinRequestTypeGql>()
    .AddProjections()
    .AddFiltering()
    .AddSorting()
    .RegisterDbContextFactory<AppDbContext>()
    .AddDataLoader<NotificationByUserIdDataLoader>()
    .AddDataLoader<ReactionsByPostIdDataLoader>()
    .AddDataLoader<RepliesByPostDataLoader>()
    .AddDataLoader<NestedRepliesByReplyIdDataLoader>()
    .AddDataLoader<UserByIdDataLoader>()
    .AddDataLoader<RepliesByReelDataLoader>()
    .AddDataLoader<FollowersByUserIdDataLoader>()
    .AddDataLoader<FollowingByUserIdDataLoader>()
    .AddDataLoader<ReactionsByReelIdDataLoader>()
    .AddDataLoader<ReactionsByReplyIdDataLoader>()
    .AddDataLoader<ReactionsByGroupMessageIdDataLoader>()
    .AddDataLoader<MentionsByGroupMessageIdDataLoader>()
    .AddDataLoader<GroupMessageByIdDataLoader>()
    .AddDataLoader<ReadsByGroupMessageIdDataLoader>()
    // .AddMutationConventions()
    .AddInMemorySubscriptions();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSubscriptionDiagnostics();
}

var app = builder.Build();

app.UseMiddleware<BlogGraphQlApp.Middleware.SecurityHeadersMiddleware>();

app.UseCors("AllowFrontend");

app.UseWebSockets();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(webRootPath),
    OnPrepareResponse = ctx =>
    {
        var origin = ctx.Context.Request.Headers["Origin"].FirstOrDefault();
        if (!string.IsNullOrEmpty(origin))
        {
            ctx.Context.Response.Headers["Access-Control-Allow-Origin"] = origin;
            ctx.Context.Response.Headers["Access-Control-Allow-Methods"] = "GET, HEAD, OPTIONS";
            ctx.Context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";
            ctx.Context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
        }
    }
});

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseHttpsRedirection();

app.MapGraphQL("/gql").WithOptions(new GraphQLServerOptions
{
    Tool = { Enable = true }
}); ;
app.Services.ApplyMigrationAsync().GetAwaiter().GetResult();

app.MapHub<PresenceHub>("/hubs/presence");
app.MapDailyWebhook();
app.MapCallHistoryEndpoints();
app.MapWebPushEndpoints();
app.Run();
