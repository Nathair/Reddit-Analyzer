using PhoneNumbers;

namespace PhoneDb.Api.Services
{
    public record PhoneProcessingResult(bool Success, string FormattedNumber = null, int CountryCode = 0, string Region = null, string Error = null);

    public interface IPhoneService
    {
        PhoneProcessingResult ValidateAndNormalize(string rawNumber, string defaultRegion = "US");
    }

    public class PhoneService : IPhoneService
    {
        private readonly ILogger<PhoneService> _logger;
        private static readonly PhoneNumberUtil PhoneUtil = PhoneNumberUtil.GetInstance();

        public PhoneService(ILogger<PhoneService> logger)
        {
            _logger = logger;
        }

        public PhoneProcessingResult ValidateAndNormalize(string rawNumber, string defaultRegion = "US")
        {
            _logger.LogInformation("Processing phone number validation request for: {RawNumber}", rawNumber);

            var sanitized = rawNumber?
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("(", "")
                .Replace(")", "")
                .Trim();

            if (string.IsNullOrWhiteSpace(sanitized))
            {
                _logger.LogWarning("Validation failed: number is null or whitespace.");
                return new PhoneProcessingResult(false, Error: "Phone number is required.");
            }

            try
            {
                var phoneNumber = PhoneUtil.Parse(sanitized, defaultRegion);
                if (!PhoneUtil.IsValidNumber(phoneNumber))
                {
                    _logger.LogWarning("Invalid phone number pattern for region {Region}: {Number}", defaultRegion, sanitized);
                    return new PhoneProcessingResult(false, Error: "Invalid phone number.");
                }

                var formatted = PhoneUtil.Format(phoneNumber, PhoneNumberFormat.E164);
                var countryCode = phoneNumber.CountryCode;
                var region = PhoneUtil.GetRegionCodeForNumber(phoneNumber);

                _logger.LogInformation("Successfully validated and formatted number: {Formatted} (Country: {Region}, Code: {Code})", 
                    formatted, region, countryCode);

                return new PhoneProcessingResult(Success: true,
                    FormattedNumber: formatted,
                    CountryCode: countryCode,
                    Region: region);
            }
            catch (NumberParseException ex)
            {
                _logger.LogError(ex, "Failed to parse phone number: {Number}", sanitized);
                return new PhoneProcessingResult(false, Error: "Invalid phone number format.");
            }
        }
    }
}
