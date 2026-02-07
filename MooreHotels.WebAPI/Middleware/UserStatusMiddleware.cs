using Microsoft.AspNetCore.Identity;
using MooreHotels.Domain.Entities;
using MooreHotels.Domain.Enums;
using System.Security.Claims;

namespace MooreHotels.WebAPI.Middleware;

public class UserStatusMiddleware
{
    private readonly RequestDelegate _next;

    public UserStatusMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdStr = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(userIdStr, out var userId))
            {
                // Note: In high-traffic scenarios, consider caching this status for 30-60 seconds 
                // to reduce DB load, but for "total invasion" requirements, we query directly.
                var user = await userManager.FindByIdAsync(userIdStr);

                if (user == null || user.Status == ProfileStatus.Suspended)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new 
                    { 
                        Message = "Security Alert: Your account has been suspended. All active sessions have been terminated.",
                        ErrorCode = "ACCOUNT_SUSPENDED"
                    });
                    return;
                }
            }
        }

        await _next(context);
    }
}

public static class UserStatusMiddlewareExtensions
{
    public static IApplicationBuilder UseUserStatusInvasion(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<UserStatusMiddleware>();
    }
}