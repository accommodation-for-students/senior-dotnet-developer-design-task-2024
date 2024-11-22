using System.Globalization;
using System.Text;

namespace StudentPropertyMarketplace
{
    public class StudentService : IPropertyManager, INotificationHandler, IPaymentProcessor, IPropertyReportGenerator
    {
        private readonly IEmailSender _emailSender;
        private readonly IDatabaseLogger _databaseLogger;
        private readonly IAnalyticsService _analyticsService;
        private readonly IFileStorage _fileStorage;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IConfigurationReader _configurationReader;
        private readonly IExternalApiClient _externalApiClient;
        private readonly ISmsNotificationService _smsNotificationService;
        private readonly StudentPropertyDbContext _dbContext;

        public StudentService(IEmailSender emailSender, IDatabaseLogger databaseLogger,
            IAnalyticsService analyticsService, IFileStorage fileStorage, IDateTimeProvider dateTimeProvider,
            IConfigurationReader configurationReader, IExternalApiClient externalApiClient,
            ISmsNotificationService smsNotificationService, StudentPropertyDbContext dbContext)
        {
            _emailSender = emailSender;
            _databaseLogger = databaseLogger;
            _analyticsService = analyticsService;
            _fileStorage = fileStorage;
            _dateTimeProvider = dateTimeProvider;
            _configurationReader = configurationReader;
            _externalApiClient = externalApiClient;
            _smsNotificationService = smsNotificationService;
            _dbContext = dbContext;
        }

        public void AddNewProperty(string address, string city, string postcode, string landlordName, string landlordEmail, decimal monthlyRent, int bedrooms)
        {
            var property = new Property
            {
                Address = address,
                City = city,
                Postcode = postcode,
                LandlordName = landlordName,
                LandlordEmail = landlordEmail,
                MonthlyRent = monthlyRent,
                Bedrooms = bedrooms
            };

            _dbContext.Properties.Add(property);
            _dbContext.SaveChanges();

            _databaseLogger.LogToDatabase("New property added: " + property.Address);

            if (!string.IsNullOrWhiteSpace(property.LandlordEmail))
            {
                _emailSender.SendEmail(property.LandlordEmail, "Property Added", $"Your property at {property.Address} has been successfully added to the system.");
            }

            var metrics = _externalApiClient.FetchExternalData("/property/metrics", new Dictionary<string, string> { { "PropertyId", property.PropertyId.ToString() } });
            _analyticsService.TrackEvent("PropertyMetricsUpdated", new Dictionary<string, string> { { "Metrics", metrics }, { "Address", property.Address } });

            if (_configurationReader.GetConfigurationValue("EnablePropertyValidation") == "true")
            {
                var validationResult = _externalApiClient
                    .FetchExternalData("/validate/property", new Dictionary<string, string>
                    {
                        { "Address", property.Address },
                        { "Postcode", property.Postcode }
                    });

                if (validationResult.Contains("error"))
                {
                    _databaseLogger.LogToDatabase("Property validation failed for: " + property.Address);
                    throw new InvalidOperationException("Property validation failed.");
                }
            }

            _analyticsService.TrackEvent("PropertyAddedDetailed", new Dictionary<string, string>
            {
                { "City", property.City },
                { "Postcode", property.Postcode },
                { "Landlord", property.LandlordName },
                { "Rent", property.MonthlyRent.ToString(CultureInfo.InvariantCulture) }
            });

            var convertedRent = _configurationReader
                .GetConfigurationValue("CurrencyConversion") == "true"
                ? _externalApiClient
                    .FetchExternalData("/convert/currency", new Dictionary<string, string>
                    {
                        { "Amount", property.MonthlyRent.ToString(CultureInfo.InvariantCulture) },
                        { "Currency", "USD" }
                    })
                : property.MonthlyRent.ToString(CultureInfo.InvariantCulture);

            _databaseLogger.LogToDatabase("Converted Rent: " + convertedRent);

            var archiveContent = $"Property: {property.Address}\nCity: {property.City}\nRent: {property.MonthlyRent}\nLandlord: {property.LandlordName}";
            _fileStorage.SaveFile($"property_{property.PropertyId}_archive.txt", Encoding.UTF8.GetBytes(archiveContent));
            _databaseLogger.LogToDatabase("Archived property data for ID: " + property.PropertyId);
        }

