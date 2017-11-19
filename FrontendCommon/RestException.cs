using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using BackendCommon;

namespace FrontendCommon
{
    public class RestException : Exception
    {
        public RestException(HttpStatusCode statusCode, RestApiErrorMessage error) : base()
        {
            this.StatusCode = statusCode;
            this.Error = error;
        }

        public HttpStatusCode StatusCode { get; set; }

        public RestApiErrorMessage Error { get; set; }

        public override string Message => this.Error.Error;
    }
}
