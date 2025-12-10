using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.Text;
using System.Threading.RateLimiting;
using Uchat;
using Uchat.Shared;
using Uchat.Database.Context;
using Uchat.Database.MongoDB;
using Uchat.Database.Repositories;
using Uchat.Database.Repositories.Interfaces;
using Uchat.Server.Services.Chat;
using Uchat.Server;
using Uchat.Server.Hubs;
using Uchat.Server.Middleware;
using Uchat.Server.Services;
using Uchat.Server.Services.Auth;
using Uchat.Server.Services.Messaging;
using Uchat.Server.Services.Contact;
using Uchat.Server.Data;
using Uchat.Server.Services.Presence;
using Uchat.Server.Services.Redis;
using Uchat.Server.Services.Reconnection;
using Uchat.Server.Services.Unread;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        try
        {
            //if (!ConnectionConfig.ValidServerArgs(args))
            //{
            //    throw new Exception("Invalid arguments");
            //}
            //if (args.Contains("-start"))
            //{
            //    SelfDaemon.RunDetached(args);
            //    return;
            //}
            //if (args.Contains("-kill"))
            //{
            //    SelfDaemon.KillExisting();
            //    return;
            //}

            //int port = int.Parse(args[^1]);
            int port = 6000;
            builder.WebHost.UseKestrel(options =>
            {
                options.ListenLocalhost(port);
            });
        }
        catch (Exception)
        {
            Console.WriteLine("Usage:\nUchat.Server.exe -start [port] (four digits)\nor\nUchat.Server.exe -kill");
            Environment.Exit(1);
        }


        builder.Services.AddDbContext<UchatDbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL"));
            //Console.WriteLine("Using PostgreSQL (Docker shared database)");
        });

        builder.Services.Configure<MongoDbSettings>(
            builder.Configuration.GetSection("MongoDB"));

        var runningInContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
        var redisConn = builder.Configuration.GetSection("Redis")["ConnectionString"];
        if (runningInContainer && (string.IsNullOrWhiteSpace(redisConn) || redisConn.Contains("localhost", StringComparison.OrdinalIgnoreCase)))
        {
            builder.Configuration["Redis:ConnectionString"] = "redis:6379";
            Console.WriteLine("Detected container environment — overriding Redis:ConnectionString to 'redis:6379'");
        }

        builder.Services.Configure<RedisSettings>(
            builder.Configuration.GetSection("Redis"));

        builder.Services.AddSingleton<MongoDbContext>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
            return new MongoDbContext(settings);
        });

        builder.Services.AddSingleton<IRedisService, RedisService>();

        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IPendingRegistrationRepository, PendingRegistrationRepository>();
        builder.Services.AddScoped<IChatRoomRepository, ChatRoomRepository>();
        builder.Services.AddScoped<IContactRepository, ContactRepository>();
        builder.Services.AddScoped<IUserSecurityTokenRepository, UserSecurityTokenRepository>();
        builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        builder.Services.AddScoped<IMessageRepository, MessageRepository>();
        builder.Services.AddScoped<ITransactionRunner, TransactionRunner>();

        // Services
        builder.Services.AddScoped<JwtService>();
        builder.Services.Configure<Uchat.Server.Services.Email.EmailSettings>(builder.Configuration.GetSection("Email"));
        builder.Services.AddScoped<Uchat.Server.Services.Email.IEmailSender, Uchat.Server.Services.Email.SmtpEmailSender>();

        builder.Services.AddScoped<UserSecurityService>();
        builder.Services.AddScoped<AuthService>();
        builder.Services.AddScoped<IChatRoomService, ChatRoomService>();
        builder.Services.AddScoped<IMessageService, MessageService>();
        builder.Services.AddScoped<IContactService, ContactService>();
        builder.Services.AddScoped<IUnreadCounterService, UnreadCounterService>();
        builder.Services.AddSingleton<IUserPresenceService, UserPresenceService>();
        builder.Services.AddScoped<IReconnectionService, ReconnectionService>();
        builder.Services.AddHostedService<Uchat.Server.Services.Background.PendingCleanupService>();

        var jwtSettings = builder.Configuration.GetSection("Jwt");
        var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(secretKey),
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;

                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chatHub"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });

        builder.Services.AddAuthorization();
        builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
        {
            options.SuppressModelStateInvalidFilter = true;
        });
        builder.Services.AddControllers();
        builder.Services.AddSignalR();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.SetIsOriginAllowed(_ => true)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });
        });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // Rate Limiting
        builder.Services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("auth", opt =>
            {
                opt.Window = TimeSpan.FromMinutes(1);
                opt.PermitLimit = 10;
                opt.QueueLimit = 0;
            });

            options.AddFixedWindowLimiter("api", opt =>
            {
                opt.Window = TimeSpan.FromMinutes(1);
                opt.PermitLimit = 100;
                opt.QueueLimit = 0;
            });

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                await context.HttpContext.Response.WriteAsync(
                    "Too many requests. Please try again later.",
                    cancellationToken: token);
            };
        });

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<Program>>();
            var dbContext = services.GetRequiredService<UchatDbContext>();

            int maxRetries = 10;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    logger.LogInformation($"Attempt {i + 1}/{maxRetries} to connect and migrate...");

                    dbContext.Database.Migrate();
                    
                    logger.LogInformation("PostgreSQL migrations applied successfully.");

                    logger.LogInformation("Initializing system data (seeding)...");
                    await DbInitializer.InitializeAsync(dbContext);
                    logger.LogInformation("System data initialized successfully");

                    logger.LogInformation("Initializing MongoDB...");
                    var mongoDbContext = services.GetRequiredService<MongoDbContext>();
                    var messageCount = mongoDbContext.Messages.CountDocuments(Builders<MongoMessage>.Filter.Empty);
                    logger.LogInformation("MongoDB initialized: {MessageCount} messages", messageCount);
                    break; 
                }
                catch (Exception ex)
                {
                    if (i == maxRetries - 1) 
                    {
                        logger.LogCritical(ex, "FATAL ERROR: Database did not start after multiple attempts.");
                        throw; 
                    }

                    logger.LogWarning($"Database not ready yet. Error: {ex.Message}");
                    logger.LogWarning($"Waiting 3 seconds before retry...");
                    
                    await Task.Delay(3000);
                }
            }
        }

        app.UseMiddleware<ExceptionHandlerMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors("AllowAll");
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHub<ChatHub>("/chatHub");

        app.Run();
    }
}
