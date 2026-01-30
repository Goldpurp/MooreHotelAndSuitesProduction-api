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
using System.Text;
using System.Security.Claims;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// 1. CONFIGURATION
var configuration = builder.Configuration;
var connectionString = configuration.GetConnectionString("DefaultConnection") 
                       ?? throw new Exception("Database connection string not configured.");
var jwtKey = configuration["Jwt:Key"] ?? throw new Exception("JWT Key missing in configuration.");

// 2. SERVICES
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddDbContext<MooreHotelsDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
    {
        npgsql.MigrationsAssembly("MooreHotels.Infrastructure");
        npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
    }));

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<MooreHotelsDbContext>()
.AddDefaultTokenProviders();

// JWT Auth
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = true;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = "MooreHotels",
        ValidateAudience = true,
        ValidAudience = "MooreHotels_Clients",
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Moore Hotels API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

// DI
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
builder.Services.AddScoped<INotificationService, MooreHotels.Infrastructure.Services.NotificationService>();

var app = builder.Build();

// 3. DATABASE MIGRATIONS & SEEDING
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var db = services.GetRequiredService<MooreHotelsDbContext>();
        logger.LogInformation("Applying migrations...");
        await db.Database.MigrateAsync(); // <-- ensures Identity tables exist

        if (configuration.GetValue<bool>("SeedAdmin"))
        {
            logger.LogInformation("Seeding admin user...");
            await DbInitializer.SeedAdminAsync(services);
        }

        logger.LogInformation("Database ready.");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Database migration or seeding failed.");
        throw;
    }
}

// 4. PIPELINE
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Use CORS from config in production
if (app.Environment.IsProduction())
{
    var allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
    app.UseCors(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
    );
}
else
{
    app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");
app.Run();
