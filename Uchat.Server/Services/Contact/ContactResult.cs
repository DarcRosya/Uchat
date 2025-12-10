namespace Uchat.Server.Services.Contact;

public class ServiceResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int Data { get; internal set; }

    public static ServiceResult SuccessResult() => new() { Success = true };
    public static ServiceResult Failure(string error) => new() { Success = false, Message = error };
}

public class ServiceResult<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }

    public static ServiceResult<T> SuccessResult(T? data = default) => new() { Success = true, Data = data };
    public static ServiceResult<T> Failure(string error) => new() { Success = false, Message = error };
}