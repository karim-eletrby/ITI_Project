using Domain.Common;


namespace Application.Common
{
    public class Result<T>
    {
        public bool IsSuccess { get; private set; }
        public string Message { get; private set; } = string.Empty;
        public T? Data { get; private set; }
        public List<string> Errors { get; private set; } = new();

        public static Result<T> Success(T data, string message = "Success")
            => new()
            {
                IsSuccess = true,
                Data = data,
                Message = message
            };

        public static Result<T> Failure(string message, IEnumerable<string>? errors = null)
            => new()
            {
                IsSuccess = false,
                Message = message,
                Errors = errors?.ToList() ?? new List<string>()
            };

        public static Result<T> Failure(string message, string error)
            => Failure(message, new[] { error });

        public SuccessResponse<T> ToSuccessResponse()
            => SuccessResponse<T>.Create(Data!, Message);

        public ErrorResponse ToErrorResponse()
            => ErrorResponse.Create(Message, Errors);
    }
}
