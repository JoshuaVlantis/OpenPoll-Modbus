namespace OpenPoll.Services;

public sealed record ModbusResult<T>(bool Success, T? Value, string? Error)
{
    public static ModbusResult<T> Ok(T value) => new(true, value, null);
    public static ModbusResult<T> Fail(string error) => new(false, default, error);
}

public sealed record ModbusResult(bool Success, string? Error)
{
    public static ModbusResult Ok() => new(true, null);
    public static ModbusResult Fail(string error) => new(false, error);
}
