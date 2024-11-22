namespace StudentPropertyMarketplace;

public class Property
{
    public int PropertyId { get; set; }
    public string Address { get; set; }
    public string City { get; set; }
    public string Postcode { get; set; }
    public string LandlordName { get; set; }
    public string LandlordEmail { get; set; }
    public decimal MonthlyRent { get; set; }
    public int Bedrooms { get; set; }
}