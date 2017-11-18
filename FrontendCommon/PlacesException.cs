using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infocom.Chargers.FrontendCommon
{
    public class PlacesException : Exception
    {
        public PlacesException(string message) : base(message)
        {
        }
    }
}
