using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using ChargeWebSite.Models;
using Infocom.Chargers.FrontendCommon;
using Newtonsoft.Json.Linq;

namespace ChargeWebSite.Services
{
    public class MapManager
    {
        private const string Keyword = "Electric%20Vehicle%20Charging%20Station";

        private IRestService restService;

        public MapManager(IRestService restService)
        {
            this.restService = restService;
        }

        public async Task<List<OtherStation>> GetOtherStations(Position location, double radiusInMeters)
        {
            System.Diagnostics.Debug.WriteLine("Getting other stations from " + location.Latitude + "; " + location.Longitude);
            string lat = location.Latitude.ToString(CultureInfo.InvariantCulture);
            string lng = location.Longitude.ToString(CultureInfo.InvariantCulture);
            string radius = (radiusInMeters <= 50000) ? radiusInMeters.ToString(CultureInfo.InvariantCulture) : "50000";
            HttpResponseMessage response =
                await this.restService.GetData("/maps/api/place/radarsearch/json?" +
                    "location=" + lat + "," + lng +
                    "&radius=" + radius +
                    "&keyword=" + Keyword +
                    "&key=" + MvcApplication.GoogleMapApiKey);
            JObject data = await GetResponseData(response);
            List<OtherStation> otherStations = new List<OtherStation>();
            List<JToken> results = data["results"].ToList();
            foreach (JToken token in results)
            {
                JToken locationToken = token["geometry"]["location"];
                otherStations.Add(new OtherStation()
                {
                    PlaceId = token["place_id"].ToObject<string>(),
                    Position = new Position()
                    {
                        Latitude = locationToken["lat"].ToObject<double>(),
                        Longitude = locationToken["lng"].ToObject<double>()
                    }
                });
            }

            System.Diagnostics.Debug.WriteLine("Other stations count = " + otherStations.Count);
            return otherStations;
        }

        public async Task<OtherStation> GetOtherStationInfo(string placeId)
        {
            System.Diagnostics.Debug.WriteLine("Getting information about placeId = " + placeId);
            HttpResponseMessage response =
                await this.restService.GetData("/maps/api/place/details/json?placeid=" + placeId +
                    "&key=" + MvcApplication.GoogleMapApiKey);
            JObject data = await GetResponseData(response);
            JToken result = data["result"];
            JToken locationToken = result["geometry"]["location"];
            OtherStation otherStation = new OtherStation()
            {
                Address = result["formatted_address"]?.ToObject<string>(),
                MapUrl = result["url"]?.ToObject<string>(),
                Name = result["name"]?.ToObject<string>(),
                Phone = result["international_phone_number"]?.ToObject<string>(),
                PlaceId = result["place_id"]?.ToObject<string>(),
                Web = result["website"]?.ToObject<string>(),
                Position = new Position
                {
                    Latitude = locationToken["lat"].ToObject<double>(),
                    Longitude = locationToken["lng"].ToObject<double>()
                }
            };
            return otherStation;
        }

        private static async Task<JObject> GetResponseData(HttpResponseMessage response)
        {
            if (response == null)
            {
                throw new PlacesException("Нет соединения");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new PlacesException("Ошибка запроса: " + response.StatusCode);
            }

            var content = await response.Content.ReadAsStringAsync();
            JObject data = JObject.Parse(content);
            string status = data["status"]?.ToObject<string>();
            if (status != "OK")
            {
                throw new PlacesException("Ошибка запроса Google API: " + status);
            }

            return data;
        }
    }
}