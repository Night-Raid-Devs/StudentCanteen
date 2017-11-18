using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using ChargeWebSite.Models;

namespace ChargeWebSite.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return this.View();
        }

        public ActionResult FindCharge()
        {
            return this.RedirectToAction("Index", "FindCharge");
        }

        public ActionResult DispatcherMap()
        {
            return this.RedirectToAction("DispatcherMap", "FindCharge");
        }

        public ActionResult CreateMarker()
        {
            return this.RedirectToAction("CreateMarker", "FindCharge");
        }

        public ActionResult ServicesForLegalBody()
        {
            return this.View();
        }

        public ActionResult ServicesForPrivatePerson()
        {
            return this.View();
        }

        public void SellCharge()
        {
        }
    }
}