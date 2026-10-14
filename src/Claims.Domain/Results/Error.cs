namespace Claims.Domain.Results;

public enum ErrorType
{
    Validation = 0,

    NotFound = 1,

    Conflict = 2,
}

public sealed record Error(string Code, string Message, ErrorType Type, string? Target = null)
{
    public static Error Validation(string code, string message, string? target = null) =>
        new(code, message, ErrorType.Validation, target);

    public static Error NotFound(string code, string message) =>
        new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) =>
        new(code, message, ErrorType.Conflict);
}
