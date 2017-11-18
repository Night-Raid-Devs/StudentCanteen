using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using ChargeWebSite.Models;
using Infocom.Chargers.BackendCommon;
using Infocom.Chargers.FrontendCommon;

namespace ChargeWebSite.Controllers
{
    public class FindChargeController : Controller
    {
        private static HashSet<OtherStation> OtherStations { get; set; }

        private static HashSet<Station> Stations { get; set; }

        public async Task<ActionResult> Index()
        {
            FindChargeController.OtherStations = new HashSet<OtherStation>();
            FindChargeController.Stations = new HashSet<Station>();
            ////List<StationData> stationsData = await MvcApplication.DataManager.GetAllStations();
            ////foreach (StationData stationData in stationsData)
            ////{
            ////    Station st = new Station();
            ////    st.CopyParams(stationData);
            ////    Stations.Add(st);
            ////}

            ViewBag.AllStations = Stations;
            return this.View();
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

                return this.Json("Get Marker Image Error!");
            }
            else
            {
                return this.Json(new string[] { "/Content/images/other_station.png", "/Content/images/selected_other_station.png" });
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
                if (!FindChargeController.OtherStations.Contains(station))
                {
                    FindChargeController.OtherStations.Add(station);
                    newOtherStations.Add(station);
                }
            }

            return this.Json(newOtherStations, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public async Task<ActionResult> GetOtherStationInfo(string placeId)
        {
            OtherStation station = FindChargeController.OtherStations.SingleOrDefault(x => x.PlaceId == placeId);
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
        public JsonResult GetOtherStationsFromHeshSet()
        {
            return this.Json(FindChargeController.OtherStations, JsonRequestBehavior.AllowGet);
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

        public ActionResult DispatcherMap()
        {
            try
            {
                if (MvcApplication.CustomerData.AccessRights.ToString() == "Admin")
                {
                    return this.View();
                }
                else
                {
                    return new HttpNotFoundResult();
                }
            }
            catch (Exception)
            {
                return new HttpNotFoundResult();
            }
        }

        public ActionResult CreateMarker()
        {
            try
            {
                if (MvcApplication.CustomerData.AccessRights.ToString() == "Admin")
                {
                    return this.View();
                }
                else
                {
                    return new HttpNotFoundResult();
                }
            }
            catch (Exception)
            {
                return new HttpNotFoundResult();
            }
        }

        public void Dispatcher(string latLng)
        {
            MvcApplication.MapCoords = latLng.ToString();
        }
    }
}