using System.Text;
using AutoMapper;
using FluentValidation;
using FluentValidation.AspNetCore;
using MACUTION.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PRODUCT.Messaging;
using PRODUCT.GlobalErrorHandler;
using PRODUCT.Model;
using PRODUCT.Validation;
using PRODUCT.Services;
using PRODUCT.Mapper;
using PRODUCT.Repository;
using MassTransit;
using PRODUCT.Messaging.Consumers;



var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<PasswordHasher<object>>();
builder.Services.AddDbContext<MACUTIONDB>(options=>options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddValidatorsFromAssemblyContaining<productCreateValidation>();
builder.Services.AddScoped<Irepository,Repository>();
builder.Services.AddValidatorsFromAssemblyContaining<ProductRequestValidation>();
builder.Services.AddValidatorsFromAssemblyContaining<ProductUpdateValidation>();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddCors(options =>
{
    options.AddPolicy("MyPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5087", "http://localhost:5000", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
builder.Services.AddHttpClient("VerifyService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Microservice:Verify_url"] ?? "http://localhost:5089");
});

builder.Services.AddHttpClient("UserService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Microservice:User_url"] ?? "http://localhost:8080");
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };  
});
builder.Services.AddScoped<IproductService,ProductService>();
builder.Services.AddExceptionHandler<GlobalErrorHandler>();
builder.Services.AddAuthorization();
builder.Services.AddControllers().AddJsonOptions((option=>option.JsonSerializerOptions.UnmappedMemberHandling= System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddAutoMapper(typeof(Mapper));
builder.Services.AddScoped<IVerificationService, HttpVerificationService>();
builder.Services.AddAutoMapper(typeof(Mapping));
builder.Services.AddRabbitMqMessaging(builder.Configuration);
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
  x.AddConsumer<verifyConsumer>();
    x.UsingRabbitMq((context, cnf) =>
    {
        var options=context.GetService<IOptions<RabbitMqOptions>>().Value;
        cnf.Host(options.HostName, options.VirtualHost, (k) =>
        {
            k.Username(options.UserName);
            k.Password(options.Password);
        });
      cnf.ConfigureEndpoints(context);        
    });    

});
var app = builder.Build();
Console.WriteLine(DateTime.Now);
app.UseCors("MyPolicy");
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<MappingId>();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/", () => "Creating Project For User Management System");
app.Run();
