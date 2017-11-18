using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Infocom.Chargers.FrontendCommon
{
    public interface IRestService
    {
        string BaseAddress { get; }

        CookieContainer Cookies { get; set; }

        Task<HttpResponseMessage> GetData(string link);

        Task<HttpResponseMessage> SaveData(string link, string jsonData, bool isNewData);

        Task<HttpResponseMessage> DeleteData(string link);
    }
}
