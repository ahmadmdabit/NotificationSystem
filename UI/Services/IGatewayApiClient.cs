using RestSharp;

namespace UI.Services;

public interface IGatewayApiClient : IDisposable
{
    Task<RestResponse> GetAsync(string path, CancellationToken cancellationToken);
    Task<RestResponse> PostAsync(string path, object body, CancellationToken cancellationToken);
}
