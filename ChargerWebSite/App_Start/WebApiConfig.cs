using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace ChargeWebSite
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "LoginApi",
                routeTemplate: "api/{controller}/{login}/{password}");
            config.Routes.MapHttpRoute(
                name: "CoordsApi",
                routeTemplate: "api/{controller}");
        }
    }
}
