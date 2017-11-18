using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Infocom.Chargers.FrontendCommon
{
    public class RestService : IRestService
    {
        private HttpClientHandler clientHandler;
        private HttpClient client;

        public RestService(string restUrl)
        {
            this.clientHandler = new HttpClientHandler();
            this.client = new HttpClient(this.clientHandler) { BaseAddress = new Uri(restUrl) };
            this.client.MaxResponseContentBufferSize = 256000;
        }

        public string BaseAddress
        {
            get
            {
                return this.client.BaseAddress.OriginalString;
            }
        }

        public CookieContainer Cookies
        {
            get
            {
                return this.clientHandler.CookieContainer;
            }

            set
            {
                this.InitializeRestServise(this.BaseAddress);
                this.clientHandler.CookieContainer = value;
            }
        }

        public async Task<HttpResponseMessage> GetData(string link)
        {
            HttpResponseMessage response = null;
            try
            {
                response = await this.client.GetAsync(link);
                System.Diagnostics.Debug.WriteLine("GetData link = {0}; Status code = {1}", link, response.StatusCode);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERROR {0}", ex.Message);
            }

            return response;
        }

        public async Task<HttpResponseMessage> SaveData(string link, string jsonData, bool isNewData)
        {
            HttpResponseMessage response = null;
            try
            {
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                response = isNewData ?
                    await this.client.PostAsync(link, content) :
                    await this.client.PutAsync(link, content);
                System.Diagnostics.Debug.WriteLine("SaveData link = {0}; Status code = {1}; Is new = {2}", link, response.StatusCode, isNewData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERROR {0}", ex.Message);
            }

            return response;
        }

        public async Task<HttpResponseMessage> DeleteData(string link)
        {
            HttpResponseMessage response = null;
            try
            {
                response = await this.client.DeleteAsync(link);
                System.Diagnostics.Debug.WriteLine("DeleteData link = {0}; Status code = {1}", link, response.StatusCode);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERROR {0}", ex.Message);
            }

            return response;
        }

        private void InitializeRestServise(string restUrl)
        {
            this.clientHandler = new HttpClientHandler();
            this.client = new HttpClient(this.clientHandler) { BaseAddress = new Uri(restUrl) };
            this.client.MaxResponseContentBufferSize = 256000;
        }
    }
}
