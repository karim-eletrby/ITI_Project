
namespace Domain.Common
{
    public class SuccessResponse<T>
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "Success";
        public T? Data { get; set; }

        public SuccessResponse()
        {
        }

        public SuccessResponse(T data, string message = "Success")
        {
            Data = data;
            Message = message;
        }

        public static SuccessResponse<T> Create(T data, string message = "Success")
            => new(data, message);
    }
}
