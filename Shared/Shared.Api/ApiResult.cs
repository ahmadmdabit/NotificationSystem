namespace Shared.Helpers;

/// <summary>
/// Standard response envelope for every HTTP endpoint in the system.
/// Lives in Shared.Api (presentation/contract kernel) so the MVC UI can use it
/// without taking a dependency on the Application layer.
/// </summary>
public class ApiResult<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }

    public ErrorResult? Error { get; set; }

    public ApiResult(bool success, T? data)
    {
        this.Success = success;
        this.Data = data;
    }

    public ApiResult(bool success, T? data, ErrorResult? error)
    {
        this.Success = success;
        this.Data = data;
        this.Error = error;
    }

    public ApiResult(T data, ErrorResult error)
    {
        this.Success = false;
        this.Data = data;
        this.Error = error;
    }

    public ApiResult(bool success, T? data, int errorCode, string errorMessage)
    {
        this.Success = success;
        this.Data = data;
        this.Error = new ErrorResult(errorCode, errorMessage);
    }
}
