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
using MooreHotels.Infrastructure.Hubs;
using MooreHotels.Infrastructure.Identity;
using MooreHotels.Infrastructure.Persistence;
using MooreHotels.Infrastructure.Repositories;
using MooreHotels.Infrastructure.Seed;
using MooreHotels.Infrastructure.Services;
using Npgsql;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var jwtKey = builder.Configuration["Jwt:Key"];
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new Exception("JWT Key is missing or too short. Minimum 32 characters required.");

if (string.IsNullOrWhiteSpace(connectionString))
    throw new Exception("Database connection string is missing.");

if (builder.Environment.IsProduction() && allowedOrigins.Length == 0)
    throw new Exception("AllowedOrigins must be configured in Production.");

/* CORE SERVICES */
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        opt.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddHealthChecks();

/* DATABASE */
builder.Services.AddDbContext<MooreHotelsDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsql =>
    {
        npgsql.MigrationsAssembly("MooreHotels.Infrastructure");
        npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
    });
});

/* IDENTITY */
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<MooreHotelsDbContext>()
    .AddDefaultTokenProviders();

/* CORS */
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        else
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

/* AUTH & JWT */
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!))
        };
    });
builder.Services.AddAuthorization();

/* SIGNALR */
builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();

/* DEPENDENCY INJECTION */
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

/* SWAGGER (DEV ONLY) */
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "Moore Hotels API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            In = ParameterLocation.Header,
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            Description = "Bearer {your JWT token}"
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
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
}

var app = builder.Build();

/* MIGRATIONS & SEEDING WITH TABLE CHECK */
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<MooreHotelsDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    var retries = 10;
    var delay = TimeSpan.FromSeconds(5);

    for (int i = 0; i < retries; i++)
    {
        try
        {
            logger.LogInformation("Applying database migrations...");
            await db.Database.MigrateAsync();

            // Ensure AspNetRoles table exists before seeding
            var tableExists = await db.Database.ExecuteSqlRawAsync(
                @"SELECT 1 FROM information_schema.tables 
                  WHERE table_name='aspnetroles';"
            ) > 0;

            if (!tableExists)
            {
                logger.LogWarning("AspNetRoles table not found. Retrying in {0}s...", delay.TotalSeconds);
                await Task.Delay(delay);
                continue;
            }

            if (app.Configuration.GetValue<bool>("SeedAdmin"))
            {
                logger.LogInformation("Seeding admin roles and user...");
                await DbInitializer.SeedAdminAsync(services);
            }

            logger.LogInformation("Database migration and seeding completed successfully.");
            break;
        }
        catch (NpgsqlException ex) when (ex.SqlState == "42P01")
        {
            logger.LogWarning("Database not ready yet, retrying in {0}s...", delay.TotalSeconds);
            await Task.Delay(delay);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Database migration or seeding failed.");
            throw;
        }
    }
}

/* MIDDLEWARE */
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHealthChecks("/health");

app.Run();
