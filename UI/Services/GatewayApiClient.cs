using System.Net;

using Microsoft.Extensions.Options;

using Newtonsoft.Json.Linq;

using RestSharp;

using UI.Models;

namespace UI.Services;

public class GatewayApiClient : IGatewayApiClient
{
    private readonly RestClient client;
    private readonly ApiSettings settings;
    private readonly SemaphoreSlim gate = new(1, 1);
    private string? token;
    private DateTime tokenExpiry = DateTime.MinValue;
    private bool disposedValue;

    public GatewayApiClient(IOptions<ApiSettings> options)
    {
        settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.GatewayBaseUrl))
        {
            throw new InvalidOperationException("ApiSettings:GatewayBaseUrl is not configured.");
        }
        if (string.IsNullOrWhiteSpace(settings.ServiceUsername) || string.IsNullOrWhiteSpace(settings.ServicePassword))
        {
            throw new InvalidOperationException(
                "ApiSettings:ServiceUsername and ServicePassword are required so the UI can obtain a JWT.");
        }
        client = new RestClient(settings.GatewayBaseUrl);
    }

    public Task<RestResponse> GetAsync(string path, CancellationToken cancellationToken)
        => SendAsync(new RestRequest(path, Method.Get), cancellationToken);

    public Task<RestResponse> PostAsync(string path, object body, CancellationToken cancellationToken)
        => SendAsync(new RestRequest(path, Method.Post).AddJsonBody(body), cancellationToken);

    private async Task<RestResponse> SendAsync(RestRequest request, CancellationToken cancellationToken)
    {
        await EnsureTokenAsync(cancellationToken).ConfigureAwait(false);
        ApplyBearer(request);

        var response = await client.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        await EnsureTokenAsync(cancellationToken, forceRefresh: true).ConfigureAwait(false);
        ApplyBearer(request);
        return await client.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private void ApplyBearer(RestRequest request)
    {
        request.AddOrUpdateParameter("Authorization", $"Bearer {token}", ParameterType.HttpHeader);
    }

    private async Task EnsureTokenAsync(CancellationToken cancellationToken, bool forceRefresh = false)
    {
        // Fast path — token valid and not a forced refresh
        if (!forceRefresh && !string.IsNullOrEmpty(token) && DateTime.UtcNow < tokenExpiry)
            return;

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check inside lock
            if (!forceRefresh && !string.IsNullOrEmpty(this.token) && DateTime.UtcNow < tokenExpiry)
                return;

            var token = await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(token))
            {
                await RegisterAsync(cancellationToken).ConfigureAwait(false);
                token = await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
            }

            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("UI service account could not authenticate against the gateway.");

            this.token = token;
            // Assume 7-day expiry matching UserService.TokenGenerate; refresh slightly earlier
            tokenExpiry = DateTime.UtcNow.AddDays(6);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<string?> AuthenticateAsync(CancellationToken cancellationToken)
    {
        var request = new RestRequest("Users/Authenticate", Method.Post)
            .AddJsonBody(new { username = settings.ServiceUsername, password = settings.ServicePassword });
        var response = await client.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        return ReadToken(response);
    }

    private async Task RegisterAsync(CancellationToken cancellationToken)
    {
        var request = new RestRequest("Users/Register", Method.Post)
            .AddJsonBody(new { username = settings.ServiceUsername, password = settings.ServicePassword });
        var response = await client.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

        // S3/B2: Don't silently swallow registration failures.
        // A 400 with success=false means "user already exists" — that's fine (idempotent).
        // Any other non-OK status is a real error and should surface.
        if (response.StatusCode == HttpStatusCode.OK)
            return;

        if (response.StatusCode == HttpStatusCode.BadRequest && IsUserExistsError(response))
            return;

        throw new InvalidOperationException(
            $"UI service account registration failed: {(int)response.StatusCode} {response.StatusDescription}. " +
            $"Response: {response.Content}");
    }

    private static bool IsUserExistsError(RestResponse response)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(response.Content)) return false;

            var body = JObject.Parse(response.Content);
            var success = body.Value<bool?>("success") ?? body.Value<bool?>("Success");
            return success == false;
        }
        catch
        {
            return false;
        }
    }

    private static string? ReadToken(RestResponse response)
    {
        if (response == null || string.IsNullOrWhiteSpace(response.Content))
            return null;
        if (response.StatusCode != HttpStatusCode.OK)
            return null;

        try
        {
            var body = JObject.Parse(response.Content);
            var success = body.Value<bool?>("success") ?? body.Value<bool?>("Success");
            if (success != true)
                return null;

            return (body.SelectToken("data.token") ?? body.SelectToken("Data.Token"))?.ToString();
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return null;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue && disposing)
        {
            gate.Dispose();
        }
        disposedValue = true;
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
