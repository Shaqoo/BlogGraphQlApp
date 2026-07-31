namespace BlogGraphQlApp.Common
{
    public class ApiResponse<T>
    {
        public bool Succeeded { get; private set; }
        public T? Data { get; private set; }
        public string? Message { get; private set; }
        public List<string> Errors { get; private set; } = [];

        public static ApiResponse<T> Success(T data, string? message = "Operation completed successfully.")
        {
            return new ApiResponse<T> { Succeeded = true, Data = data, Message = message };
        }

        public static ApiResponse<T> Fail(string errorMessage, List<string>? errors = null)
        {
            return new ApiResponse<T> { Succeeded = false, Message = errorMessage, Errors = errors ?? [errorMessage] };
        }

        public static ApiResponse<T> Fail(string errorMessage)
        {
            return new ApiResponse<T> { Succeeded = false, Message = errorMessage, Errors = [errorMessage] };
        }
    }
}