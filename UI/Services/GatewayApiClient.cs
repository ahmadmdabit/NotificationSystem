using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using RestSharp;
using UI.Models;

namespace UI.Services
{
    public class GatewayApiClient : IGatewayApiClient, IDisposable
    {
        private readonly RestClient _client;
        private readonly ApiSettings _settings;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private string _token;
        private bool _disposed;

        public GatewayApiClient(IOptions<ApiSettings> options)
        {
            _settings = options.Value;
            if (string.IsNullOrWhiteSpace(_settings.GatewayBaseUrl))
                throw new InvalidOperationException("ApiSettings:GatewayBaseUrl is not configured.");
            if (string.IsNullOrWhiteSpace(_settings.ServiceUsername) || string.IsNullOrWhiteSpace(_settings.ServicePassword))
                throw new InvalidOperationException(
                    "ApiSettings:ServiceUsername and ServicePassword are required so the UI can obtain a JWT.");

            _client = new RestClient(_settings.GatewayBaseUrl);
        }

        public Task<IRestResponse> GetAsync(string path, CancellationToken cancellationToken)
            => SendAsync(new RestRequest(path, Method.GET, DataFormat.Json), cancellationToken);

        public Task<IRestResponse> PostAsync(string path, object body, CancellationToken cancellationToken)
            => SendAsync(new RestRequest(path, Method.POST, DataFormat.Json).AddJsonBody(body), cancellationToken);

        private async Task<IRestResponse> SendAsync(IRestRequest request, CancellationToken cancellationToken)
        {
            await EnsureTokenAsync(cancellationToken).ConfigureAwait(false);
            ApplyBearer(request);

            var response = await _client.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
                return response;

            await EnsureTokenAsync(cancellationToken, forceRefresh: true).ConfigureAwait(false);
            ApplyBearer(request);
            return await _client.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        }

        private void ApplyBearer(IRestRequest request)
        {
            request.AddOrUpdateParameter("Authorization", "Bearer " + _token, ParameterType.HttpHeader);
        }

        private async Task EnsureTokenAsync(CancellationToken cancellationToken, bool forceRefresh = false)
        {
            if (!forceRefresh && !string.IsNullOrEmpty(_token))
                return;

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!forceRefresh && !string.IsNullOrEmpty(_token))
                    return;

                var token = await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(token))
                {
                    await RegisterAsync(cancellationToken).ConfigureAwait(false);
                    token = await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
                }

                if (string.IsNullOrEmpty(token))
                    throw new InvalidOperationException("UI service account could not authenticate against the gateway.");

                _token = token;
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<string> AuthenticateAsync(CancellationToken cancellationToken)
        {
            var request = new RestRequest("Users/Authenticate", Method.POST, DataFormat.Json)
                .AddJsonBody(new { username = _settings.ServiceUsername, password = _settings.ServicePassword });
            var response = await _client.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            return ReadToken(response);
        }

        private async Task RegisterAsync(CancellationToken cancellationToken)
        {
            var request = new RestRequest("Users/Register", Method.POST, DataFormat.Json)
                .AddJsonBody(new { username = _settings.ServiceUsername, password = _settings.ServicePassword });
            var response = await _client.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

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

        private static bool IsUserExistsError(IRestResponse response)
        {
            try
            {
                var body = JObject.Parse(response.Content);
                var success = body.Value<bool?>("success") ?? body.Value<bool?>("Success");
                return success == false;
            }
            catch
            {
                return false;
            }
        }

        private static string ReadToken(IRestResponse response)
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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _gate.Dispose();
            // RestSharp 106.x RestClient does not implement IDisposable;
            // it holds no unmanaged resources that require explicit release.
        }
    }
}
