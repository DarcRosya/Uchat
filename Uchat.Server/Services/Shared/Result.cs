using System;

namespace Uchat.Server.Services.Shared;

public class Result
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }

    protected Result(bool isSuccess, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public static Result Success() => new(true, null);

    public static Result Failure(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Error message must be provided.", nameof(errorMessage));
        }

        return new Result(false, errorMessage);
    }

    public override string ToString() => IsSuccess ? "Success" : $"Failure: {ErrorMessage}";
}

public sealed class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool isSuccess, T? value, string? errorMessage)
        : base(isSuccess, errorMessage)
    {
        Value = value;
    }

    public static Result<T> Success(T value)
        => new(true, value, null);

    public new static Result<T> Failure(string errorMessage)
        => new(false, default, errorMessage);
}
