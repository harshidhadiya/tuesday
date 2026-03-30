using System.Text;
using AutoMapper;
using FluentValidation;
using FluentValidation.AspNetCore;
using MACUTION.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Name;
using USER.Data.Interfaces;
using USER.MAPPER;
using USER.Messaging;
using USER.Model;
using USER.Repository;
using USER.Services;
using USER.Validation;
using MassTransit;
using Microsoft.Extensions.Options;
using USER.CloudinaryService;
using USER.Messaging.Consumer;
using Helper;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<PasswordHasher<object>>();
builder.Services.AddDbContext<MACUTIONDB>(options=>options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddValidatorsFromAssemblyContaining<UserCreateValidation>();
builder.Services.AddValidatorsFromAssemblyContaining<ChangeProfileValidator>();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddScoped<IsellerLogin,SellerLogin>();
builder.Services.AddScoped<IadminLogin,AdminLogin>();
builder.Services.AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection("RabbitMq"))
    .ValidateOnStart();
    builder.Services.AddScoped<IHttpRequestCommon,HttpRequestCommon>();

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddConsumer<ImageDeleteConsumer>(e =>
    {
        e.UseMessageRetry(x=>x.Interval(6,TimeSpan.FromSeconds(30)));
    });
    x.UsingRabbitMq((context, cfg) =>
    {
        var options = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
        cfg.Host(options.HostName, options.VirtualHost, h =>
        {
            h.Username(options.UserName);
            h.Password(options.Password);
        });
        cfg.ReceiveEndpoint("user-messaging-consumer-image-delete-consumer", x =>
        {
            x.ConfigureConsumer<ImageDeleteConsumer>(context);
        });
        cfg.ConfigureEndpoints(context);
    });
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("MyPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5087", "http://localhost:5000", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
              
    });
});

builder.Services.AddHttpClient("DefaultClient", option => {
    option.BaseAddress = new Uri(builder.Configuration["Microservice:Request_admin_url"]);
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
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserAdminService, UserAdminService>();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<ItokenGeneration,Tokenget>();
builder.Services.AddAutoMapper(typeof(Mapper));
builder.Services.AddScoped<IClodinaryService,ClodinaryService>();
builder.Services.AddScoped<Ihelper,Helpers>();
builder.Services.AddHealthChecks();
var app = builder.Build();
app.MapHealthChecks("/health");
app.UseCors("MyPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<MappingId>();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/", () => "Creating Project For User Management System");
app.Run();