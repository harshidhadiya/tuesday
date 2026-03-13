using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using MACUTION.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Name;
using VERIFY.Messaging;
using VERIFY.Model;
using VERIFY.Repositories;
using VERIFY.Services;
using VERIFY.Validation;
using MassTransit;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<VerifyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddCors(options =>
{
    options.AddPolicy("MyPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5087",
                "http://localhost:5000",
                "http://localhost:8080",
                "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddHttpClient("DefaultClient", option =>
{
    option.BaseAddress = new Uri(builder.Configuration["Microservice:Request_admin_url"]);
});

builder.Services.AddHttpClient("ProductService", option =>
{
    option.BaseAddress = new Uri(builder.Configuration["Microservice:Product_url"]);
});

builder.Services.AddHttpClient("UserService", option =>
{
    option.BaseAddress = new Uri(builder.Configuration["Microservice:User_url"] ?? "http://localhost:8080");
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = false,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});



builder.Services.AddValidatorsFromAssemblyContaining<verifyProductValidator>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(option =>
        option.JsonSerializerOptions.UnmappedMemberHandling =
            System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<ItokenGeneration, Tokenget>();
builder.Services.AddScoped<IVerifyRepository, VerifyRepository>();
builder.Services.AddScoped<IVerifyService, VerifyService>();

builder.Services.AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection("RabbitMq"))
    .ValidateOnStart();

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddConsumersFromNamespaceContaining<VERIFY.Messaging.Consumers.ProductDeletedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var options = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
        cfg.Host(options.HostName, options.VirtualHost, h =>
        {
            h.Username(options.UserName);
            h.Password(options.Password);
        });
        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

app.UseCors("MyPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<MappingId>();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/", () => "Verify Service — Product Verification");
app.Run();