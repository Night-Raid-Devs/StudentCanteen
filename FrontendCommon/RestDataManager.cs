using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Infocom.Chargers.BackendCommon;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Infocom.Chargers.FrontendCommon
{
    public class RestDataManager
    {
        private const string CustomersTemplate = "/customers";
        private const string CarsTemplate = "/cars";
        private const string StationsTemplate = "/stations";
        private const string PortsTemplate = "/ports";
        private const string ManagePortTemplate = "/ManagePort";
        private const string TariffsTemplate = "/tariffs";
        private const string ChargingSessionsTemplate = "/ChargingSessions";
        private const string RFIDsTemplate = "/RFIDs";
        private const string SessionsTemplate = "/sessions";
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
            System.Diagnostics.Debug.WriteLine("LogIn request");
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

        public async Task<List<SessionData>> SearchSessions(long? phone = null, DateTime? startTime = null, DateTime? stopTime = null, AccessRightEnum? accessRights = null, string ip = null, long? lastId = null, long? limit = null, string email = null, string organizationName = null, string firstName = null, string middleName = null, string lastName = null)
        {
            /* 
             /sessions
             ?lastid={lastId}&start={startTime}&stop={stopTime}&ar={accessRights}&ip={ip}&p={phone}&e={email}&o={organizationName}&f={firstName}&m={middleName}&l={lastName}&limit={limit}
            */

            System.Diagnostics.Debug.WriteLine("Searching sessions");
            string link = SessionsTemplate + "?" +
                GetParam("lastid", lastId, true) +
                GetParam("start", startTime?.ToEpochtime()) +
                 GetParam("stop", stopTime?.ToEpochtime()) +
                GetParam("ar", accessRights) +
                GetParam("ip", ip) +
                GetParam("p", phone) +
                GetParam("e", email) +
                GetParam("o", organizationName) +
                GetParam("f", firstName) +
                GetParam("m", middleName) +
                GetParam("l", lastName) +
                GetParam("limit", limit);

            HttpResponseMessage response = await this.restService.GetData(link);
            await GetErrorMessage(response);
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<SessionData>>(content);
        }

        #endregion

        #region CustomerData

        public async Task<CustomerData> GetCustomerData(string login)
        {
            System.Diagnostics.Debug.WriteLine("Getting customer data " + login);
            HttpResponseMessage response = await this.restService.GetData(CustomersTemplate + "/" + login);
            await GetErrorMessage(response);
            this.IsLoggedIn = true;
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<CustomerData>(content);
        }

        public async Task AddCustomerData(CustomerData customerData)
        {
            System.Diagnostics.Debug.WriteLine("Creating customer data " + customerData.Email);
            string jsonData = JsonConvert.SerializeObject(customerData);
            HttpResponseMessage response = await this.restService.SaveData(CustomersTemplate, jsonData, true);
            await GetErrorMessage(response);
            this.IsLoggedIn = true;
        }

        public async Task ChangeCustomerData(CustomerData customerData)
        {
            System.Diagnostics.Debug.WriteLine("Saving customer data " + customerData.Email);
            JObject jsonObject = JObject.FromObject(customerData);
            jsonObject.Remove("Password");
            string jsonData = jsonObject.ToString();
            HttpResponseMessage response = await this.restService.SaveData(CustomersTemplate, jsonData, false);
            await GetErrorMessage(response);
        }

        public async Task RemoveCustomer(CustomerData customer) 
        {
            System.Diagnostics.Debug.WriteLine("Removing customer (id = " + customer.Id + ")");
            HttpResponseMessage response = await this.restService.DeleteData(CustomersTemplate + "?id=" + customer.Id + "&purge=false");
            await GetErrorMessage(response);
        }

        public async Task<List<CustomerData>> SearchCustomerData()
        {
            System.Diagnostics.Debug.WriteLine("Search customer data ");
            HttpResponseMessage response = await this.restService.GetData(CustomersTemplate + "?");
            await GetErrorMessage(response);
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<CustomerData>>(content);
        }
        #endregion

        #region CarData

        public async Task<long> AddCarData(CarData carData)
        {
            System.Diagnostics.Debug.WriteLine("Adding Car " + carData.RegNumber);
            string jsonData = JsonConvert.SerializeObject(carData);
            HttpResponseMessage response = await this.restService.SaveData(CarsTemplate, jsonData, true);
            await GetErrorMessage(response);
            var content = await response.Content.ReadAsStringAsync();
            return Convert.ToInt64(content);
        }

        public async Task RemoveCarData(CarData carData)
        {
            System.Diagnostics.Debug.WriteLine("Removing car " + carData.RegNumber);
            HttpResponseMessage response = await this.restService.DeleteData(CarsTemplate + "?id=" + carData.Id + "&cid=" + carData.CustomerId + "&purge=false");
            await GetErrorMessage(response);
        }

        #endregion

        #region StationData

        public async Task<StationData> GetStation(long stationId)
        {
            System.Diagnostics.Debug.WriteLine("Getting station with id: " + stationId);
            HttpResponseMessage response = await this.restService.GetData(StationsTemplate + "/" + stationId);
            await GetErrorMessage(response);
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<StationData>(content);
        }

        public async Task<List<StationData>> GetAllStations()
        {
            System.Diagnostics.Debug.WriteLine("Getting all stations");
            HttpResponseMessage response = await this.restService.GetData(StationsTemplate + "?t=town");
            await GetErrorMessage(response);
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<StationData>>(content);
        }

        public async Task<List<StationData>> GetStations(double lat, double lng, double radiusInMeters)
        {
            System.Diagnostics.Debug.WriteLine("Getting stations from pos: " + lat + "; " + lng + "; radius = " + radiusInMeters);
            HttpResponseMessage response = await this.restService.GetData(StationsTemplate + "?t=road" + "&la=" + lat + "&lo=" + lng + "&d=" + radiusInMeters);
            await GetErrorMessage(response);
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<StationData>>(content);
        }

        public async Task<long> AddStation(StationData station)
        {
            System.Diagnostics.Debug.WriteLine("Creating station " + station.Name);
            string jsonData = JsonConvert.SerializeObject(station);
            HttpResponseMessage response = await this.restService.SaveData(StationsTemplate, jsonData, true);
            await GetErrorMessage(response);
            var content = await response.Content.ReadAsStringAsync();
            return Convert.ToInt64(content);
        }

        public async Task RemoveStation(StationData station)
        {
            System.Diagnostics.Debug.WriteLine("Removing station " + station.Name);
            HttpResponseMessage response = await this.restService.DeleteData(StationsTemplate + "?id=" + station.Id + "&purge=false");
            await GetErrorMessage(response);
        }

        public async Task ChangeStationData(StationData station)
        {
            System.Diagnostics.Debug.WriteLine("Changing station data (id = " + station.Id + ")");
            string jsonData = JsonConvert.SerializeObject(station);
            HttpResponseMessage response = await this.restService.SaveData(StationsTemplate, jsonData, false);
            await GetErrorMessage(response);
        }

        #endregion

        #region PortData

        public async Task<long> AddPort(PortData port)
        {
            System.Diagnostics.Debug.WriteLine("Creating port " + port.Name);
            string jsonData = JsonConvert.SerializeObject(port);
            HttpResponseMessage response = await this.restService.SaveData(PortsTemplate, jsonData, true);
            await GetErrorMessage(response);
            var content = await response.Content.ReadAsStringAsync();
            return Convert.ToInt64(content);
        }

        public async Task ChangePortData(PortData port)
        {
            System.Diagnostics.Debug.WriteLine("Changing port data (id = " + port.Id + ")");
            string jsonData = JsonConvert.SerializeObject(port);
            HttpResponseMessage response = await this.restService.SaveData(StationsTemplate, jsonData, false);
            await GetErrorMessage(response);
        }

        public async Task RemovePort(PortData port)
        {
            System.Diagnostics.Debug.WriteLine("Removing port (id = " + port.Id + ")");
            HttpResponseMessage response = await this.restService.DeleteData(PortsTemplate + "?id=" + port.Id + "&sid= " + port.StationId + "&purge=false");
            await GetErrorMessage(response);
        }

        public async Task<string> ManagePort(string portId, string command) ////
        {
            System.Diagnostics.Debug.WriteLine("Managing port (id = " + portId + ") Command: " + command);
            HttpResponseMessage response = await this.restService.GetData(StationsTemplate + "?pid=" + portId + "&pc=" + command);
            await GetErrorMessage(response);
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<ChargingDriverStatusEnum>(content).ToString();
        }

        #endregion

        #region TariffData

        public async Task<long> AddTariff(TariffData tariff)
        {
            System.Diagnostics.Debug.WriteLine("Creating tariff " + tariff.Name);
            string jsonData = JsonConvert.SerializeObject(tariff);
            HttpResponseMessage response = await this.restService.SaveData(TariffsTemplate, jsonData, true);
            await GetErrorMessage(response);
            var content = await response.Content.ReadAsStringAsync();
            return Convert.ToInt64(content);
        }

        #endregion

        #region ChargingSessions

        public async Task<StartChargingSessionData> StartChargingSession(long customerId, long portId, long? carId = null, long? maxChargeTimeInSeconds = null, double? maxEnergyInKWH = null)
        {
            System.Diagnostics.Debug.WriteLine("Starting Charging session, carId: " + carId + "; portId: " + portId);
            HttpResponseMessage response = await this.restService.GetData("/StartChargingSession" +
                GetParam("cid", customerId, true) +
                GetParam("pid", portId) +
                GetParam("car", carId) +
                GetParam("mct", maxChargeTimeInSeconds) +
                GetParam("mec", maxEnergyInKWH) +
                GetParam("ps", "LiqPay"));
            await GetErrorMessage(response);
            var content = await response.Content.ReadAsStringAsync();
            var startChargingSessionData = JsonConvert.DeserializeObject<StartChargingSessionData>(content);
            startChargingSessionData.PaymentString.Replace("\"", string.Empty).Replace("\\", string.Empty);
            return startChargingSessionData;
        }

        public async Task StopChargingSession(long chargingSessionId)
        {
            System.Diagnostics.Debug.WriteLine("Stopping Charging session, chargingSessionId: " + chargingSessionId);
            HttpResponseMessage response = await this.restService.GetData("/StopChargingSession?csid=" + chargingSessionId);
            await GetErrorMessage(response);
        }

        public async Task<ChargingSessionData> GetChargingSession(long chargingSessionId)
        {
            System.Diagnostics.Debug.WriteLine("Getting charging session, chargingSessionId: " + chargingSessionId);
            HttpResponseMessage response = await this.restService.GetData(ChargingSessionsTemplate + "/" + chargingSessionId);
            await GetErrorMessage(response);
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<ChargingSessionData>(content);
        }

        public async Task<List<ChargingSessionData>> GetStoppableChargingSessions()
        {
            System.Diagnostics.Debug.WriteLine("Getting stoppable charging sessions");
            DateTime currentTime = DateTime.Now;
            HttpResponseMessage response = await this.restService.GetData(ChargingSessionsTemplate +
                "?start=" + (currentTime - new TimeSpan(12, 0, 0)).ToEpochtime() +
                "&stop=" + currentTime.ToEpochtime() +
                "&s=SessionStart,PaymentHolded,ChargingUnblocked,ChargingStarted");
            await GetErrorMessage(response);
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<ChargingSessionData>>(content);
        }

        #endregion

        #region RFIDs

        public async Task<long> CreateRFID(RFIDData rfidData)
        {
            System.Diagnostics.Debug.WriteLine("Creating RFID, value: " + rfidData.Value);
            string jsonData = JsonConvert.SerializeObject(rfidData);
            HttpResponseMessage response = await this.restService.SaveData(RFIDsTemplate, jsonData, true);
            await GetErrorMessage(response);
            var content = await response.Content.ReadAsStringAsync();
            return Convert.ToInt64(content);
        }

        public async Task SetRFIDBlockStatus(RFIDData rfidData)
        {
            System.Diagnostics.Debug.WriteLine("SetRFIDBlockStatus, value: " + rfidData.Value + "; blocked: " + rfidData.Blocked);
            string jsonData = JsonConvert.SerializeObject(rfidData);
            HttpResponseMessage response = await this.restService.SaveData(RFIDsTemplate, jsonData, false);
            await GetErrorMessage(response);
        }

        #endregion

        #region Payments

        public async Task<double> GetPaymentAmountEstimation(long portId, long? maxChargeTimeInSeconds, double? maxEnergyInKWH)
        {
            System.Diagnostics.Debug.WriteLine("Getting payment amout estination, portId = {0}; maxChargeTime = {1}, maxEnergy = {2}", portId, maxChargeTimeInSeconds, maxEnergyInKWH);
            HttpResponseMessage response = await this.restService.GetData("GetPaymentAmountEstimation" +
                GetParam("pid", portId, true) +
                GetParam("mct", maxChargeTimeInSeconds) +
                GetParam("mec", maxEnergyInKWH));
            await GetErrorMessage(response);
            var content = await response.Content.ReadAsStringAsync();
            return Convert.ToDouble(content, CultureInfo.InvariantCulture);
        }

        #endregion

        private static string GetParam<T>(string key, T value, bool isFirst = false)
        {
            if (value == null)
            {
                return string.Empty;
            }

            string param = key + "=" + value;
            if (isFirst)
            {
                return param;
            }
            else
            {
                return "&" + param;
            }
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
    }
}