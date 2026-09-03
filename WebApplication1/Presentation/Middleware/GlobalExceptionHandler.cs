using Application.Exceptions;
using Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using System.Net;
using System.Text.Json;

namespace Presentation.Middleware
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            var (statusCode, errorResponse) = exception switch
            {
                AppException appEx => (
                    appEx.StatusCode,
                    CreateErrorResponse(appEx)
                ),

                UnauthorizedAccessException => (
                    (int)HttpStatusCode.Unauthorized,
                    ErrorResponse.Create("Unauthorized access.", "UNAUTHORIZED")
                ),

                Microsoft.AspNetCore.Http.BadHttpRequestException badRequestEx => (
                    StatusCodes.Status413PayloadTooLarge,
                    ErrorResponse.Create(
                        badRequestEx.Message.Contains("body", StringComparison.OrdinalIgnoreCase)
                            ? "File is too large. Maximum size is 1000 MB."
                            : badRequestEx.Message)
                ),

                _ => (
                    (int)HttpStatusCode.InternalServerError,
                    ErrorResponse.Create("Something went wrong. Please try again later.")
                )
            };

            if (statusCode >= 500)
            {
                _logger.LogError(exception, "Unhandled server error occurred: {Message}", exception.Message);
            }
            else if (statusCode == StatusCodes.Status401Unauthorized)
            {
                _logger.LogDebug("Authentication failed: {Message}", exception.Message);
            }
            else
            {
                _logger.LogWarning("Handled application exception ({StatusCode}): {Message}", statusCode, exception.Message);
            }

            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "application/json";

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            await httpContext.Response.WriteAsJsonAsync(errorResponse, jsonOptions, cancellationToken);

            return true; // Signals that this exception has been completely handled
        }

        private static ErrorResponse CreateErrorResponse(AppException appEx)
        {
            var response = ErrorResponse.Create(
                appEx.Message,
                appEx.Errors.Any() ? appEx.Errors : null);

            if (appEx.FieldErrors is { Count: > 0 })
            {
                response.FieldErrors = appEx.FieldErrors
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
            }

            if (appEx.Details is not null)
                response.Data = appEx.Details;

            return response;
        }
    }
}
