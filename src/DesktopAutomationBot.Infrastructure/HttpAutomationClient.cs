using System.Net.Http.Headers;
using System.Text;
using DesktopAutomationBot.Application;

namespace DesktopAutomationBot.Infrastructure;

public sealed class HttpAutomationClient : IHttpAutomationClient
{
    private static readonly HttpClient Client = new();

    public async Task<HttpAutomationResponse> SendAsync(
        HttpAutomationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(request.Timeout);

        using var message = new HttpRequestMessage(request.Method, request.Url);
        if (!string.IsNullOrEmpty(request.BearerToken))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.BearerToken);
        }

        if (request.Body is not null && request.Method != HttpMethod.Get)
        {
            message.Content = new StringContent(request.Body, Encoding.UTF8, "application/json");
        }

        using var response = await Client.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            timeoutSource.Token);
        var body = await response.Content.ReadAsStringAsync(timeoutSource.Token);

        return new HttpAutomationResponse((int)response.StatusCode, body);
    }
}
