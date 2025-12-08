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

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        //Поддержка аргументов демонизации
        if (args.Contains("-kill") && args.Contains("-start"))
        {
            Console.WriteLine("Usage: Uchat.Server.exe (--daemon / --kill) port (four digits)");
            return;
        }
        if (!ConnectionConfig.ValidServerArgs(args))
        {
            Console.WriteLine("Usage:\nUchat.Server.exe -start port (four digits)\nor\nUchat.Server.exe -kill");
            Environment.Exit(0);
        }

        if (args.Contains("-kill"))
        {
            SelfDaemon.KillExisting();
            return;
        }
        else if (args.Contains("-start"))
        {
            SelfDaemon.RunDetached(args);
            return;
        }

        int port = int.Parse(args[^1]);
        //int port = 6000;
        builder.WebHost.UseKestrel(options =>
        {
            options.ListenLocalhost(port);
        });

        // ============================================================================
        // БАЗЫ ДАННЫХ
        // ============================================================================
        
        // PostgreSQL для пользователей, чатов, контактов (из Docker)
        builder.Services.AddDbContext<UchatDbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL"));
            Console.WriteLine("Using PostgreSQL (Docker shared database)");
        });

        // MongoDB для сообщений (из Docker)
        builder.Services.Configure<MongoDbSettings>(
            builder.Configuration.GetSection("MongoDB"));

        builder.Services.AddSingleton<MongoDbContext>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
            return new MongoDbContext(settings);
        });

        // ============================================================================
        // РЕПОЗИТОРИИ 
        // ============================================================================

        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IChatRoomRepository, ChatRoomRepository>();
        builder.Services.AddScoped<IContactRepository, ContactRepository>();
        builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        builder.Services.AddScoped<IMessageRepository, MessageRepository>();
        builder.Services.AddScoped<ITransactionRunner, TransactionRunner>();

        // Services
        builder.Services.AddScoped<JwtService>();
        builder.Services.AddScoped<AuthService>();
        builder.Services.AddScoped<IChatRoomService, ChatRoomService>();
        builder.Services.AddScoped<IMessageService, MessageService>();
        builder.Services.AddScoped<IContactService, ContactService>();

        // ============================================================================
        // JWT АУТЕНТИФИКАЦИЯ
        // ============================================================================

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

        // ============================================================================
        // ИНИЦИАЛИЗАЦИЯ БАЗ ДАННЫХ
        // ============================================================================
        using (var scope = app.Services.CreateScope())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                logger.LogInformation("Applying PostgreSQL migrations...");
                var dbContext = scope.ServiceProvider.GetRequiredService<UchatDbContext>();
                dbContext.Database.Migrate();
                logger.LogInformation("PostgreSQL migrations applied successfully");
                
                // Инициализация системных сущностей (System User + Global Chat)
                logger.LogInformation("Initializing system data (seeding)...");
                await DbInitializer.InitializeAsync(dbContext);
                logger.LogInformation("System data initialized successfully");

                logger.LogInformation("Initializing MongoDB...");
                var mongoDbContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
                var messageCount = mongoDbContext.Messages.CountDocuments(Builders<MongoMessage>.Filter.Empty);
                logger.LogInformation("MongoDB initialized: {MessageCount} messages", messageCount);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Database initialization failed");
                throw;
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
