using AUCTION.BackgroundJobs;
using AUCTION.Consumers;
using AUCTION.Data;
using AUCTION.Data.Repositories;
using AUCTION.Data.Repositories.Interfaces;
using AUCTION.ExceptionHandler;
using AUCTION.Hubs;
using AUCTION.Services;
using AUCTION.Services.Interfaces;
using AUCTION.Validation;
using FluentValidation;
using MassTransit;
using Messaging.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.CodeAnalysis.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Database (PostgreSQL) ─────────────────────────────────────────────────────
builder.Services.AddDbContext<AuctionDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Redis ─────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379"));
builder.Services.AddScoped<IRedisService, RedisService>();

// ── Repositories ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<IAuctionRepository,   AuctionRepository>();
builder.Services.AddScoped<IBidRepository,       BidRepository>();
builder.Services.AddScoped<IWatchlistRepository, WatchlistRepository>();

// ── Application Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IAuctionService,   AUCTION.Services.AuctionService>();
builder.Services.AddScoped<IBidService,       BidService>();
builder.Services.AddScoped<IWatchlistService, WatchlistService>();

// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();
builder.Services.AddScoped<IAuctionHubService, AuctionHubService>();
// BUG FIX: All three AddValidatorsFromAssemblyContaining calls scan the SAME assembly
// (AUCTION project), so validators were being registered 3x. One call is sufficient.
builder.Services.AddValidatorsFromAssemblyContaining<CreateAuctionRequestValidator>();


builder.Services.AddMassTransit(x =>
{
    // ── Consumers (messages we receive from other services) ───────────────────
    x.AddConsumer<ProductVerifiedConsumer>();
    x.AddConsumer<ProductUnverifiedConsumer>();
    x.AddConsumer<ProductDeleteConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(
            builder.Configuration["RabbitMQ:Host"] ?? "localhost",
            builder.Configuration["RabbitMQ:VHost"] ?? "/",
            h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });

        // ── Retry policy ──────────────────────────────────────────────────────
        cfg.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(30)));

        

        cfg.ConfigureEndpoints(ctx);
    });
});

// ── Scheduler Background Job ──────────────────────────────────────────────────
builder.Services.AddHostedService<AuctionSchedulerJob>();
builder.Services.AddHttpClient("api_gateway",opt=>opt.BaseAddress=new Uri(builder.Configuration["Microservice:api_gate_way"] ?? ""));
// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
      
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = false,
            ValidateAudience         = false,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = false,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        // Required: SignalR passes token as query string for WebSocket connections
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                var path  = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(token) && path.StartsWithSegments("/hubs/auction"))
                    context.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddExceptionHandler<GlobalHandler>();
// ── CORS (required for SignalR + browser clients — allows ANY frontend origin) ─
builder.Services.AddCors(opt =>
    opt.AddPolicy("AllowFrontend", policy =>
        policy
            .SetIsOriginAllowed(_ => true)   // allow any origin (dev + prod frontends)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));             // required for SignalR WebSocket auth

// ── Swagger ───────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Auction Service", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In          = ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}",
        Name        = "Authorization",
        Type        = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddProblemDetails();
builder.Services.AddControllers();

var app = builder.Build();

// ── Auto-migrate on startup ───────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuctionDbContext>();
    db.Database.Migrate();
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseStatusCodePages();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── SignalR hub ───────────────────────────────────────────────────────────────
app.MapHub<AuctionHub>("/hubs/auction");

app.Run("http://localhost:5001");

// Needed for WebApplicationFactory in integration tests
public partial class Program { }
