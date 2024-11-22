namespace StudentPropertyMarketplace;

public interface IPropertyReportGenerator
{
    void GenerateTenantReport(string propertyId, string tenantEmail);
    void GeneratePaymentSummaryReport(string fromDate, string toDate);
}