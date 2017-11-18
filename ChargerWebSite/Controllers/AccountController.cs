using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.UI;
using ChargeWebSite.Models;
using Infocom.Chargers.BackendCommon;
using Infocom.Chargers.FrontendCommon;
using Newtonsoft.Json;

namespace ChargeWebSite.Controllers
{
    public class AccountController : Controller
    {
        // GET: Account
        public ActionResult LogIn()
        {
            return this.View();
        }

        [HttpPost]
        public async Task<ActionResult> LogIn(string email, string password)
        {
            try
            {
                await MvcApplication.DataManager.LogIn(email, password);
                MvcApplication.CustomerData = await MvcApplication.DataManager.GetCustomerData(email);
                return this.RedirectToAction("index", "UserRoom");
            }
            catch (RestException ex)
            {
                this.ViewBag.Result = ex.Message;
                this.ViewBag.Email = email;
                this.ViewBag.Password = password;
                return this.View();
            }
        }

        public ActionResult SignUp()
        {
            this.ViewBag.Message = "signup";
            return this.View();
        }

        [HttpPost]
        public async Task<ActionResult> SignUp(string email, string password, string phoneNumber, string firstName, string lastName, string organizationName, string customerTypeID)
        {
            CustomerData localCustomerData = new CustomerData
            {
                Email = email,
                Password = password,
                Phone = (long?)Convert.ToDouble(phoneNumber),
                CustomerType = Convert.ToInt32(customerTypeID) == 1 ? CustomerTypeEnum.Private.ToString() : CustomerTypeEnum.Organization.ToString()
            };
            if (localCustomerData.CustomerType == CustomerTypeEnum.Private.ToString())
            {
                localCustomerData.FirstName = firstName;
                localCustomerData.LastName = lastName;
            }
            else
            {
                localCustomerData.OrganizationName = organizationName;
            }

            try
            {
                await MvcApplication.DataManager.AddCustomerData(localCustomerData);
                MvcApplication.CustomerData = JsonConvert.DeserializeObject<CustomerData>(JsonConvert.SerializeObject(localCustomerData));
                return this.RedirectToAction("SignUpAcc", "Account");
            }
            catch (RestException ex)
            {
                this.ViewBag.Result = ex.Message; 
                return this.View();
            }
        }

        public ActionResult SignUpAcc()
        {
            this.ViewBag.Message = "signupAcc";
            return this.View();
        }

        [HttpPost]
        public async Task<ActionResult> SignUpAcc(string country, string town, string address, string postIndex, string sex, string dateTime)
        {
            this.ViewBag.Message = "signupAcc";
            CustomerData localCustomerData = JsonConvert.DeserializeObject<CustomerData>(JsonConvert.SerializeObject(MvcApplication.CustomerData));
            if (!string.IsNullOrWhiteSpace(country))
            {
                localCustomerData.Country = country;
            }

            if (!string.IsNullOrWhiteSpace(town))
            {
                localCustomerData.Town = town;
            }

            if (!string.IsNullOrWhiteSpace(address))
            {
                localCustomerData.Address = address;
            }

            if (!string.IsNullOrWhiteSpace(postIndex))
            {
                localCustomerData.PostIndex = postIndex;
            }

            switch (Convert.ToInt32(sex))
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

            if (!string.IsNullOrWhiteSpace(dateTime))
            {
                localCustomerData.BirthDate = Convert.ToDateTime(dateTime);
            }

            try
            {
                await MvcApplication.DataManager.ChangeCustomerData(localCustomerData);
                MvcApplication.CustomerData = localCustomerData;
                return this.RedirectToAction("SignUpPay", "Account");
            }
            catch (RestException ex)
            {
                this.ViewBag.Result = ex.Message;
                return this.View();
            }
        }

        public ActionResult SignUpPay()
        {
            this.ViewBag.Message = "signupPay";
            return this.View();
        }

        [HttpPost]
        public ActionResult SignUpPay(string incomingData)
        {
            this.ViewBag.Message = "signupPay";
            return this.RedirectToAction("index", "UserRoom");
        }
    }
}