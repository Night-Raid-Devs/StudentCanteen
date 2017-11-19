using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using BackendCommon;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FrontendCommon
{
    public class RestDataManager
    {
        private const string CustomersTemplate = "/customers";
        private const string DishesTemplate = "/dishes";
        private const string OrdersTemplate = "/orders";
        private IRestService restService;

        public RestDataManager(IRestService restService)
        {
            this.restService = restService;
            this.IsLoggedIn = false;
        }

        #region Sessions

        public bool IsLoggedIn { get; private set; }

        public CookieContainer Cookies
        {
            get
            {
                return this.restService.Cookies;
            }

            set
            {
                this.restService.Cookies = value;
            }
        }

        public async Task LogIn(string login, string password)
        {
            System.Diagnostics.Debug.WriteLine("LogIn request " + login);
            HttpResponseMessage response = await this.restService.GetData(string.Format("/login/{0}/{1}", login, password));
            await GetErrorMessage(response);
            this.IsLoggedIn = true;
        }

        public async Task LogOut()
        {
            System.Diagnostics.Debug.WriteLine("LogOut request");
            this.IsLoggedIn = false;
            HttpResponseMessage response = await this.restService.GetData("/logout");
            await GetErrorMessage(response);
        }

        #endregion

        #region Customers

        public async Task CreateCustomer(CustomerData customer)
        {
            System.Diagnostics.Debug.WriteLine("Creating customer " + customer.Login);
            string jsonData = JsonConvert.SerializeObject(customer);
            HttpResponseMessage response = await this.restService.SaveData(CustomersTemplate, jsonData, true);
            await GetErrorMessage(response);
            this.IsLoggedIn = true;
        }

        public async Task<CustomerData> GetCustomer(string login)
        {
            System.Diagnostics.Debug.WriteLine("Getting customer " + login);
            HttpResponseMessage response = await this.restService.GetData(CustomersTemplate + "/" + login);
            await GetErrorMessage(response);
            this.IsLoggedIn = true;
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<CustomerData>(content);
        }

        public async Task<List<CustomerData>> GetCustomers()
        {
            System.Diagnostics.Debug.WriteLine("Getting customers");
            HttpResponseMessage response = await this.restService.GetData(CustomersTemplate);
            await GetErrorMessage(response);
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<CustomerData>>(content);
        }

        public async Task DeleteCustomer(CustomerData customer) 
        {
            System.Diagnostics.Debug.WriteLine("Deleting customer " + customer.Id);
            HttpResponseMessage response = await this.restService.DeleteData(CustomersTemplate + "?id=" + customer.Id);
            await GetErrorMessage(response);
        }

        #endregion

        #region Dishes

        public async Task<List<long>> CreateDishes(List<DishData> dishes)
        {
            System.Diagnostics.Debug.WriteLine("Creating dishes");
            string jsonData = JsonConvert.SerializeObject(dishes);
            HttpResponseMessage response = await this.restService.SaveData(DishesTemplate, jsonData, true);
            await GetErrorMessage(response);
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<long>>(content);
        }

        public async Task UpdateDishes(List<DishData> dishes)
        {
            System.Diagnostics.Debug.WriteLine("Updating dishes");
            string jsonData = JsonConvert.SerializeObject(dishes);
            HttpResponseMessage response = await this.restService.SaveData(DishesTemplate, jsonData, false);
            await GetErrorMessage(response);
        }

        public async Task<List<DishData>> GetDishes(long? customerId, DateTime startDate, DateTime endDate)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("Getting dishes: customerId={0}; startDate={1}; endDate={2}",
                customerId, startDate.ToShortDateString(), endDate.ToShortDateString()));
            Dictionary<string, object> query = new Dictionary<string, object>
            {
                { "cid", customerId },
                { "start", startDate.ToEpochtime() },
                { "end", endDate.ToEpochtime() }
            };
            HttpResponseMessage response = await this.restService.GetData(DishesTemplate + GetQueryString(query));
            await GetErrorMessage(response);
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<DishData>>(content);
        }

        #endregion

        #region Orders

        public async Task<List<long>> CreateOrders(List<OrderData> orders)
        {
            System.Diagnostics.Debug.WriteLine("Creating orders");
            string jsonData = JsonConvert.SerializeObject(orders);
            HttpResponseMessage response = await this.restService.SaveData(DishesTemplate, jsonData, true);
            await GetErrorMessage(response);
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<long>>(content);
        }

        public async Task UpdateOrders(List<OrderData> orders)
        {
            System.Diagnostics.Debug.WriteLine("Updating orders");
            string jsonData = JsonConvert.SerializeObject(orders);
            HttpResponseMessage response = await this.restService.SaveData(DishesTemplate, jsonData, false);
            await GetErrorMessage(response);
        }

        #endregion

        #region private

        private static string GetQueryString(Dictionary<string, object> query)
        {
            string result = "?";
            bool isFirst = true;
            foreach (var pair in query)
            {
                if (pair.Value != null)
                {
                    result += (isFirst ? string.Empty : "&") + pair.Key + "=" + pair.Value;
                }
            }
            return result;
        }

        private static async Task GetErrorMessage(HttpResponseMessage response)
        {
            HttpStatusCode statusCode = response != null ? response.StatusCode : HttpStatusCode.NotFound;
            RestApiErrorMessage errorMessage;
            if (response != null)
            {
                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                var content = await response.Content.ReadAsStringAsync();
                try
                {
                    errorMessage = JsonConvert.DeserializeObject<RestApiErrorMessage>(content);
                }
                catch
                {
                    errorMessage = new RestApiErrorMessage("Неверный запрос");
                }
            }
            else
            {
                errorMessage = new RestApiErrorMessage("Нет соединения");
            }

            throw new RestException(statusCode, errorMessage);
        }

        #endregion
    }
}