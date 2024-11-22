namespace StudentPropertyMarketplace;

public interface IConfigurationReader
{
    string GetConfigurationValue(string key);
}