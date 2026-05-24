namespace H2CursorRouter.H2;

public sealed record H2CommandResult(bool IsSuccess, string Message, string? RequestJson, string? ResponseJson)
{
    public static H2CommandResult Success(string requestJson, string responseJson) =>
        new(true, "H2 command succeeded.", requestJson, responseJson);

    public static H2CommandResult Failure(string message, string? requestJson = null, string? responseJson = null) =>
        new(false, message, requestJson, responseJson);
}
