using Domain.Common;


namespace Domain.Primitives
{
    public class Address : ValueObjectBase
    {
        public string Country { get; private set; }
        public string City { get; private set; }
        public string Street { get; private set; }

        
        private Address() { }

        private Address(string country, string city, string street)
        {
            Country = country;
            City = city;
            Street = street;
        }

        public static Result<Address> Create(
            string country,
            string city,
            string street)
        {
            if (string.IsNullOrEmpty(country))
               return Result<Address>.Failure("Country cannot be null or empty.");
            if (string.IsNullOrEmpty(city))
                return Result<Address>.Failure("City cannot be null or empty.");
            if (string.IsNullOrEmpty(street))
                return Result<Address>.Failure("Street cannot be null or empty.");

            return Result<Address>
                    .Success(new Address(country, city, street));
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {  
            yield return Country;
            yield return City;
            yield return Street;
        }
    }
}
