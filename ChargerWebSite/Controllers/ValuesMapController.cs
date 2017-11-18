using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Mvc;
using Infocom.Chargers.BackendCommon;

namespace ChargeWebSite.Controllers
{
    public class ValuesMapController : ApiController
    {
        // GET: api/ValuesMap
        public HttpResponseMessage Get()
        {
            try
            {
                if (MvcApplication.CustomerData.AccessRights == AccessRightEnum.Admin.ToString())
                {
                    HttpResponseMessage response = new HttpResponseMessage();
                    response.Content = new StringContent(MvcApplication.MapCoords);
                    return response;
                }
                else
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }
            }
            catch (Exception)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }

        // GET: api/ValuesMap/5
        public async Task<HttpResponseMessage> Get(string login, string password)
        {
            try
            {
                await MvcApplication.DataManager.LogIn(login, password);
                MvcApplication.CustomerData = await MvcApplication.DataManager.GetCustomerData(login);
                HttpResponseMessage response = new HttpResponseMessage();
                response.Content = new StringContent(MvcApplication.MapCoords);
                return response;
            }
            catch (Exception)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }

        // POST: api/ValuesMap
        public void Post([FromBody]string value)
        {
        }

        // PUT: api/ValuesMap/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE: api/ValuesMap/5
        public void Delete(int id)
        {
        }
    }
}
