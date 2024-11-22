namespace StudentPropertyMarketplace;

public interface IPaymentProcessor
{
    string? MakePayment(string cardNumber, string cardExpiryDate, string securityCode, decimal paymentAmount, string currency = "GBP", bool isInternational = false);
}