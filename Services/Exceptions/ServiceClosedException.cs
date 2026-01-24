using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BarTasca.Services.Exceptions
{
    public class ServiceClosedException : Exception
    {
        public ServiceClosedException() : base("SERVICE_CLOSED")
        {
        }
    }
}
