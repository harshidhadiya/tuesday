using System.Text;
using AutoMapper;
using FluentValidation.AspNetCore;
using MACUTION.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Name;
using USER.MAPPER;
using ADMIN.Messaging;
using ADMIN.Model;
using ADMIN.Repositories;
using ADMIN.Services;
using Microsoft.AspNetCore.Identity;
using MassTransit;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<PasswordHasher<object>>();
builder.Services.AddDbContext<MACUTIONDB>(options=>options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection("RabbitMq"))
    .ValidateOnStart();

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddConsumersFromNamespaceContaining<ADMIN.Messaging.Consumers.RequestConsumer>();

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
builder.Services.AddAutoMapper(typeof(MappingProfile));
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };  
});
builder.Services.AddAuthorization();
builder.Services.AddControllers().AddJsonOptions((option=>option.JsonSerializerOptions.UnmappedMemberHandling= System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors(options =>
{
    options.AddPolicy("MyPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:8080", "http://localhost:5000", "http://localhost:5087", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<ItokenGeneration,Tokenget>();
builder.Services.AddScoped<IRequestRepository, RequestRepository>();
builder.Services.AddScoped<IRequestService, RequestService>();
builder.Services.AddScoped<IAdminProductService, AdminProductService>();
var app = builder.Build();
app.UseCors("MyPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<MappingId>();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/", () => "Admin & Request service");
app.Run();