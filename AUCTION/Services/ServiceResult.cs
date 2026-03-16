namespace AUCTION.Services;

public class ServiceResult<T>
{
    public bool Success { get; private set; }
    public T? Data { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }

    private ServiceResult() { }

    public static ServiceResult<T> Ok(T data, string message = "Operation successful")
        => new() { Success = true, Data = data, Message = message, StatusCode = 200 };

    public static ServiceResult<T> Created(T data, string message = "Created successfully")
        => new() { Success = true, Data = data, Message = message, StatusCode = 201 };

    public static ServiceResult<T> Fail(string message, int statusCode = 400)
        => new() { Success = false, Data = default, Message = message, StatusCode = statusCode };

    public static ServiceResult<T> Forbidden(string message = "Access denied")
        => Fail(message, 403);

    public static ServiceResult<T> NotFound(string message = "Resource not found")
        => Fail(message, 404);

    public static ServiceResult<T> Conflict(string message = "Conflict")
        => Fail(message, 409);

    public static ServiceResult<T> Unauthorized(string message = "Unauthorized")
        => Fail(message, 401);
}
