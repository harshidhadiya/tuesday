namespace USER.Services
{
  
    public class ServiceResult<T>
    {
        public bool Success { get; private set; }
        public T? Data { get; private set; }
        public string Message { get; private set; } = string.Empty;
        public int StatusCode { get; private set; }

        private ServiceResult() { }

        public static ServiceResult<T> Ok(T data, string message = "Operation successful")
        {
            return new ServiceResult<T>
            {
                Success = true,
                Data = data,
                Message = message,
                StatusCode = 200
            };
        }

        public static ServiceResult<T> Fail(string message, int statusCode = 400)
        {
            return new ServiceResult<T>
            {
                Success = false,
                Data = default,
                Message = message,
                StatusCode = statusCode
            };
        }

        public static ServiceResult<T> Forbidden(string message = "Access denied")
        {
            return Fail(message, 403);
        }
        public static ServiceResult<T> NotFound(string message = "Resource not found")
        {
            return Fail(message, 404);
        }
    }
}