        public void SubmitTenantApplication(string applicantName, string applicantPhone, string applicantEmail, string cardNumber, string cardExpiry, decimal amountPaid)
        {
            if (string.IsNullOrWhiteSpace(applicantName) || string.IsNullOrWhiteSpace(applicantPhone) || string.IsNullOrWhiteSpace(applicantEmail))
            {
                _databaseLogger.LogToDatabase("Invalid applicant details provided.");
                throw new ArgumentException("Applicant details cannot be null or empty.");
            }

            if (string.IsNullOrWhiteSpace(cardNumber) || string.IsNullOrWhiteSpace(cardExpiry) || amountPaid <= 0)
            {
                _databaseLogger.LogToDatabase("Invalid payment details for applicant: " + applicantEmail);
                throw new ArgumentException("Invalid payment details.");
            }

            if (applicantPhone.Length < 10 || !applicantPhone.All(char.IsDigit))
            {
                _databaseLogger.LogToDatabase("Invalid phone number for applicant: " + applicantEmail);
                throw new ArgumentException("Invalid phone number.");
            }

            var transactionId = MakePayment(cardNumber, cardExpiry, "123", amountPaid);
            if (string.IsNullOrEmpty(transactionId))
            {
                _databaseLogger.LogToDatabase("Payment failed for applicant: " + applicantEmail);
                return;
            }

            var application = new Application
            {
                ApplicantName = applicantName,
                ApplicantPhone = applicantPhone,
                ApplicantEmail = applicantEmail,
                CardNumber = cardNumber,
                CardExpiryDate = cardExpiry,
                AmountPaid = amountPaid
            };

            if (_dbContext.Applications.Where(a => a.ApplicantEmail == applicantEmail).ToList().Any())
            {
                _databaseLogger.LogToDatabase("Duplicate application detected for applicant: " + applicantEmail);
                throw new InvalidOperationException("Duplicate application not allowed.");
            }

            _dbContext.Applications.Add(application);
            _dbContext.SaveChanges();

            _databaseLogger.LogToDatabase("Application received for applicant: " + applicantName);

            SendNotification(applicantEmail, "Application Submitted", $"Dear {applicantName}, your application has been successfully submitted.");

            var statusMessage = $"Dear {application.ApplicantName},\n\nThank you for your application for the property. Your application ID is {application.ApplicationId}.\n\nWe will review your submission and contact you shortly.";

            _smsNotificationService.SendSms(application.ApplicantPhone, statusMessage);
            _databaseLogger.LogToDatabase("Sent application status notification to: " + application.ApplicantPhone);

            var archiveContent = $"Applicant: {application.ApplicantName}\nEmail: {application.ApplicantEmail}\nPhone: {application.ApplicantPhone}\nAmount Paid: {application.AmountPaid}";

            _fileStorage.SaveFile($"application_{application.ApplicationId}_archive.txt", Encoding.UTF8.GetBytes(archiveContent));
            _databaseLogger.LogToDatabase("Archived application data for ID: " + application.ApplicationId);

            _analyticsService.TrackEvent("TenantApplicationSubmitted", new Dictionary<string, string>
            {
                { "ApplicantName", applicantName },
                { "ApplicantEmail", applicantEmail },
                { "AmountPaid", amountPaid.ToString(CultureInfo.InvariantCulture) }
            });

            Console.WriteLine("Application processing complete for applicant: " + applicantName);
        }

