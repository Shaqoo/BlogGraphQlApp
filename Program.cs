using BlogGraphQlApp.BackgroundServices;
using BlogGraphQlApp.Config;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.Data;
using BlogGraphQlApp.Extensions.Migration;
using BlogGraphQlApp.External;
using BlogGraphQlApp.GraphQL.DataLoaders;
using BlogGraphQlApp.GraphQL.Mutations;
using BlogGraphQlApp.GraphQL.Queries;
using BlogGraphQlApp.GraphQL.Subscriptions;
using BlogGraphQlApp.GraphQL.Types;
using BlogGraphQlApp.Hubs;
using BlogGraphQlApp.Infrastructure;
using BlogGraphQlApp.Infrastructure.Services;
using BlogGraphQlApp.Repositories.Implementations;
using BlogGraphQlApp.Repositories.Interfaces;
using BlogGraphQlApp.Services.Implementations;
using BlogGraphQlApp.Services.Interfaces;
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

builder.Services.Configure<AgoraSettings>(
    builder.Configuration.GetSection(AgoraSettings.SectionName));

builder.Services.Configure<SpotifySettings>(
    builder.Configuration.GetSection(SpotifySettings.SectionName));

builder.Services.Configure<GeminiSettings>(
    builder.Configuration.GetSection(GeminiSettings.SectionName));

builder.Services.AddSignalR();

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
    .AddScoped<IAgoraService, AgoraService>()
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

// File storage is selected based on the runtime environment.
// - Development: files are stored under wwwroot/uploads and served locally.
// - Production:  files are uploaded to UploadThing using the UPLOADTHING_TOKEN
//   environment variable (configure it on the hosting platform, never commit it).
builder.Services.AddSingleton<LocalFileStorage>();
builder.Services.AddSingleton<UploadThingStorage>();
builder.Services.AddSingleton<IFileStorage>(sp =>
{
    var environment = sp.GetRequiredService<IWebHostEnvironment>();
    return environment.IsDevelopment()
        ? sp.GetRequiredService<LocalFileStorage>()
        : sp.GetRequiredService<UploadThingStorage>();
});

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
            .WithOrigins("http://localhost:5173", "https://blog-frontend-ice5.vercel.app")
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
    .AddSubscriptionType(d => d.Name("Subscription"))
        .AddTypeExtension<MessagingSubscription>()
        .AddTypeExtension<ReactionSubscription>()
    .AddType<UserType>()
    .AddType<ReelType>()
    .AddType<PostType>()
    .AddType<ReactionType>()
    .AddType<ReplyType>()
    .AddType<NotificationTypeGql>()
    .AddType<UploadType>()
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
    // .AddMutationConventions()
    .AddInMemorySubscriptions();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSubscriptionDiagnostics();
}

var app = builder.Build();

app.UseCors("AllowFrontend");

app.UseWebSockets();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(webRootPath)
});

app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();

app.MapGraphQL("/gql").WithOptions(new GraphQLServerOptions
{
    Tool = { Enable = true }
}); ;
app.Services.ApplyMigrationAsync().GetAwaiter().GetResult();

app.MapHub<PresenceHub>("/hubs/presence");
app.Run();
