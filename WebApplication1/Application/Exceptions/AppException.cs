using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Exceptions
{
  
    public abstract class AppException : Exception
    {
        protected AppException(string message, IEnumerable<string>? errors = null)
            : base(message)
        {
            Errors = errors?.ToList() ?? new List<string>();
        }

        protected AppException(string message, string error)
            : this(message, new[] { error })
        {
        }

        public abstract int StatusCode { get; }
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyDictionary<string, string[]>? FieldErrors { get; protected set; }
        public object? Details { get; protected set; }
    }

    public class BadRequestException : AppException
    {
        public BadRequestException(string message, IEnumerable<string>? errors = null)
            : base(message, errors) { }

        public BadRequestException(string message, string error)
            : base(message, error) { }

        public BadRequestException(string message, IDictionary<string, string[]> fieldErrors)
            : base(message, fieldErrors.SelectMany(kvp => kvp.Value))
        {
            FieldErrors = new Dictionary<string, string[]>(fieldErrors, StringComparer.OrdinalIgnoreCase);
        }

        public override int StatusCode => 400;
    }

    public class UnauthorizedException : AppException
    {
        public UnauthorizedException(string message, IEnumerable<string>? errors = null)
            : base(message, errors) { }

        public UnauthorizedException(string message, string error)
            : base(message, error) { }

        public UnauthorizedException(string message, IDictionary<string, string[]> fieldErrors)
            : base(message, fieldErrors.SelectMany(kvp => kvp.Value))
        {
            FieldErrors = new Dictionary<string, string[]>(fieldErrors, StringComparer.OrdinalIgnoreCase);
        }

        public UnauthorizedException(
            string message,
            IDictionary<string, string[]> fieldErrors,
            object? details)
            : this(message, fieldErrors)
        {
            Details = details;
        }

        public override int StatusCode => 401;
    }

    public class ForbiddenException : AppException
    {
        public ForbiddenException(string message, IEnumerable<string>? errors = null)
            : base(message, errors) { }

        public ForbiddenException(string message, string error)
            : base(message, error) { }

        public override int StatusCode => 403;
    }

    public class NotFoundException : AppException
    {
        public NotFoundException(string message, IEnumerable<string>? errors = null)
            : base(message, errors) { }

        public NotFoundException(string message, string error)
            : base(message, error) { }

        public override int StatusCode => 404;
    }

    public class ConflictException : AppException
    {
        public ConflictException(string message, IEnumerable<string>? errors = null)
            : base(message, errors) { }

        public ConflictException(string message, string error)
            : base(message, error) { }

        public ConflictException(string message, IDictionary<string, string[]> fieldErrors)
            : base(message, fieldErrors.SelectMany(kvp => kvp.Value))
        {
            FieldErrors = new Dictionary<string, string[]>(fieldErrors, StringComparer.OrdinalIgnoreCase);
        }

        public override int StatusCode => 409;
    }
}
