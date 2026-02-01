using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MooreHotels.Application.Interfaces;
using MooreHotels.Application.Interfaces.Repositories;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Application.Services;
using MooreHotels.Domain.Entities;
using MooreHotels.Infrastructure.Identity;
using MooreHotels.Infrastructure.Persistence;
using MooreHotels.Infrastructure.Repositories;
using MooreHotels.Infrastructure.Seed;
using MooreHotels.Infrastructure.Services;
using MooreHotels.Infrastructure.Hubs;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// =======================
// CONFIGURATION
// =======================
var configuration = builder.Configuration;

var connectionString =
    configuration.GetConnectionString("DefaultConnection")
    ?? throw new Exception("Database connection string not configured");

var jwtKey =
    configuration["Jwt:Key"]
    ?? throw new Exception("JWT key not configured");

// =======================
// SERVICES
// =======================
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddDbContext<MooreHotelsDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
    {
        npgsql.MigrationsAssembly("MooreHotels.Infrastructure");
        npgsql.EnableRetryOnFailure();
    }));

builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<MooreHotelsDbContext>()
.AddDefaultTokenProviders();

// =======================
// AUTHENTICATION (JWT)
// =======================
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // REQUIRED for Render
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = configuration["Jwt:Issuer"],

        ValidateAudience = true,
        ValidAudience = configuration["Jwt:Audience"],

        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey)
        ),

        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();

// =======================
// SWAGGER (ENABLED IN PROD)
// =======================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Moore Hotels API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// =======================
// DI
// =======================
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPaymentService, MockPaymentService>();

builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IGuestRepository, GuestRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IVisitRecordRepository, VisitRecordRepository>();

builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IGuestService, GuestService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IVisitRecordService, VisitRecordService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IStaffService, StaffService>();
builder.Services.AddScoped<IOperationService, OperationService>();
builder.Services.AddScoped<INotificationService, MooreHotels.Application.Services.NotificationService>();

var app = builder.Build();

// =======================
// DATABASE MIGRATION
// =======================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var db = services.GetRequiredService<MooreHotelsDbContext>();
        await db.Database.MigrateAsync();

        if (configuration.GetValue<bool>("SeedAdmin"))
        {
            await DbInitializer.SeedAdminAsync(services);
        }
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Database migration failed");
        throw;
    }
}

// =======================
// MIDDLEWARE (ORDER MATTERS)
// =======================
app.UseForwardedHeaders();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Moore Hotels API v1");
    c.RoutePrefix = "swagger";
});

app.UseRouting();

app.UseCors(policy =>
{
    var origins = configuration.GetSection("AllowedOrigins").Get<string[]>();
    if (origins != null && origins.Length > 0)
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    else
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
