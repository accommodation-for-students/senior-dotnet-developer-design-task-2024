namespace StudentPropertyMarketplace;

public interface INotificationHandler
{
    void SendNotification(string recipientEmail, string messageTitle, string messageBody);
}