using Microsoft.Extensions.Configuration;

namespace TestDoubles.Helpers;

public static class ConfigurationTestFactory
{
    public static IConfiguration CreateConfiguration(Dictionary<string, string?> settings)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }
}
