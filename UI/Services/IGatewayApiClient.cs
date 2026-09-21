using System.Threading;
using System.Threading.Tasks;
using RestSharp;

namespace UI.Services
{
    public interface IGatewayApiClient
    {
        Task<IRestResponse> GetAsync(string path, CancellationToken cancellationToken);
        Task<IRestResponse> PostAsync(string path, object body, CancellationToken cancellationToken);
    }
}
