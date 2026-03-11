namespace ADMIN.Services
{
    public class ServiceResult<T>
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }

        public static ServiceResult<T> Ok(T data, string message = "") => new() { Success = true, StatusCode = 200, Data = data, Message = message };
        public static ServiceResult<T> NotFound(string message = "Not found") => new() { Success = false, StatusCode = 404, Message = message };
        public static ServiceResult<T> BadRequest(string message = "Bad request") => new() { Success = false, StatusCode = 400, Message = message };
        public static ServiceResult<T> Forbid(string message = "Forbidden") => new() { Success = false, StatusCode = 403, Message = message };
        public static ServiceResult<T> Error(string message, int statusCode = 500, List<string>? errors = null) => new() { Success = false, StatusCode = statusCode, Message = message, Errors = errors };
    }
}
