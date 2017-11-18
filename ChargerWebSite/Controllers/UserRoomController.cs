using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using ChargeWebSite.Models;
using Infocom.Chargers.BackendCommon;
using Infocom.Chargers.FrontendCommon;
using Newtonsoft.Json;

namespace ChargeWebSite.Controllers
{
    public class UserRoomController : Controller
    {
        //// GET: UserRoom

        private const string Keyword = "Electric%20Vehicle%20Charging%20Station";

        public static string ViewBagErrorMessage { get; set; }

        private static HashSet<OtherStation> OtherStations { get; set; }

        private static HashSet<Station> Stations { get; set; }

        public async Task<ActionResult> Index()
        {
            if (MvcApplication.CustomerData != null && MvcApplication.DataManager.IsLoggedIn)
            {
                UserRoomController.OtherStations = new HashSet<OtherStation>();
                UserRoomController.Stations = new HashSet<Station>();
                switch (MvcApplication.CustomerData.Sex)
                {
                    case null:
                        this.ViewBag.Sex = "Не указано";
                        break;
                    case true:
                        this.ViewBag.Sex = "Мужской";
                        break;
                    case false:
                        this.ViewBag.Sex = "Женский";
                        break;
                }

                List<StationData> stationsData = await MvcApplication.DataManager.GetAllStations();
                foreach (StationData stationData in stationsData)
                {
                    Station st = new Station();
                    st.CopyParams(stationData);
                    Stations.Add(st);
                }

                ViewBag.AllStations = Stations;
                ViewBag.Result = ViewBagErrorMessage;
                ViewBag.CustomerType = (MvcApplication.CustomerData.CustomerType.ToString() == "Private") ? true : false;
                return this.View(MvcApplication.CustomerData);
            }
            else
            {
                return this.RedirectToAction("LogIn", "Account");
            }
        }

        public async Task<ActionResult> EditUserInfo(string newFirstName, string newLastName, string newEmail, string newPhone, string newDateTime, string newSex, string newCountry, string newTown, string newAddress, string newPostIndex, string newOrganizationName)
        {
            try
            {
                CustomerData localCustomerData = JsonConvert.DeserializeObject<CustomerData>(JsonConvert.SerializeObject(MvcApplication.CustomerData));
                ////Only private data
                if (!string.IsNullOrWhiteSpace(newFirstName))
                {
                    localCustomerData.FirstName = newFirstName;
                }

                if (!string.IsNullOrWhiteSpace(newLastName))
                {
                    localCustomerData.LastName = newLastName;
                }

                if (!string.IsNullOrWhiteSpace(newDateTime))
                {
                    localCustomerData.BirthDate = Convert.ToDateTime(newDateTime);
                }

                switch (Convert.ToInt32(newSex))
                {
                    case 0:
                        localCustomerData.Sex = null;
                        break;
                    case 1:
                        localCustomerData.Sex = true;
                        break;
                    case 2:
                        localCustomerData.Sex = false;
                        break;
                }

                ////Only organization data
                if (!string.IsNullOrWhiteSpace(newOrganizationName))
                {
                    localCustomerData.OrganizationName = newOrganizationName;
                }

                ////General data
                if (!string.IsNullOrWhiteSpace(newEmail))
                {
                    localCustomerData.Email = newEmail;
                }

                if (!string.IsNullOrWhiteSpace(newPhone))
                {
                    localCustomerData.Phone = (long?)Convert.ToDouble(newPhone);
                }

                if (!string.IsNullOrWhiteSpace(newCountry))
                {
                    localCustomerData.Country = newCountry;
                }

                if (!string.IsNullOrWhiteSpace(newTown))
                {
                    localCustomerData.Town = newTown;
                }

                if (!string.IsNullOrWhiteSpace(newAddress))
                {
                    localCustomerData.Address = newAddress;
                }

                if (!string.IsNullOrWhiteSpace(newPostIndex))
                {
                    localCustomerData.PostIndex = newPostIndex;
                }

                await MvcApplication.DataManager.ChangeCustomerData(localCustomerData);
                MvcApplication.CustomerData = localCustomerData;
                ViewBagErrorMessage = null;
                return this.RedirectToAction("Index", "UserRoom");
            }
            catch (Exception ex)
            {
                ViewBagErrorMessage = ex.Message;
                return this.RedirectToAction("Index", "UserRoom");
            }
        }

        [HttpGet]
        public async Task<ActionResult> LogOut()
        {
            try
            {
                await MvcApplication.DataManager.LogOut();
                ViewBagErrorMessage = null;
                return this.RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ViewBagErrorMessage = ex.Message;
                return this.RedirectToAction("Index", "UserRoom");
            }
        }

        [HttpPost]
        public async Task<ActionResult> UpdateOtherStations(Position newCenter)
        {
            List<OtherStation> localOtherStations = new List<OtherStation>();
            List<OtherStation> newOtherStations = new List<OtherStation>();

            try
            {
                localOtherStations = await MvcApplication.MapManager.GetOtherStations(newCenter, 50000);
            }
            catch (PlacesException ex)
            {
                System.Diagnostics.Debug.WriteLine("UpdateOtherStations exception " + ex.Message);
            }

            foreach (OtherStation station in localOtherStations)
            {
                if (!UserRoomController.OtherStations.Contains(station))
                {
                    UserRoomController.OtherStations.Add(station);
                    newOtherStations.Add(station);
                }
            }

            return this.Json(newOtherStations, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public async Task<ActionResult> GetOtherStationInfo(string placeId)
        {
            OtherStation station = UserRoomController.OtherStations.SingleOrDefault(x => x.PlaceId == placeId);
            if (station.Phone == null)
            {
                OtherStation stationInfo = await MvcApplication.MapManager.GetOtherStationInfo(placeId);
                station.Address = stationInfo.Address;
                station.MapUrl = stationInfo.MapUrl;
                station.Name = stationInfo.Name;
                station.Phone = stationInfo.Phone;
                station.Url = stationInfo.Url;
                station.Web = stationInfo.Web;
            }

            return this.Json(station, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult GetStationInfo(long id)
        {
            Station station = Stations.FirstOrDefault(x => x.Id == id);
            if (Stations != null)
                {
                return this.PartialView(station);
                }

            return this.HttpNotFound();
        }

        [HttpPost]
        public JsonResult GetMarkerImage(long? id, bool ourStation)
        {
            if (ourStation)
            {
                Station station = Stations.FirstOrDefault(x => x.Id == id);
                if (Stations != null)
                {
                    return this.Json(station.GetMarkerImage());
                }

                return this.Json("DONT_FIND_STATION");
            }
            else
            {
                return this.Json(new string[] { "/Content/images/other_station.png", "/Content/images/selected_other_station.png" });
            }
        }

        [HttpGet]
        public JsonResult GetOtherStationsFromHeshSet()
        {
            return this.Json(UserRoomController.OtherStations, JsonRequestBehavior.AllowGet);
        }
    }
}
