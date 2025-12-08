namespace LoveLetter.App.Services;

public sealed record ServiceResult<T>(bool Success, T? Value, string? Error)
{
    public static ServiceResult<T> Ok(T value) => new(true, value, null);

    public static ServiceResult<T> Fail(string message) => new(false, default, message);
}
