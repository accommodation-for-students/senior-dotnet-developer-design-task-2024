namespace StudentPropertyMarketplace;

public interface IExternalApiClient
{
    string FetchExternalData(string endpoint, Dictionary<string, string> parameters);
}