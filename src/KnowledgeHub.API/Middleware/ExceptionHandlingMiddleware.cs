using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeHub.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
            var (statusCode, title) = ex switch
            {
                ArgumentException => (HttpStatusCode.BadRequest, "Bad Request"),
                UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized"),
                FileNotFoundException => (HttpStatusCode.NotFound, "Not Found"),
                KeyNotFoundException => (HttpStatusCode.NotFound, "Not Found"),
                InvalidOperationException => (HttpStatusCode.Conflict, "Conflict"),
                OperationCanceledException => (HttpStatusCode.BadRequest, "Request Cancelled"),
                _ => (HttpStatusCode.InternalServerError, "Internal Server Error")
            };

            _logger.LogError(
                ex,
                "Unhandled exception on {Method} {Path} — Status: {StatusCode} Type: {ExceptionType}",
                context.Request.Method,
                context.Request.Path,
                (int)statusCode,
                ex.GetType().Name);

            await HandleExceptionAsync(context, ex, statusCode, title);
        }
    }

    private static async Task HandleExceptionAsync(
        HttpContext context, Exception exception, HttpStatusCode statusCode, string title)
    {
        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = statusCode == HttpStatusCode.InternalServerError
                ? "An unexpected error occurred. Please try again later."
                : exception.Message,
            Instance = context.Request.Path,
            Type = $"https://httpstatuses.com/{(int)statusCode}"
        };

        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, options));
    }
}
