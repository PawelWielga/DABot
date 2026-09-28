namespace DesktopAutomationBot.Application;

public sealed record HttpAutomationRequest(
    HttpMethod Method,
    string Url,
    string? Body,
    string? BearerToken,
    TimeSpan Timeout);

public sealed record HttpAutomationResponse(
    int StatusCode,
    string Body);

public interface IHttpAutomationClient
{
    Task<HttpAutomationResponse> SendAsync(
        HttpAutomationRequest request,
        CancellationToken cancellationToken);
}
