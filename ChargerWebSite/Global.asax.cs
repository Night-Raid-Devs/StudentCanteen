using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using ChargeWebSite.Services;
using Infocom.Chargers.BackendCommon;
using Infocom.Chargers.FrontendCommon;

namespace ChargeWebSite
{
    public class MvcApplication : System.Web.HttpApplication
    {
        public const string GoogleMapApiKey = "AIzaSyAEPGzLwvvtt5aCIu2QzO645E-Wlr1ESvg";

        public const string GoogleApiUrl = "https://maps.googleapis.com";

        public const string RestUrl = "http://192.168.56.99:8080";

        public static RestDataManager DataManager { get; private set; }

        public static CustomerData CustomerData { get; set; }

        public static MapManager MapManager { get; private set; } = new MapManager(new RestService(GoogleApiUrl));

        public static string MapCoords { get; set; }

        protected void Application_Start()
        {
            DataManager = new RestDataManager(new RestService(RestUrl));

            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        } 
    }
}
