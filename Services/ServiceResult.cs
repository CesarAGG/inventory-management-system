using System.Collections.Generic;

namespace InventoryManagementSystem.Services;

public enum ServiceErrorType
{
    None,
    NotFound,
    Forbidden,
    InvalidInput,
    Concurrency,
    General
}

public class ServiceResult<T>
{
    public T? Data { get; private set; }
    public bool IsSuccess => ErrorType == ServiceErrorType.None;
    public ServiceErrorType ErrorType { get; private set; } = ServiceErrorType.None;
    public string? ErrorMessage { get; private set; }
    public Dictionary<string, string>? ValidationErrors { get; private set; }

    public static ServiceResult<T> Success(T data) => new() { Data = data };
    public static ServiceResult<T> FromError(ServiceErrorType type, string message) => new() { ErrorType = type, ErrorMessage = message };
    public static ServiceResult<T> FromValidationErrors(Dictionary<string, string> errors) => new() { ErrorType = ServiceErrorType.InvalidInput, ValidationErrors = errors };
}