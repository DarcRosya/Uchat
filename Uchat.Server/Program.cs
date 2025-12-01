using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
// using StackExchange.Redis;
using System.Text;
using Uchat.Database.Context;
using Uchat.Database.LiteDB;
using Uchat.Database.Repositories;
using Uchat.Database.Repositories.Interfaces;
using Uchat.Server.Middleware;
using Uchat.Server.Services;
using Uchat.Server.Services.Auth;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 1. БАЗЫ ДАННЫХ
// ============================================================================

// SQLite 
// Lifetime: Scoped (новый экземпляр на каждый HTTP запрос)
builder.Services.AddDbContext<UchatDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SQLite")));

// LiteDB 
// Регистрируем настройки из appsettings.json
builder.Services.Configure<LiteDbSettings>(
    builder.Configuration.GetSection("LiteDb"));

// LiteDbContext как Singleton (один экземпляр на приложение)
// ПОЧЕМУ Singleton? LiteDB держит файл открытым, нужно переиспользовать подключение
builder.Services.AddSingleton<LiteDbContext>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<LiteDbSettings>>().Value;
    return new LiteDbContext(settings);
});

// Write Gate для синхронизации записей в LiteDB (thread-safety)
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

// ============================================================================
// 3. СЕРВИСЫ 
// ============================================================================

// JwtService - генерация JWT токенов
builder.Services.AddScoped<JwtService>();

builder.Services.AddScoped<AuthService>();

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

    // Проблема: 
    // HTTP запросы отправляют токен в заголовке Authorization: Bearer 
    // SignalR не может отправлять custom headers

    // Решение:
    // SignalR отправляет токен в query string
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

// Регистрация Controllers
builder.Services.AddControllers();

// Регистрация SignalR
builder.Services.AddSignalR();

// CORS для клиента
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Swagger для тестирования API
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlerMiddleware>(); 

// ============================================================================
// КОНФИГУРАЦИЯ MIDDLEWARE PIPELINE
// ============================================================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// TODO: Добавить маппинг SignalR Hub
// app.MapHub<ChatHub>("/chatHub");

app.Run();

