using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis.Maintenance;

// using StackExchange.Redis;
using System.Text;
using System.Threading.RateLimiting;
using Uchat.Database.Context;
using Uchat.Database.LiteDB;
using Uchat.Database.Repositories;
using Uchat.Database.Repositories.Interfaces;
using Uchat.Database.Services.Chat;
using Uchat.Server;
using Uchat.Server.Hubs;
using Uchat.Server.Middleware;
using Uchat.Server.Services;
using Uchat.Server.Services.Auth;

public class Program
{
    public static void Main(string[] args)
    {
        //if (args.Length < 2)
        //{
        //    Console.WriteLine("Usage: binary (--demonize / --kill) port (four digits)");
        //    return;
        //}

        var builder = WebApplication.CreateBuilder(args);

        if (args.Contains("-kill") && args.Contains("-start"))
        {
            Console.WriteLine("Usage: Uchat.Server.exe (--daemon / --kill) port (four digits)");
            return;
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

        int port = 5000;
        if (args.Length > 0 && !int.TryParse(args[^1], out port))
        {
            Console.WriteLine("Invalid port. Usage: Uchat.Server.exe (--daemon / --kill) port (four digits)");
            return;
        }

        builder.WebHost.UseKestrel(options =>
        {
            //options.ListenAnyIP(8080); // слушаем порт 8080 на всех интерфейсах
            options.ListenLocalhost(port); // options.ListenLocalhost(8080); // если только localhost
        });

        // Databases
        builder.Services.AddDbContext<UchatDbContext>(options =>
            options.UseSqlite(builder.Configuration.GetConnectionString("SQLite")));

        builder.Services.Configure<LiteDbSettings>(
            builder.Configuration.GetSection("LiteDb"));

        builder.Services.AddSingleton<LiteDbContext>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<LiteDbSettings>>().Value;
            return new LiteDbContext(settings);
        });

        builder.Services.AddSingleton<ILiteDbWriteGate, LiteDbWriteGate>();

        // Redis - для статусов пользователей (Online/Offline)
        // builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        // {
        //     var redisConfig = builder.Configuration.GetSection("Redis");
        //     var configuration = ConfigurationOptions.Parse(redisConfig["ConnectionString"]!);
        //     configuration.AbortOnConnectFail = false;
        //     return ConnectionMultiplexer.Connect(configuration);
        // });

        // ============================================================================
        // 2. РЕПОЗИТОРИИ 
        // ============================================================================

        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IChatRoomRepository, ChatRoomRepository>();
        builder.Services.AddScoped<IContactRepository, ContactRepository>();
        builder.Services.AddScoped<IFriendshipRepository, FriendshipRepository>();
        builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        builder.Services.AddScoped<IMessageRepository, MessageRepository>();
        builder.Services.AddScoped<ITransactionRunner, TransactionRunner>();

        // Services
        builder.Services.AddScoped<JwtService>();
        builder.Services.AddScoped<AuthService>();
        builder.Services.AddScoped<IChatRoomService, ChatRoomService>();

        // TODO: UserStatusService - работа со статусами через Redis
        // builder.Services.AddScoped<IUserStatusService, UserStatusService>();

        // ============================================================================
        // 4. JWT АУТЕНТИФИКАЦИЯ
        // ============================================================================

        var jwtSettings = builder.Configuration.GetSection("Jwt");
        var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme; // как проверять токен (через JWT)
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme; // что делать если токена нет (вернуть 401 Unauthorized)
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,          // Проверять кто выдал токен
                ValidateAudience = true,        // Проверять для кого токен
                ValidateLifetime = true,        // Проверять не истёк ли
                ValidateIssuerSigningKey = true,// Проверять подпись
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(secretKey),
                ClockSkew = TimeSpan.Zero       // Без допуска по времени
            };

            // Middleware извлекает токен из query параметра
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
                        .AllowCredentials(); // Важно для SignalR!
            });
        });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // Rate Limiting
        builder.Services.AddRateLimiter(options =>
        {
            // Ограничение для auth endpoints (login, register)
            options.AddFixedWindowLimiter("auth", opt =>
            {
                opt.Window = TimeSpan.FromMinutes(1);
                opt.PermitLimit = 10; // Максимум 10 попыток в минуту
                opt.QueueLimit = 0; // Не ставить в очередь
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

        // Автоматически применяем миграции при запуске
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<UchatDbContext>();
            dbContext.Database.Migrate();

            // Инициализируем LiteDB - создаём БД и индексы
            var liteDbContext = scope.ServiceProvider.GetRequiredService<LiteDbContext>();
            Console.WriteLine($"LiteDB initialized: {liteDbContext.Messages.Count()} messages");
        }

        app.UseMiddleware<ExceptionHandlerMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // Отключаем HTTPS redirect для локальной разработки
        // app.UseHttpsRedirection();

        app.UseCors("AllowAll");
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHub<ChatHub>("/chatHub");

        app.Run();
    }
}