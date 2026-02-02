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
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// --- 1. CONFIGURATION & SECRET VALIDATION ---
// In production, these are pulled from Render Environment Variables
var jwtKey = builder.Configuration["Jwt:Key"];
var dbConnection = builder.Configuration.GetConnectionString("DefaultConnection");
var isProduction = builder.Environment.IsProduction();

// Critical Validation: App should not start in production if secrets are compromised/missing
if (isProduction)
{
    if (string.IsNullOrEmpty(jwtKey) || jwtKey.Contains("Placeholder") || jwtKey.Length < 32)
        throw new Exception("FATAL: JWT Key is invalid or insecure. Ensure 'Jwt__Key' environment variable is set.");

    if (string.IsNullOrEmpty(dbConnection) || dbConnection.Contains("localhost"))
        throw new Exception("FATAL: Production database connection string is missing or pointing to localhost.");
}

// --- 2. CORE SERVICES ---
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
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

// Prevent Identity from redirecting to /Account/Login for API consumers
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

// --- 3. PRODUCTION-GRADE CORS ---
builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy => {
        if (builder.Environment.IsDevelopment()) {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        } else {
            var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
            policy.WithOrigins(origins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
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

// --- 6. SWAGGER ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Moore Hotels API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme {
        In = ParameterLocation.Header,
        Description = "Please enter JWT with 'Bearer ' prefix",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
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

// --- 7. DB AUTOMATION (MIGRATIONS & SEEDING) ---
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
        logger.LogError(ex, "An error occurred during DB initialization.");
    }
}

// --- 8. MIDDLEWARE PIPELINE ---
app.UseSwagger();
app.UseSwaggerUI();

if (!app.Environment.IsDevelopment()) {
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();