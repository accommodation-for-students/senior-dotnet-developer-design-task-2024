namespace StudentPropertyMarketplace;

public interface ISmsNotificationService
{
    void SendSms(string phoneNumber, string message);
}