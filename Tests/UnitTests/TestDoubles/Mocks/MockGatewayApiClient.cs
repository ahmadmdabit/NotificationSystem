using RestSharp;

using UI.Services;

namespace TestDoubles.Mocks;

public static class MockGatewayApiClient
{
    public static IGatewayApiClientMock Create(RestResponse defaultResponse)
    {
        var mock = IGatewayApiClient.Mock();

        mock.GetAsync(Any<string>(), Any<CancellationToken>())
            .Returns(defaultResponse);

        mock.PostAsync(Any<string>(), Any<object>(), Any<CancellationToken>())
            .Returns(defaultResponse);

        return mock;
    }
}
