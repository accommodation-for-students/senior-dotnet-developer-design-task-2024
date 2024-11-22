namespace StudentPropertyMarketplace;

public interface IAnalyticsService
{
    void TrackEvent(string eventName, Dictionary<string, string> properties);
}