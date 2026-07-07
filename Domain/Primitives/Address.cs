using Domain.Common;
using Domain.Errors;

namespace Domain.Primitives
{
    public class Address : ValueObjectBase
    {

        private const int MAX_COUNTRY_LENGTH = 100;
        private const int MAX_CITY_LENGTH = 100;
        private const int MAX_STREET_LENGTH = 200;
        private const int MAX_POSTAL_CODE_LENGTH = 20;


        public string Country { get; }
        public string City { get; }
        public string Street { get; }
        public string? PostalCode { get; }
        public double? Latitude { get; }
        public double? Longitude { get; }


        private Address(
            string country,
            string city,
            string street,
            string? postalCode = null,
            double? latitude = null,
            double? longitude = null)
        {
            Country = country;
            City = city;
            Street = street;
            PostalCode = postalCode;
            Latitude = latitude;
            Longitude = longitude;
        }


        public static Result<Address> Create(
            string country,
            string city,
            string street,
            string? postalCode = null,
            double? latitude = null,
            double? longitude = null)
        {
            // Validate Country
            if (string.IsNullOrWhiteSpace(country))
                return Result<Address>.Failure(AddressErrors.CountryCannotBeEmpty());

            if (country.Length > MAX_COUNTRY_LENGTH)
                return Result<Address>.Failure(AddressErrors.CountryTooLong(MAX_COUNTRY_LENGTH));

            // Validate City
            if (string.IsNullOrWhiteSpace(city))
                return Result<Address>.Failure(AddressErrors.CityCannotBeEmpty());

            if (city.Length > MAX_CITY_LENGTH)
                return Result<Address>.Failure(AddressErrors.CityTooLong(MAX_CITY_LENGTH));

            // Validate Street
            if (string.IsNullOrWhiteSpace(street))
                return Result<Address>.Failure(AddressErrors.StreetCannotBeEmpty());

            if (street.Length > MAX_STREET_LENGTH)
                return Result<Address>.Failure(AddressErrors.StreetTooLong(MAX_STREET_LENGTH));

            // Validate PostalCode (Optional)
            if (!string.IsNullOrWhiteSpace(postalCode) && postalCode.Length > MAX_POSTAL_CODE_LENGTH)
                return Result<Address>.Failure(AddressErrors.PostalCodeInvalid(postalCode));

            if (latitude.HasValue && (latitude < -90 || latitude > 90))
                return Result<Address>.Failure(AddressErrors.InvalidLatitude(latitude.Value));

            if (longitude.HasValue && (longitude < -180 || longitude > 180))
                return Result<Address>.Failure(AddressErrors.InvalidLongitude(longitude.Value));

            return Result<Address>.Success(new Address(
                country.Trim(),
                city.Trim(),
                street.Trim(),
                postalCode?.Trim(),
                latitude,
                longitude));
        }


        public bool IsEmpty()
        {
            return string.IsNullOrWhiteSpace(Country) &&
                   string.IsNullOrWhiteSpace(City) &&
                   string.IsNullOrWhiteSpace(Street);
        }

        public string GetFullAddress()
        {
            var parts = new List<string> { Street, City, Country };
            if (!string.IsNullOrWhiteSpace(PostalCode))
                parts.Add(PostalCode);

            return string.Join(", ", parts);
        }

        public string GetShortAddress()
        {
            return $"{City}, {Country}";
        }


        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Country;
            yield return City;
            yield return Street;
            yield return PostalCode ?? string.Empty;
            yield return Latitude ?? 0;
            yield return Longitude ?? 0;
        }

        public override string ToString()
        {
            return GetFullAddress();
        }
    }
}