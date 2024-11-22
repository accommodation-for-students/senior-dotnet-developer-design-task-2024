namespace StudentPropertyMarketplace;

public interface IPropertyManager
{
    void AddNewProperty(string address, string city, string postcode, string landlordName, string landlordEmail, decimal monthlyRent, int bedrooms);
    void SubmitTenantApplication(string applicantName, string applicantPhone, string applicantEmail, string cardNumber, string cardExpiry, decimal amountPaid);
}