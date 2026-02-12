using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MooreHotels.Application.DTOs;
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
using MooreHotels.WebAPI.Middleware;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
{
    Env.Load();
}

var builder = WebApplication.CreateBuilder(args);

// --- 1. CONFIGURATION & SECRET VALIDATION ---
var jwtKey = builder.Configuration["Jwt:Key"];
var dbConnection = builder.Configuration.GetConnectionString("DefaultConnection");
var isProduction = builder.Environment.IsProduction();

if (isProduction)
{
    if (string.IsNullOrEmpty(jwtKey) || jwtKey.Contains("Placeholder") || jwtKey.Length < 32)
        throw new Exception("FATAL: JWT Key is invalid. Ensure 'Jwt__Key' environment variable is correctly set.");

    if (string.IsNullOrEmpty(dbConnection) || dbConnection.Contains("localhost"))
        throw new Exception("FATAL: Production database connection string is missing.");
}

// --- 2. CORE SERVICES ---
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, true));
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

builder.Services.AddDbContext<MooreHotelsDbContext>(options =>
    options.UseNpgsql(dbConnection, npgsql => {
        npgsql.MigrationsAssembly("MooreHotels.Infrastructure");
        npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
    }));

builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options => {
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<MooreHotelsDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options => {
    options.Events.OnRedirectToLogin = context => {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context => {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

// --- 3. CORS ---
// builder.Services.AddCors(options => {
//     options.AddDefaultPolicy(policy => {
//         if (builder.Environment.IsDevelopment()) {
//             policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
//         } else {
//             var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
//             policy.WithOrigins(origins)
//                   .AllowAnyHeader()
//                   .AllowAnyMethod()
//                   .AllowCredentials();
//         }
//     });
// });

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// --- 4. AUTHENTICATION & SECURITY ---
builder.Services.AddAuthentication(options => {
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options => {
    options.TokenValidationParameters = new TokenValidationParameters {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// --- SETTINGS BINDING ---
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("CloudinarySettings"));

// --- 5. DEPENDENCY INJECTION ---
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
builder.Services.AddScoped<IImageService, CloudinaryService>();

// --- 6. SWAGGER ---
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Moore Hotels API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme {
        In = ParameterLocation.Header,
        Description = "Please enter JWT with 'Bearer ' prefix",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// --- 7. DB AUTOMATION ---
using (var scope = app.Services.CreateScope()) {
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<MooreHotelsDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();
    
    try {
        await context.Database.MigrateAsync();
        if (app.Configuration.GetValue<bool>("SeedAdmin")) {
            await DbInitializer.SeedAdminAsync(services);
        }
    } catch (Exception ex) {
        logger.LogError(ex, "DB initialization failure.");
    }
}

// --- 8. MIDDLEWARE PIPELINE ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Moore Hotels API v1");
    });
}

if (!app.Environment.IsDevelopment()) {
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors();

app.UseAuthentication();
app.UseUserStatusInvasion();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

await app.RunAsync();
