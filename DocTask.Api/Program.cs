using System.Text;
using CloudinaryDotNet;
using DockTask.Api.Configurations;
using DocTask.Core.Models;
using DockTask.Api.Handlers;
using DocTask.Core.Dtos.Gemini;
using DocTask.Data;
using DocTask.Service.Services;
using DotNetEnv;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using DocTask.Core.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwagger();
builder.Services.AddSignalR();

// builder.Services.AddHttpClient();

Env.Load();
Console.WriteLine("========== START DEBUG ==========");

// Lưu tạm phản hồi của AI vào cache
builder.Services.AddMemoryCache();

// Configure JSON serialization to handle circular references
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.WriteIndented = true;
});

// Configuration SQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(Environment.GetEnvironmentVariable("DEFAULT_CONNECTION"));
});

// Configuration JWT
builder.Services.Configure<JwtSetting>(options =>
{
    options.AccessSecretKey = Environment.GetEnvironmentVariable("JWT_ACCESS_SECRET_KEY") ?? "";
    options.RefreshSecretKey = Environment.GetEnvironmentVariable("JWT_REFRESH_SECRET_KEY") ?? "";
    options.AccessTokenExpiry = Environment.GetEnvironmentVariable("JWT_ACCESS_TOKEN_EXPIRY") ?? "";
    options.RefreshTokenExpiry = Environment.GetEnvironmentVariable("JWT_REFRESH_TOKEN_EXPIRY") ?? "";
    options.Issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "";
    options.Audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "";
});

// --- Cấu hình Cloudinary settings ---
builder.Services.Configure<CloudinarySettings>(options =>
{
    options.CloudName = Environment.GetEnvironmentVariable("CLOUDINARY_CLOUDNAME") ?? "";
    options.ApiKey = Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY") ?? "";
    options.ApiSecret = Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET") ?? "";
    options.Folder = Environment.GetEnvironmentVariable("CLOUDINARY_FOLDER") ?? "doctask";
});
Console.WriteLine($"CLOUDINARY_CLOUDNAME = {Environment.GetEnvironmentVariable("CLOUDINARY_CLOUDNAME")}");
Console.WriteLine($"CLOUDINARY_API_KEY   = {Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY")}");
Console.WriteLine($"CLOUDINARY_FOLDER    = {Environment.GetEnvironmentVariable("CLOUDINARY_FOLDER")}");

// Đăng ký SMTP settings từ environment
builder.Services.Configure<SmtpSettings>(options =>
{
    options.Server = Environment.GetEnvironmentVariable("SMTP_SERVER");
    options.Port = int.Parse(Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587");
    options.SenderName = Environment.GetEnvironmentVariable("SMTP_SENDER_NAME");
    options.SenderEmail = Environment.GetEnvironmentVariable("SMTP_SENDER_EMAIL");
    options.Username = Environment.GetEnvironmentVariable("SMTP_USERNAME");
    options.Password = Environment.GetEnvironmentVariable("SMTP_PASSWORD");
    options.EnableSsl = bool.Parse(Environment.GetEnvironmentVariable("SMTP_ENABLE_SSL") ?? "true");
});

// Đăng ký Cloudinary instance
builder.Services.AddSingleton<Cloudinary>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<CloudinarySettings>>().Value;
    
    var account = new Account(
        settings.CloudName,
        settings.ApiKey,
        settings.ApiSecret
    );
    
    return new Cloudinary(account);
});

// Configuration GeminiAI
builder.Services.AddSingleton(new GeminiDto.GeminiOptions
{
  ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? ""
});


builder.Services.AddControllerConfiguration();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()

               .WithOrigins(Environment.GetEnvironmentVariable("CLIENT_URL") ?? "http://localhost:4200", "http://localhost:4200"); // frontend URL
    });
});

// Authorization (tích hợp role-based)
builder.Services.AddAuthentication("JwtAuth")  // Set default scheme
    .AddJwtBearer("JwtAuth", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "",
            ValidAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JWT_ACCESS_SECRET_KEY") ?? "")),
            ClockSkew = TimeSpan.Zero
        };
        // Cho phép token trong query cho SignalR
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/notificationHub"))
                {
                    context.Token = accessToken;
                }
                return System.Threading.Tasks.Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("User", policy => policy.RequireRole("User"));
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddApplicationContainer();

using (var scope = builder.Services.BuildServiceProvider().CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        if (dbContext.Database.CanConnect())
        {
            Console.WriteLine("Kết nối đến SQL Server thành công!");
        }
        else
        {
            Console.WriteLine("Không thể kết nối đến SQL Server.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Lỗi khi kết nối đến SQL Server: {ex.Message}");
    }
}

var app = builder.Build();

// Kiểm tra Cloudinary đã được khởi tạo thành công
using (var scope = app.Services.CreateScope())
{
    var cloudinary = scope.ServiceProvider.GetRequiredService<Cloudinary>();
    Console.WriteLine("Cloudinary client initialized successfully!");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var enableSwagger = Environment.GetEnvironmentVariable("ENABLE_SWAGGER");
if (app.Environment.IsDevelopment() || string.Equals(enableSwagger, "true", StringComparison.OrdinalIgnoreCase))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DocTask API v1");
        // Optional: serve Swagger at app root
        // options.RoutePrefix = string.Empty;
    });
}

app.UseCors("AllowAll");
app.UseJwtAuthentication();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationHub>("/notificationHub");
app.UseExceptionHandler(_ => {});
app.UseHttpsRedirection();

app.Run();