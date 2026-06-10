using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Exceptions
{
    internal class DomainException : Exception
    {
        public DomainException(string message) : base(message)
        {
            
            
        }
        public DomainException(string message, Exception innerException) : base(message, innerException)
        {
        }
        public DomainException()
        {

        }
        public DomainException(string message, params object[] args) : base(string.Format(message, args))
        {
        }
        public DomainException(string message, Exception innerException, params object[] args) : base(string.Format(message, args), innerException)
        {
        }

    }
}
