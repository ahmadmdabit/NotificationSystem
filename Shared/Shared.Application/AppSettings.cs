namespace Shared.Helpers;

public class AppSettings
{
    public string Secret { get; set; } = null!;
    public string SqlConnectionString { get; set; } = null!;
    public string ServiceAccountUsername { get; set; } = "uiservice";
}