        public void SendNotification(string recipientEmail, string messageTitle, string messageBody)
        {
            _emailSender.SendEmail(recipientEmail, messageTitle, messageBody);
        }

        public string MakePayment(string cardNumber, string cardExpiryDate, string securityCode, decimal paymentAmount, string currency = "GBP", bool isInternational = false)
        {
            if (string.IsNullOrWhiteSpace(cardNumber) || string.IsNullOrWhiteSpace(cardExpiryDate) || paymentAmount <= 0)
            {
                _databaseLogger.LogToDatabase("Invalid payment details.");
                throw new ArgumentException("Invalid payment details.");
            }

            decimal discount = 0;
            if (paymentAmount > 1000)
            {
                if (currency == "USD")
                {
                    discount = paymentAmount * 0.05m;
                }
                else
                {
                    if (currency == "EUR")
                    {
                        discount = paymentAmount * 0.04m;
                    }
                    else
                    {
                        if (currency == "GBP")
                        {
                            discount = paymentAmount * 0.03m;
                        }
                    }
                }
            }
            else if (paymentAmount > 500)
            {
                if (currency == "USD")
                {
                    discount = paymentAmount * 0.02m;
                }
                else
                {
                    if (currency == "AUD")
                    {
                        discount = paymentAmount * 0.015m;
                    }
                }
            }

            var finalAmount = paymentAmount - discount;
            _databaseLogger.LogToDatabase($"Applying discount of {discount}. Final amount: {finalAmount}");

            string paymentGateway;
            if (isInternational)
            {
                if (currency != "USD")
                {
                    paymentGateway = "InternationalGateway";
                    if (currency == "EUR" || currency == "GBP")
                    {
                        _databaseLogger.LogToDatabase("Converting payment to USD for international transaction.");
                        var conversionRates = new Dictionary<string, decimal>
                        {
                            { "EUR", 1.1m },
                            { "GBP", 1.3m },
                            { "AUD", 0.7m },
                            { "INR", 0.012m }
                        };

                        if (!conversionRates.TryGetValue(currency, out var rate))
                        {
                            _databaseLogger.LogToDatabase("Unsupported currency: " + currency);
                            throw new ArgumentException("Unsupported currency.");
                        }

                        finalAmount = finalAmount * rate;
                    }
                    else
                    {
                        if (currency == "INR")
                        {
                            _databaseLogger.LogToDatabase("INR transactions incur additional conversion fees.");
                            finalAmount += 10;
                        }
                    }
                }
                else
                {
                    paymentGateway = "USDGateway";
                }
            }
            else
            {
                paymentGateway = "DomesticGateway";
            }

            _databaseLogger.LogToDatabase($"Processing payment through {paymentGateway}.");

            return Guid.NewGuid().ToString();
        }

        public void GenerateTenantReport(string propertyId, string tenantEmail)
        {
            if (string.IsNullOrWhiteSpace(propertyId) || string.IsNullOrWhiteSpace(tenantEmail))
            {
                Console.WriteLine("Invalid tenant report request.");
                return;
            }

            var tenantReportContent = $"Tenant report for property {propertyId} and tenant {tenantEmail}";
            _fileStorage.SaveFile("tenant_report.txt", Encoding.UTF8.GetBytes(tenantReportContent));
            _analyticsService.TrackEvent("TenantReportGenerated",
                new Dictionary<string, string> { { "PropertyId", propertyId }, { "TenantEmail", tenantEmail } });
        }

        public void GeneratePaymentSummaryReport(string fromDate, string toDate)
        {
            if (string.IsNullOrWhiteSpace(fromDate) || string.IsNullOrWhiteSpace(toDate))
            {
                Console.WriteLine("Invalid date range for payment summary report.");
                return;
            }

            var paymentSummaryContent = $"Payment summary report from {fromDate} to {toDate}";
            _fileStorage.SaveFile("payment_summary_report.txt", Encoding.UTF8.GetBytes(paymentSummaryContent));
        }
    }
}