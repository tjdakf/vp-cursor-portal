namespace H2CursorRouter.Core.Validation;

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Success { get; } = new(true, Array.Empty<string>());

    public static ValidationResult Failure(IEnumerable<string> errors)
    {
        var list = errors.Where(error => !string.IsNullOrWhiteSpace(error)).ToArray();
        return list.Length == 0 ? Success : new ValidationResult(false, list);
    }
}
