using Domain.Common;

namespace Domain.Errors
{
    public static class AddressErrors
    {
        // =========================================================================
        // ===== Validation Errors ==================================================
        // =========================================================================

        public static Error CountryCannotBeEmpty()
            => Error.Validation(
                "Address.CountryCannotBeEmpty",
                "Country cannot be empty.");

        public static Error CityCannotBeEmpty()
            => Error.Validation(
                "Address.CityCannotBeEmpty",
                "City cannot be empty.");

        public static Error StreetCannotBeEmpty()
            => Error.Validation(
                "Address.StreetCannotBeEmpty",
                "Street cannot be empty.");

        public static Error PostalCodeInvalid(string postalCode)
            => Error.Validation(
                "Address.PostalCodeInvalid",
                $"Postal code '{postalCode}' is invalid.");

        public static Error CountryTooLong(int maxLength)
            => Error.Validation(
                "Address.CountryTooLong",
                $"Country name cannot exceed {maxLength} characters.");

        public static Error CityTooLong(int maxLength)
            => Error.Validation(
                "Address.CityTooLong",
                $"City name cannot exceed {maxLength} characters.");

        public static Error StreetTooLong(int maxLength)
            => Error.Validation(
                "Address.StreetTooLong",
                $"Street name cannot exceed {maxLength} characters.");

        public static Error InvalidLatitude(double latitude)
            => Error.Validation(
                "Address.InvalidLatitude",
                $"Latitude {latitude} is invalid. Must be between -90 and 90.");

        public static Error InvalidLongitude(double longitude)
            => Error.Validation(
                "Address.InvalidLongitude",
                $"Longitude {longitude} is invalid. Must be between -180 and 180.");
    }
}