namespace H2CursorRouter.H2;

public sealed record H2AckResult(bool IsSuccess, string? Ack, string? Command, string Message)
{
    public static H2AckResult Success(string? command, string ack) =>
        new(true, ack, command, "H2 ACK succeeded.");

    public static H2AckResult Failure(string message, string? command = null, string? ack = null) =>
        new(false, ack, command, message);
}
