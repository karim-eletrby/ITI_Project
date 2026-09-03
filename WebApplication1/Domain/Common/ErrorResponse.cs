namespace Domain.Common
{
    public class ErrorResponse
    {
        public bool Success { get; set; } = false;
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
        public Dictionary<string, string[]>? FieldErrors { get; set; }
        public object? Data { get; set; }

        public ErrorResponse()
        {
        }

        public ErrorResponse(string message, IEnumerable<string>? errors = null)
        {
            Message = message;
            Errors = errors?.ToList() ?? new List<string>();
        }

        public static ErrorResponse Create(string message, IEnumerable<string>? errors = null)
            => new(message, errors);

        public static ErrorResponse Create(string message, string error)
            => new(message, new[] { error });
    }
}
