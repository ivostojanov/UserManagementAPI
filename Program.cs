using System.ComponentModel.DataAnnotations;
using Microsoft.OpenApi.Models;
using UserManagementAPI.Models;
using UserManagementAPI.Stores;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        Description = "Enter just the token value (without 'Bearer' prefix). Example: token_demo123"
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
            new string[] { }
        }
    });
});
builder.Services.AddSingleton<IUserStore, InMemoryUserStore>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<AuthenticationMiddleware>();
app.UseMiddleware<LoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/users", async (int? page, int? size, IUserStore store, ILogger<Program> log) =>
{
    try
    {
        var p = page.GetValueOrDefault(1);
        var s = size.GetValueOrDefault(100);
        var users = await store.GetAllAsync(p, s);
        return Results.Ok(users);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Failed to get users");
        return Results.Problem("Internal server error");
    }
})
    .WithName("GetUsers")
    .WithOpenApi();

app.MapGet("/users/{id:int}", async (int id, IUserStore store, ILogger<Program> log) =>
{
    try
    {
        var user = await store.GetAsync(id);
        return user is not null ? Results.Ok(user) : Results.NotFound();
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Failed to get user {Id}", id);
        return Results.Problem("Internal server error");
    }
})
    .WithName("GetUserById")
    .WithOpenApi();

app.MapPost("/users", async (CreateUserRequest req, IUserStore store, ILogger<Program> log) =>
{
    if (req is null) return Results.BadRequest(new { Error = "Request body is required" });
    if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest(new { Error = "Name is required" });
    if (!string.IsNullOrWhiteSpace(req.Email) && !new EmailAddressAttribute().IsValid(req.Email))
    {
        return Results.BadRequest(new { Error = "Email is invalid" });
    }

    try
    {
        var created = await store.AddAsync(req.Name, req.Email);
        return Results.Created($"/users/{created.Id}", created);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Failed to create user");
        return Results.Problem("Internal server error");
    }
})
    .WithName("CreateUser")
    .WithOpenApi();

app.MapPut("/users/{id:int}", async (int id, CreateUserRequest req, IUserStore store, ILogger<Program> log) =>
{
    if (req is null) return Results.BadRequest(new { Error = "Request body is required" });
    if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest(new { Error = "Name is required" });
    if (!string.IsNullOrWhiteSpace(req.Email) && !new EmailAddressAttribute().IsValid(req.Email))
    {
        return Results.BadRequest(new { Error = "Email is invalid" });
    }

    try
    {
        var updated = await store.UpdateAsync(id, req.Name, req.Email);
        return updated ? Results.NoContent() : Results.NotFound();
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Failed to update user {Id}", id);
        return Results.Problem("Internal server error");
    }
})
    .WithName("UpdateUser")
    .WithOpenApi();

app.MapDelete("/users/{id:int}", async (int id, IUserStore store, ILogger<Program> log) =>
{
    try
    {
        var deleted = await store.DeleteAsync(id);
        return deleted ? Results.NoContent() : Results.NotFound();
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Failed to delete user {Id}", id);
        return Results.Problem("Internal server error");
    }
})
    .WithName("DeleteUser")
    .WithOpenApi();

// Login endpoint - generates a valid token
app.MapPost("/auth/login", (ILogger<Program> log) =>
{
    var token = "token_" + Guid.NewGuid().ToString().Substring(0, 8);
    TokenStore.AddToken(token);
    log.LogInformation("User logged in, token: {Token}", token);
    return Results.Ok(new { token, message = "Use this token in the Authorization header or Swagger Authorize dialog" });
})
    .WithName("Login")
    .WithOpenApi();

app.Run();

// Token store - in-memory storage of valid tokens
public static class TokenStore
{
    private static readonly HashSet<string> ValidTokens = new()
    {
        "token_demo123",
        "token_test456",
        "token_admin789"
    };

    public static void AddToken(string token)
    {
        ValidTokens.Add(token);
    }

    public static bool IsValidToken(string token)
    {
        return ValidTokens.Contains(token);
    }
}

// Authentication middleware
public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthenticationMiddleware> _logger;
    private static readonly string[] PublicPaths = { "/swagger", "/auth/login", "/health" };

    public AuthenticationMiddleware(RequestDelegate next, ILogger<AuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip authentication for public endpoints
        var path = context.Request.Path.Value;
        if (PublicPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Extract token from Authorization header
        var authHeader = context.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader))
        {
            _logger.LogWarning("Request to {Path} missing Authorization header", path);
            await HandleUnauthorizedAsync(context);
            return;
        }

        // Extract token - handle both "Bearer token" and plain "token" formats
        var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader.Substring("Bearer ".Length).Trim()
            : authHeader.Trim();

        if (!TokenStore.IsValidToken(token))
        {
            _logger.LogWarning("Request to {Path} with invalid token: {Token}", path, token);
            await HandleUnauthorizedAsync(context);
            return;
        }

        // Attach user context to HttpContext.Items for downstream use
        context.Items["User"] = new { Token = token, AuthenticatedAt = DateTime.UtcNow };
        _logger.LogInformation("Request to {Path} authenticated successfully", path);
        await _next(context);
    }

    private static Task HandleUnauthorizedAsync(HttpContext context)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;

        var response = new
        {
            error = "Unauthorized",
            message = "Invalid or missing token. Use /auth/login to get a valid token."
        };

        return context.Response.WriteAsJsonAsync(response);
    }
}
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var response = new
        {
            error = "Internal server error",
            message = exception.Message
        };

        return context.Response.WriteAsJsonAsync(response);
    }
}

// Logging middleware
public class LoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LoggingMiddleware> _logger;

    public LoggingMiddleware(RequestDelegate next, ILogger<LoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path;
        var startTime = DateTime.UtcNow;

        try
        {
            await _next(context);

            var statusCode = context.Response.StatusCode;
            var duration = DateTime.UtcNow - startTime;

            _logger.LogInformation(
                "HTTP {Method} {Path} returned {StatusCode} in {DurationMs}ms",
                method,
                path,
                statusCode,
                duration.TotalMilliseconds
            );
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(
                ex,
                "HTTP {Method} {Path} failed with exception after {DurationMs}ms",
                method,
                path,
                duration.TotalMilliseconds
            );
            throw;
        }
    }
}
