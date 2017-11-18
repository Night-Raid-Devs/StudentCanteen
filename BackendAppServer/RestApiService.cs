using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;
using System.Text;
using System.Threading;
using Infocom.Chargers.BackendCommon;
using Infocom.Chargers.BackendDatabase;

namespace Infocom.Chargers.BackendAppServer
{
    public class RestApiService : IRestApiService
    {
        private static readonly string PasswordHashSalt = "made" + "_" + "by" + "_" + "Infocom" + "(c)";
        private static readonly int MaxUserSessionCount = 5;        // Max active sessions per user
        private static readonly int MaxAdminSessionCount = 1;       // Max active sessions per admin/operator
        private static readonly int MaxSessionLifeInMonths = 120;   // Life time of session in months
        private static readonly int DefaultMaxCount = 10000;        // Default Max returned records in one query
        private static Mutex sessionMutex = new Mutex();
        private static Dictionary<string, UserSession> sessionByToken = new Dictionary<string, UserSession>();
        private static Dictionary<long, HashSet<string>> sessionByCustomerId = new Dictionary<long, HashSet<string>>();

        public bool Ping()
        {
            return true;
        }

        public string Echo(string echo)
        {
            return echo.Length > 255 ? echo.Substring(0, 255) : echo;
        }

        public string EchoPost(string echo)
        {
            return echo.Length > 255 ? echo.Substring(0, 255) : echo;
        }

        public string EchoPut(string echo)
        {
            return echo.Length > 255 ? echo.Substring(0, 255) : echo;
        }

        #region Sessions

        public void Login(string phoneOrEmail, string password)
        {
            CustomerData customer;
            bool success = false;

            this.Logout();

            try
            {
                long phoneNumber = 0;

                try
                {
                    phoneNumber = Convert.ToInt64(phoneOrEmail);
                }
                catch
                {
                }

                if (phoneNumber > 0)
                {
                    customer = DatabaseManager.Instance.GetCustomer(phoneNumber, false);
                }
                else
                {
                    customer = DatabaseManager.Instance.GetCustomer(phoneOrEmail.Trim().ToLower(), false);
                }

                if (!string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(customer.Password) && customer.Password == this.GetPasswordHash(password))
                {
                    this.CreateSession(customer);
                    success = true;
                }
            }
            catch
            {
            }

            if (!success)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Неверный телефон/e-mail или пароль"), HttpStatusCode.Unauthorized);
            }
        }

        public void LoginRFID(string rfid)
        {
            CustomerData customer;
            bool success = false;

            this.Logout();

            try
            {
                customer = DatabaseManager.Instance.GetCustomerByRFID(rfid.Trim().ToUpper(), false);
                this.CreateSession(customer);
                success = true;
            }
            catch
            {
            }

            if (!success)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("RFID карта не существует или заблокирована"), HttpStatusCode.Unauthorized);
            }
        }

        public void Logout()
        {
            this.Logout(this.GetSessionToken());
        }

        public void Logout(string sessionToken)
        {
            if (!string.IsNullOrEmpty(sessionToken))
            {
                // Delete from database
                DatabaseManager.Instance.DeleteSession(sessionToken);

                // Delete from memory
                sessionMutex.WaitOne();
                try
                {
                    sessionByToken.Remove(sessionToken);
                }
                catch
                {
                }
                finally
                {
                    sessionMutex.ReleaseMutex();
                }
            }

            WebOperationContext.Current.OutgoingResponse.Headers[HttpResponseHeader.SetCookie] = "SessionToken=\"\"; Expires=Thu, 01-Jan-1970 00:00:10 GMT; Path=/";
        }

        // SHA256
        public string GetPasswordHash(string password)
        {
            StringBuilder str = new StringBuilder();

            using (SHA256 hash = SHA256.Create())
            {
                Encoding enc = Encoding.UTF8;
                byte[] result = hash.ComputeHash(enc.GetBytes(password + PasswordHashSalt));

                foreach (byte b in result)
                {
                    str.Append(b.ToString("x2"));
                }
            }

            return str.ToString();
        }

        public string GetSessionToken()
        {
            string coockie = WebOperationContext.Current.IncomingRequest.Headers[HttpRequestHeader.Cookie];
            string sessionToken = null;

            try
            {
                // Try to get session token from coockie
                if (!string.IsNullOrEmpty(coockie))
                {
                    var values = coockie.TrimEnd(';').Split(';');
                    foreach (var parts in values.Select(c => c.Split(new[] { '=' }, 2)))
                    {
                        if (parts.Length == 2 && parts[0].Trim() == "SessionToken")
                        {
                            sessionToken = parts[1];
                            break;
                        }
                    }
                }

                // Try to get session token from header
                if (string.IsNullOrEmpty(sessionToken))
                {
                    sessionToken = WebOperationContext.Current.IncomingRequest.Headers.Get("SessionToken");
                }
            }
            catch
            {
            }

            return sessionToken;
        }

        public UserSession GetUserSession(bool allowGuestAccess = false)
        {
            UserSession userSession = new UserSession();
            string sessionToken = this.GetSessionToken();

            try
            {
                // Try to find CustomerId using session token
                if (!string.IsNullOrEmpty(sessionToken))
                {
                    bool found = false;

                    sessionMutex.WaitOne();
                    try
                    {
                        found = sessionByToken.TryGetValue(sessionToken, out userSession);
                    }
                    catch
                    {
                    }
                    finally
                    {
                        sessionMutex.ReleaseMutex();
                    }

                    // Not found in memory, try to find in database
                    if (!found)
                    {
                        SessionData data = DatabaseManager.Instance.GetSesson(sessionToken);
                        userSession.CustomerId = data.CustomerId;
                        userSession.ExpirationDate = data.ExpirationDateEpochtime.HasValue ? data.ExpirationDateEpochtime.Value : DateTime.Now.ToEpochtime();

                        try
                        {
                            userSession.AccessRight = (AccessRightEnum)Enum.Parse(typeof(AccessRightEnum), data.AccessRights);
                        }
                        catch
                        {
                            userSession.AccessRight = AccessRightEnum.User;
                        }

                        // Add to memory
                        sessionMutex.WaitOne();
                        try
                        {
                            sessionByToken[sessionToken] = userSession;
                            if (!sessionByCustomerId.ContainsKey(userSession.CustomerId))
                            {
                                sessionByCustomerId[userSession.CustomerId] = new HashSet<string>() { sessionToken };
                            }
                            else
                            {
                                sessionByCustomerId[userSession.CustomerId].Add(sessionToken);
                            }
                        }
                        catch
                        {
                        }
                        finally
                        {
                            sessionMutex.ReleaseMutex();
                        }
                    }
                }
            }
            catch
            {
            }

            if (allowGuestAccess == false && (userSession.CustomerId == 0 || userSession.ExpirationDate < DateTime.Now.ToEpochtime()))
            {
                this.Logout(sessionToken);
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Время сессии истекло. Введите логин и пароль заново"), HttpStatusCode.Unauthorized);
            }

            return userSession;
        }

        // Search for user sessions
        public List<SessionData> SearchSessions(string lastId, string startTime, string stopTime, string accessRights, string ip, string phone, string email, string organizationName, string firstName, string middleName, string lastName, string limit)
        {
            UserSession session = this.GetUserSession();

            try
            {
                if (!session.IsAdmin())
                {
                    // User cannot search 
                    throw new Exception("Нет прав");
                }
                else
                {
                    long maxCount = Convert.ToInt64(limit);
                    if (maxCount <= 0)
                    {
                        maxCount = DefaultMaxCount;
                    }

                    long start = Convert.ToInt64(startTime);
                    long stop = Convert.ToInt64(stopTime);

                    return DatabaseManager.Instance.SearchSessions(lastId, start, stop, accessRights, ip, phone, email, organizationName, firstName, middleName, lastName, maxCount);
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }
        }

        #endregion 

        #region Customers

        public void CreateCustomer(CustomerData customer)
        {
            long who = 0;

            try
            {
                who = this.GetUserSession().CustomerId;
            }
            catch
            {
            }

            if (customer.Phone == null)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указан номер телефона"), HttpStatusCode.BadRequest);
            }

            if (customer.Email == null)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указан E-mail адрес"), HttpStatusCode.BadRequest);
            }

            if (customer.Password == null)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указан пароль"), HttpStatusCode.BadRequest);
            }

            if (customer.CustomerType == null)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указан тип клиента"), HttpStatusCode.BadRequest);
            }

            if (customer.OrganizationName == null)
            {
                if (customer.FirstName == null)
                {
                    throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указано имя"), HttpStatusCode.BadRequest);
                }

                if (customer.LastName == null)
                {
                    throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указана фамилия"), HttpStatusCode.BadRequest);
                }
            }

            if (customer.Language == null)
            {
                customer.Language = LanguageEnum.ru.ToString();
            }

            try
            {
                this.ValidateCustomerData(customer, new UserSession());

                customer.CreationDateEpochtime = customer.UpdateDateEpochtime;
                customer.Deleted = false;
                customer.Id = DatabaseManager.Instance.CreateCustomer(customer, who);
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }

            this.CreateSession(customer);
        }

        public void UpdateCustomer(CustomerData customer)
        {
            UserSession session = this.GetUserSession();

            try
            {
                if (session.IsRemoteStation())
                {
                    // Station cannot update its info as user, only admins can do it for station
                    throw new Exception("Нет прав");
                }

                this.ValidateCustomerData(customer, session);
                DatabaseManager.Instance.UpdateCustomer(customer, session.CustomerId);
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }
        }

        public CustomerData GetCustomer(string phoneOrEmail)
        {
            UserSession session = this.GetUserSession();
            CustomerData customer;

            try
            {
                if (!session.IsAdmin())
                {
                    customer = DatabaseManager.Instance.GetCustomerById(session.CustomerId, false);
                }
                else
                {
                    // Get user by phone or email with admin rights, show deleted cars
                    long phoneNumber = 0;

                    try
                    {
                        phoneNumber = Convert.ToInt64(phoneOrEmail);
                    }
                    catch
                    {
                    }

                    if (phoneNumber > 0)
                    {
                        customer = DatabaseManager.Instance.GetCustomer(phoneNumber, true);
                    }
                    else
                    {
                        customer = DatabaseManager.Instance.GetCustomer(phoneOrEmail.Trim().ToLower(), true);
                    }
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }

            return customer;
        }

        public List<CustomerData> SearchCustomers(string lastId, string phone, string email, string organizationName, string firstName, string middleName, string lastName, string limit)
        {
            UserSession session = this.GetUserSession();

            try
            {
                if (!session.IsAdmin())
                {
                    // User cannot search 
                    throw new Exception("Нет прав");
                }
                else
                {
                    long maxCount = Convert.ToInt64(limit);
                    if (maxCount <= 0)
                    {
                        maxCount = DefaultMaxCount;
                    }

                    return DatabaseManager.Instance.SearchCustomers(lastId, phone, email, organizationName, firstName, middleName, lastName, maxCount);
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }
        }

        public void DeleteCustomer(string id, string purge)
        {
            UserSession session = this.GetUserSession();

            try
            {
                if (!session.IsAdmin())
                {
                    // User cannot delete 
                    throw new Exception("Нет прав");
                }
                else
                {
                    // Admin can delete any user
                    CustomerData customer = new CustomerData();
                    customer.Id = Convert.ToInt64(id);

                    if (string.IsNullOrEmpty(purge) == false && purge.ToLower() == "true")
                    {
                        DatabaseManager.Instance.PurgeCustomer(customer, session.CustomerId);
                    }
                    else
                    {
                        DatabaseManager.Instance.DeleteCustomer(customer, session.CustomerId);
                    }

                    DatabaseManager.Instance.DeleteSession(customer.Id);

                    // Remove sessions of this user from memory (if it is needed they will be read from database)
                    sessionMutex.WaitOne();
                    try
                    {
                        if (sessionByCustomerId.ContainsKey(customer.Id))
                        {
                            foreach (string token in sessionByCustomerId[customer.Id])
                            {
                                sessionByToken.Remove(token);
                            }

                            sessionByCustomerId[customer.Id].Clear();
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        sessionMutex.ReleaseMutex();
                    }
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }
        }

        public void ValidateCustomerData(CustomerData customer, UserSession session)
        {
            // For users we allow to create/update only specific fields and only for the current user
            if (!session.IsAdmin())
            {
                // User
                customer.Id = session.CustomerId;
                customer.AccessRights = session.AccessRight.ToString();
                customer.Comments = null;
                customer.Deleted = null;
            }
            else
            {
                // Admin
                if (customer.AccessRights != null)
                {
                    if (!Enum.IsDefined(typeof(AccessRightEnum), customer.AccessRights))
                    {
                        customer.AccessRights = AccessRightEnum.User.ToString();
                    }
                }
            }

            customer.UpdateDateEpochtime = DateTime.Now.ToEpochtime();
            customer.CreationDateEpochtime = null;

            if (customer.Phone != null && (customer.Phone.Value < 1000000000 || customer.Phone.Value > 999999999999))
            {
                throw new Exception("Неверно указан номер телефона. Номер телефона должен содержать код страны, например +380681234567");
            }

            if (customer.Email != null)
            {
                customer.Email = customer.Email.Trim().ToLower();
                if (!new EmailAddressAttribute().IsValid(customer.Email))
                {
                    throw new Exception("Неверно указан E-mail адрес");
                }
            }

            if (customer.Password != null)
            {
                if (customer.Password.Length < 5)
                {
                    throw new Exception("Минимальная длина пароля 5 символов");
                }

                customer.Password = this.GetPasswordHash(customer.Password);
            }

            if (customer.CustomerType != null)
            {
                if (!Enum.IsDefined(typeof(CustomerTypeEnum), customer.CustomerType))
                {
                    customer.CustomerType = CustomerTypeEnum.Private.ToString();
                }
            }

            if (customer.Language != null)
            {
                if (!Enum.IsDefined(typeof(LanguageEnum), customer.Language))
                {
                    customer.Language = LanguageEnum.ru.ToString();
                }
            }

            if (customer.OrganizationName != null)
            {
                customer.OrganizationName = customer.OrganizationName.Trim();
            }

            if (customer.FirstName != null)
            {
                customer.FirstName = customer.FirstName.Trim();
                if (string.IsNullOrEmpty(customer.OrganizationName) && customer.FirstName.Length == 0)
                {
                    throw new Exception("Не указано имя");
                }
            }

            if (customer.LastName != null)
            {
                customer.LastName = customer.LastName.Trim();
                if (string.IsNullOrEmpty(customer.OrganizationName) && customer.LastName.Length == 0)
                {
                    throw new Exception("Не указана фамилия");
                }
            }

            if (customer.MiddleName != null)
            {
                customer.MiddleName = customer.MiddleName.Trim();
            }

            if (customer.Country != null)
            {
                customer.Country = customer.Country.Trim();
            }

            if (customer.Region != null)
            {
                customer.Region = customer.Region.Trim();
            }

            if (customer.Town != null)
            {
                customer.Town = customer.Town.Trim();
            }

            if (customer.PostIndex != null)
            {
                // allow only 'a'..'z' 'A'..'Z', '0'..'9'
                customer.PostIndex = new string(customer.PostIndex.Where(c => char.IsLetterOrDigit(c)).ToArray()).Trim();
            }

            if (customer.Address != null)
            {
                customer.Address = customer.Address.Trim();
            }

            if (customer.SecretAnswer != null)
            {
                customer.SecretAnswer = customer.SecretAnswer.Trim();
            }
        }

        #endregion 

        #region Cars

        public long CreateCar(CarData car)
        {
            UserSession session = this.GetUserSession();

            if (car.Brand == null)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указана марка(бренд) авто"), HttpStatusCode.BadRequest);
            }

            if (car.RegNumber == null)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указан регистрационный номер авто"), HttpStatusCode.BadRequest);
            }

            try
            {
                if (session.IsRemoteStation() || session.IsStationOperator())
                {
                    // Station cannot create cars
                    throw new Exception("Нет прав");
                }

                this.ValidateCarData(car, session);
                car.Deleted = false;

                return DatabaseManager.Instance.CreateCar(car, session.CustomerId);
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }
        }

        public void UpdateCar(CarData car)
        {
            UserSession session = this.GetUserSession();

            try
            {
                if (session.IsRemoteStation() || session.IsStationOperator())
                {
                    // Station cannot update cars
                    throw new Exception("Нет прав");
                }

                this.ValidateCarData(car, session);
                DatabaseManager.Instance.UpdateCar(car, session.CustomerId);
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }
        }

        public List<CarData> GetCars(string customerId)
        {
            UserSession session = this.GetUserSession();
            List<CarData> carList;

            try
            {
                if (!session.IsAdmin())
                {
                    carList = DatabaseManager.Instance.GetCars(session.CustomerId, false);
                }
                else
                {
                    // Get car of any user with admin rights
                    carList = DatabaseManager.Instance.GetCars(Convert.ToInt64(customerId), true);
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }

            return carList;
        }

        public void DeleteCar(string id, string customerId, string purge)
        {
            UserSession session = this.GetUserSession();

            try
            {
                if (session.IsRemoteStation() || session.IsStationOperator())
                {
                    // Station cannot delete cars
                    throw new Exception("Нет прав");
                }

                CarData car = new CarData();
                car.Id = Convert.ToInt64(id);

                if (!session.IsAdmin())
                {
                    // User can delete only its cars
                    car.CustomerId = session.CustomerId;
                    DatabaseManager.Instance.DeleteCar(car, session.CustomerId);
                }
                else 
                {
                    // Admin can delete cars from any user
                    car.CustomerId = Convert.ToInt64(customerId);

                    if (string.IsNullOrEmpty(purge) == false && purge.ToLower() == "true")
                    {
                        DatabaseManager.Instance.PurgeCar(car, session.CustomerId);
                    }
                    else
                    {
                        DatabaseManager.Instance.DeleteCar(car, session.CustomerId);
                    }
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }
        }

        public void ValidateCarData(CarData car, UserSession session)
        {
            // for users we allow to create/update only specific fields and only for the current user
            if (!session.IsAdmin())
            {
                // User
                car.CustomerId = session.CustomerId;
                car.Comments = null;
                car.Deleted = false;        // Restore deleted
            }
            else
            {
                // Admin
            }

            car.UpdateDateEpochtime = DateTime.Now.ToEpochtime();

            if (car.Brand != null)
            {
                car.Brand = car.Brand.Trim();
                if (car.Brand.Length == 0)
                {
                    throw new Exception("Не указана марка(бренд) авто");
                }
            }

            if (car.Model != null)
            {
                car.Model = car.Model.Trim();
            }

            if (car.Year != null && (car.Year.Value < 1900 || car.Year.Value > DateTime.Now.Year + 1))
            {
                throw new Exception("Неверно указан год производства авто");
            }

            if (car.RegNumber != null)
            {
                car.RegNumber = car.RegNumber.Trim();
                if (car.RegNumber.Length == 0)
                {
                    throw new Exception("Не указан регистрационный номер авто");
                }
            }

            if (car.VIN != null)
            {
                // allow only 'a'..'z' 'A'..'Z', '0'..'9'
                car.VIN = new string(car.VIN.Where(c => char.IsLetterOrDigit(c)).ToArray()).Trim();
                car.VIN = car.VIN.ToUpper();
            }
        }

        #endregion

        #region RFIDs

        public long CreateRFID(RFIDData rfid)
        {
            UserSession session = this.GetUserSession();

            if (rfid.Value == null)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указано значение RFID карты"), HttpStatusCode.BadRequest);
            }

            if (rfid.Blocked == null)
            {
                rfid.Blocked = false;
            }

            try
            {
                this.ValidateRFIDData(rfid, session);

                if (!session.IsAdmin())
                {
                    // User cannot create
                    throw new Exception("Нет прав");
                }

                rfid.CreationDateEpochtime = DateTime.Now.ToEpochtime();
                rfid.Deleted = false;
                return DatabaseManager.Instance.CreateRFID(rfid, session.CustomerId);
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }
        }

        public void UpdateRFID(RFIDData rfid)
        {
            UserSession session = this.GetUserSession();

            try
            {
                this.ValidateRFIDData(rfid, session);

                DatabaseManager.Instance.UpdateRFID(rfid, session.CustomerId);
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }
        }

        public List<RFIDData> GetRFIDs(string customerId)
        {
            UserSession session = this.GetUserSession();
            List<RFIDData> rfidList;

            try
            {
                if (!session.IsAdmin())
                {
                    rfidList = DatabaseManager.Instance.GetRFIDs(session.CustomerId, false);
                }
                else
                {
                    // Get RFIDs of any user with admin rights
                    rfidList = DatabaseManager.Instance.GetRFIDs(Convert.ToInt64(customerId), true);
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }

            return rfidList;
        }

        public List<RFIDData> SearchRFIDs(string lastId, string value, string limit)
        {
            UserSession session = this.GetUserSession();

            try
            {
                if (!session.IsAdmin())
                {
                    // User cannot search 
                    throw new Exception("Нет прав");
                }
                else
                {
                    long maxCount = Convert.ToInt64(limit);
                    if (maxCount <= 0)
                    {
                        maxCount = DefaultMaxCount;
                    }

                    return DatabaseManager.Instance.SearchRFIDs(lastId, value.ToUpper(), maxCount);
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }
        }

        public void DeleteRFID(string id, string customerId, string purge)
        {
            UserSession session = this.GetUserSession();

            try
            {
                if (!session.IsAdmin())
                {
                    // User cannot delete
                    throw new Exception("Нет прав");
                }
                else
                {
                    // Admin can delete RFIDs from any user
                    RFIDData rfid = new RFIDData();
                    rfid.Id = Convert.ToInt64(id);
                    rfid.CustomerId = Convert.ToInt64(customerId);

                    if (string.IsNullOrEmpty(purge) == false && purge.ToLower() == "true")
                    {
                        DatabaseManager.Instance.PurgeRFID(rfid, session.CustomerId);
                    }
                    else
                    {
                        DatabaseManager.Instance.DeleteRFID(rfid, session.CustomerId);
                    }
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }
        }

        public void ValidateRFIDData(RFIDData rfid, UserSession session)
        {
            // for users we allow to update only specific fields (Blocked) and only for the current user
            if (!session.IsAdmin())
            {
                // User
                rfid.CustomerId = session.CustomerId;
                rfid.Value = null;
                rfid.CreationDateEpochtime = null;
                rfid.Comments = null;
                rfid.Deleted = null;
            }
            else
            {
                // Admin
                rfid.CreationDateEpochtime = null;
            }

            if (rfid.Value != null)
            {
                // allow only 'A'..'H', '0'..'9'
                rfid.Value = rfid.Value.ToUpper();
                rfid.Value = new string(rfid.Value.Where(c => char.IsNumber(c) || (c >= 'A' && c <= 'H')).ToArray()).Trim();

                if (rfid.Value.Length == 0)
                {
                    throw new Exception("Не указано значение RFID карты");
                }
            }
        }

        #endregion

        #region Stations

        public long CreateStation(StationData station)
        {
            UserSession session = this.GetUserSession();

            if (station.Name == null)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указано название зарядной станции"), HttpStatusCode.BadRequest);
            }

            if (station.Latitude == null)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указана широта в координатах"), HttpStatusCode.BadRequest);
            }

            if (station.Longitude == null)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указана долгота в координатах"), HttpStatusCode.BadRequest);
            }

            if (station.NetworkName == null)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указано название сети зарядных станций"), HttpStatusCode.BadRequest);
            }

            if (station.Country == null)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указана страна"), HttpStatusCode.BadRequest);
            }

            if (station.Region == null)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указан регион"), HttpStatusCode.BadRequest);
            }

            if (station.Town == null)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указан город"), HttpStatusCode.BadRequest);
            }

            if (station.Address == null)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указана улица и дом в адресе"), HttpStatusCode.BadRequest);
            }

            if (station.AccessType == null)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указан тип доступа к данной станции"), HttpStatusCode.BadRequest);
            }

            if (station.Status == null)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указано состояние зарядной станции"), HttpStatusCode.BadRequest);
            }

            try
            {
                this.ValidateStationData(station, session);

                if (!session.IsAdmin())
                {
                    // User cannot create 
                    throw new Exception("Нет прав");
                }
                else
                {
                    // Admin can create
                    station.CreationDateEpochtime = station.UpdateDateEpochtime;
                    station.Deleted = false;
                    return DatabaseManager.Instance.CreateStation(station, session.CustomerId);
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }
        }

        public void UpdateStation(StationData station)
        {
            UserSession session = this.GetUserSession();

            try
            {
                this.ValidateStationData(station, session);

                if (!session.IsAdmin() && !session.IsStationOperator())
                {
                    // User cannot update 
                    throw new Exception("Нет прав");
                }
                else
                {
                    // Admin can update
                    DatabaseManager.Instance.UpdateStation(station, session.CustomerId);
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }
        }

        public StationData GetStation(string id)
        {
            UserSession session = this.GetUserSession();
            StationData station;

            try
            {
                // Admin can also see deleted
                station = DatabaseManager.Instance.GetStation(Convert.ToInt64(id), session.IsAdmin());
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }

            return station;
        }

        public List<StationData> SearchStations(string lastId, string ignoreIds, string type, string latitude, string longitude, string latitude2, string longitude2, string distance, string country, string region, string town, string accessTypes, string stationStatuses, string portTypes, string portLevels, string portStatuses, string limit)
        {
            UserSession session = this.GetUserSession(true);       // Allow guest access
            List<StationData> stations;
            long[] ig = null;
            string[] at = null;
            string[] ss = null;
            string[] pt = null;
            string[] pl = null;
            string[] ps = null;
            double minLatitude = 0;
            double maxLatitude = 0;
            double minLongitude = 0;
            double maxLongitude = 0;
            double d = 0;

            try
            {
                // MaxCount
                long maxCount = Convert.ToInt64(limit);
                if (maxCount <= 0)
                {
                    maxCount = DefaultMaxCount;
                }

                // IgnoreIds
                if (!string.IsNullOrEmpty(ignoreIds))
                {
                    string[] ignoreSplitted = ignoreIds.Split(',');
                    ig = new long[ignoreSplitted.Length];
                    int i = 0;

                    foreach (var id in ignoreSplitted)
                    {
                        ig[i++] = Convert.ToInt64(id);
                    }
                }

                // AccessTypeEnum
                if (!string.IsNullOrEmpty(accessTypes))
                {
                    at = accessTypes.Split(',');
                }

                // StationStatusEnum
                if (!string.IsNullOrEmpty(stationStatuses))
                {
                    ss = stationStatuses.Split(',');
                }

                // PortTypes
                if (!string.IsNullOrEmpty(portTypes))
                {
                    pt = portTypes.Split(',');
                }

                // PortLevels
                if (!string.IsNullOrEmpty(portLevels))
                {
                    pl = portLevels.Split(',');
                }

                // PortStatusEnum
                if (!string.IsNullOrEmpty(portStatuses))
                {
                    ps = portStatuses.Split(',');
                }

                // Type
                switch (type)
                {
                    case "map":
                        minLatitude = Convert.ToDouble(latitude);
                        maxLatitude = Convert.ToDouble(latitude2);
                        minLongitude = Convert.ToDouble(longitude);
                        maxLongitude = Convert.ToDouble(longitude2);

                        // Swap
                        if (minLatitude > maxLatitude)
                        {
                            double t = minLatitude;
                            minLatitude = maxLatitude;
                            maxLatitude = t;
                        }

                        if (minLongitude > maxLongitude)
                        {
                            double t = minLongitude;
                            minLongitude = maxLongitude;
                            maxLongitude = t;
                        }

                        // Admin can also see deleted
                        stations = DatabaseManager.Instance.SearchStationsOnMap(lastId, ig, minLatitude, maxLatitude, minLongitude, maxLongitude, at, ss, pt, pl, ps, session.IsAdmin(), maxCount);
                        break;

                    case "town":
                        // Admin can also see deleted
                        stations = DatabaseManager.Instance.SearchStationsInTown(lastId, country, region, town, at, ss, pt, pl, ps, session.IsAdmin(), maxCount);
                        break;

                    case "road":
                        // Coordinates and distance
                        d = Convert.ToDouble(distance);

                        if (d <= 0.0)
                        {
                            throw new Exception("Плохой параметр d={distance}");
                        }

                        minLatitude = Convert.ToDouble(latitude);
                        minLongitude = Convert.ToDouble(longitude);

                        // Convert meters to latitude and longitude
                        d = d * 8.983152841e-6;
                        double d2 = d / Math.Cos(minLatitude * 0.01745329252);
                        maxLatitude = minLatitude + d;
                        minLatitude = minLatitude - d;
                        maxLongitude = minLongitude + d2;
                        minLongitude = minLongitude - d2;

                        // Admin can also see deleted
                        stations = DatabaseManager.Instance.SearchStationsOnMap(lastId, ig, minLatitude, maxLatitude, minLongitude, maxLongitude, at, ss, pt, pl, ps, session.IsAdmin(), maxCount);
                        break;

                    default:
                        throw new Exception("Неизвестное значение для параметра t={type}");
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }

            return stations;
        }

        public void DeleteStation(string id, string purge)
        {
            UserSession session = this.GetUserSession();

            try
            {
                if (!session.IsAdmin())
                {
                    // User cannot delete 
                    throw new Exception("Нет прав");
                }
                else
                {
                    // Admin can delete
                    StationData station = new StationData();
                    station.Id = Convert.ToInt64(id);

                    if (string.IsNullOrEmpty(purge) == false && purge.ToLower() == "true")
                    {
                        DatabaseManager.Instance.PurgeStation(station, session.CustomerId);
                    }
                    else
                    {
                        DatabaseManager.Instance.DeleteStation(station, session.CustomerId);
                    }
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }
        }

        public void ValidateStationData(StationData station, UserSession session)
        {
            if (!session.IsAdmin())
            {
                // User cannot change
                throw new Exception("Нет прав");
            }
            else
            {
                // Admin
            }

            station.UpdateDateEpochtime = DateTime.Now.ToEpochtime();
            station.CreationDateEpochtime = null;

            if (station.Name != null)
            {
                station.Name = station.Name.Trim();
                if (station.Name.Length == 0)
                {
                    throw new Exception("Не указано название сети зарядных станций");
                }
            }

            if (station.Description != null)
            {
                station.Description = station.Description.Trim();
            }

            if (station.Latitude != null)
            {
                if (station.Latitude == 0.0 || station.Latitude > 90.0 || station.Latitude < -90.0)
                {
                    throw new Exception("Не верно указана широта в координатах");
                }
            }

            if (station.Longitude != null)
            {
                if (station.Longitude == 0.0 || station.Longitude > 180.0 || station.Longitude < -180.0)
                {
                    throw new Exception("Не верно указана долгота в координатах");
                }
            }

            if (station.InfoMessage != null)
            {
                station.InfoMessage = station.InfoMessage.Trim();
            }

            if (station.NetworkName != null)
            {
                station.NetworkName = station.NetworkName.Trim();
                if (station.NetworkName.Length == 0)
                {
                    throw new Exception("Не указано название сети зарядных станций");
                }
            }

            if (station.Phone != null && (station.Phone.Value < 1000000000 || station.Phone.Value > 999999999999))
            {
                throw new Exception("Неверно указан номер телефона. Номер телефона должен содержать код страны, например +380681234567");
            }

            if (station.Country != null)
            {
                station.Country = station.Country.Trim();
                if (station.Country.Length == 0)
                {
                    throw new Exception("Не указана страна");
                }
            }

            if (station.Region != null)
            {
                station.Region = station.Region.Trim();
                if (station.Region.Length == 0)
                {
                    throw new Exception("Не указан регион");
                }
            }

            if (station.Town != null)
            {
                station.Town = station.Town.Trim();
                if (station.Town.Length == 0)
                {
                    throw new Exception("Не указан город");
                }
            }

            if (station.PostIndex != null)
            {
                // allow only 'a'..'z' 'A'..'Z', '0'..'9'
                station.PostIndex = new string(station.PostIndex.Where(c => char.IsLetterOrDigit(c)).ToArray()).Trim();
            }

            if (station.Address != null)
            {
                station.Address = station.Address.Trim();
                if (station.Address.Length == 0)
                {
                    throw new Exception("Не указана улица и дом в адресе");
                }
            }

            if (station.Web != null)
            {
                station.Web = station.Web.Trim();
            }

            if (station.OpenHours != null)
            {
                station.OpenHours = station.OpenHours.Trim();
            }

            if (station.AccessType != null)
            {
                if (!Enum.IsDefined(typeof(AccessTypeEnum), station.AccessType))
                {
                    throw new Exception("Не верно указан тип доступа к данной станции");
                }
            }

            if (station.PaymentType != null)
            {
                station.PaymentType = station.PaymentType.Trim();
            }

            if (station.Status != null)
            {
                if (!Enum.IsDefined(typeof(StationStatusEnum), station.Status))
                {
                    throw new Exception("Не верно указано состояние зарядной станции");
                }
            }
        }

        #endregion

        #region Tariff

        public long CreateTariff(TariffData tariff)
        {
            UserSession session = this.GetUserSession();

            if (tariff.Name == null)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указано название тарифа"), HttpStatusCode.BadRequest);
            }

            if (tariff.Description == null)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указано описание тарифа"), HttpStatusCode.BadRequest);
            }

            if (tariff.PaymentReqired == null)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указано бесплатный или платный тариф"), HttpStatusCode.BadRequest);
            }

            if (tariff.PaymentReqired.Value)
            {
                if (tariff.PriceType == null)
                {
                    throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указан тип цены"), HttpStatusCode.BadRequest);
                }

                if (tariff.Price == null)
                {
                    throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указана цена"), HttpStatusCode.BadRequest);
                }

                if (tariff.Currency == null)
                {
                    throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указана валюта"), HttpStatusCode.BadRequest);
                }
            }

            try
            {
                if (!session.IsAdmin())
                {
                    // User cannot create 
                    throw new Exception("Нет прав");
                }
                else
                {
                    // Admin can create
                    this.ValidateTariffData(tariff);
                    tariff.Deleted = false;
                    return DatabaseManager.Instance.CreateTariff(tariff, session.CustomerId);
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }
        }

        public void UpdateTariff(TariffData tariff)
        {
            UserSession session = this.GetUserSession();

            try
            {
                if (!session.IsAdmin())
                {
                    // User cannot update 
                    throw new Exception("Нет прав");
                }
                else
                {
                    // Admin can update
                    this.ValidateTariffData(tariff);
                    DatabaseManager.Instance.UpdateTariff(tariff, session.CustomerId);
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }
        }

        public TariffData GetTariff(string id)
        {
            UserSession session = this.GetUserSession();
            TariffData tariff;

            try
            {
                // Admin can also see deleted
                tariff = DatabaseManager.Instance.GetTariff(Convert.ToInt64(id), session.IsAdmin());
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }

            return tariff;
        }

        public List<TariffData> SearchTariffs(string lastId, string name, string description, string limit)
        {
            UserSession session = this.GetUserSession();

            try
            {
                if (!session.IsAdmin())
                {
                    // User cannot search 
                    throw new Exception("Нет прав");
                }
                else
                {
                    long maxCount = Convert.ToInt64(limit);
                    if (maxCount <= 0)
                    {
                        maxCount = DefaultMaxCount;
                    }

                    return DatabaseManager.Instance.SearchTariffs(lastId, name, description, maxCount);
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }
        }

        public void DeleteTariff(string id, string purge)
        {
            UserSession session = this.GetUserSession();

            try
            {
                if (!session.IsAdmin())
                {
                    // User cannot delete 
                    throw new Exception("Нет прав");
                }
                else
                {
                    // Admin can delete
                    TariffData tariff = new TariffData();
                    tariff.Id = Convert.ToInt64(id);

                    if (string.IsNullOrEmpty(purge) == false && purge.ToLower() == "true")
                    {
                        DatabaseManager.Instance.PurgeTariff(tariff, session.CustomerId);
                    }
                    else
                    {
                        DatabaseManager.Instance.DeleteTariff(tariff, session.CustomerId);
                    }
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }
        }

        public void ValidateTariffData(TariffData tariff)
        {
            if (tariff.Name != null)
            {
                tariff.Name = tariff.Name.Trim();
                if (tariff.Name.Length == 0)
                {
                    throw new Exception("Не указано название тарифа");
                }
            }

            if (tariff.Description != null)
            {
                tariff.Description = tariff.Description.Trim();
                if (tariff.Description.Length == 0)
                {
                    throw new Exception("Не указано описание тарифа");
                }
            }

            if (tariff.PriceType != null && !Enum.IsDefined(typeof(PriceTypeEnum), tariff.PriceType))
            {
                throw new Exception("Не верно указан тип цены");
            }

            if (tariff.Price != null && tariff.Price < 0.0)
            {
                throw new Exception("Цена не может быть отрицательной");
            }

            if (tariff.Currency != null)
            {
                if (!Enum.IsDefined(typeof(CurrencyEnum), tariff.Currency))
                {
                    throw new Exception("Не верно указан код валюты");
                }
            }
        }

        #endregion

        #region private

        private string GetIP()
        {
            OperationContext context = OperationContext.Current;
            MessageProperties prop = context.IncomingMessageProperties;
            RemoteEndpointMessageProperty endpoint = prop[RemoteEndpointMessageProperty.Name] as RemoteEndpointMessageProperty;
            return endpoint.Address;
        }

        private void CreateSession(CustomerData customer)
        {
            SessionData session = new SessionData();

            session.SessionToken = new string(Guid.NewGuid().ToString().Where(c => char.IsLetterOrDigit(c)).ToArray());
            session.CustomerId = customer.Id;
            session.AccessRights = customer.AccessRights;
            session.IP = this.GetIP();
            session.ExpirationDateEpochtime = DateTime.Now.AddMonths(MaxSessionLifeInMonths).ToEpochtime();
            session.UserAgent = WebOperationContext.Current.IncomingRequest.UserAgent;

            try
            {
                DatabaseManager.Instance.CreateSesson(
                    session,
                    (customer.AccessRights == AccessRightEnum.Admin.ToString() || customer.AccessRights == AccessRightEnum.StationOperator.ToString()) ? MaxAdminSessionCount : MaxUserSessionCount);
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.Unauthorized);
            }

            // Add this session to memory and remove the others from memory (if it is needed they will be read from database)
            UserSession userSession = new UserSession();
            userSession.CustomerId = session.CustomerId;
            userSession.ExpirationDate = session.ExpirationDateEpochtime.Value;

            try
            {
                userSession.AccessRight = (AccessRightEnum)Enum.Parse(typeof(AccessRightEnum), session.AccessRights);
            }
            catch
            {
                userSession.AccessRight = AccessRightEnum.User;
            }

            sessionMutex.WaitOne();
            try
            {
                if (sessionByCustomerId.ContainsKey(session.CustomerId))
                {
                    foreach (string token in sessionByCustomerId[session.CustomerId])
                    {
                        sessionByToken.Remove(token);
                    }

                    sessionByCustomerId[session.CustomerId].Clear();
                    sessionByCustomerId[session.CustomerId].Add(session.SessionToken);
                }
                else
                {
                    sessionByCustomerId[session.CustomerId] = new HashSet<string>() { session.SessionToken };
                }

                sessionByToken[session.SessionToken] = userSession;
            }
            catch
            {
            }
            finally
            {
                sessionMutex.ReleaseMutex();
            }

            WebOperationContext.Current.OutgoingResponse.Headers[HttpResponseHeader.SetCookie] = string.Format(
                    "SessionToken={0}; Expires={1}; Path=/; HttpOnly",
                    session.SessionToken,
                    Epochtime.ToDateTimeUTC(session.ExpirationDateEpochtime.Value).ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture));
        }

        #endregion 

        public struct UserSession
        {
            public long CustomerId;
            public AccessRightEnum AccessRight;
            public long ExpirationDate;

            public UserSession(long customerId, AccessRightEnum accessRight, long expirationDate)
            {
                this.CustomerId = customerId;
                this.AccessRight = accessRight;
                this.ExpirationDate = expirationDate;
            }

            public bool IsAdmin()
            {
                return this.AccessRight == AccessRightEnum.Admin;
            }

            public bool IsRemoteStation()
            {
                return this.AccessRight == AccessRightEnum.RemoteStation;
            }

            public bool IsStationOperator()
            {
                return this.AccessRight == AccessRightEnum.StationOperator;
            }
        }
    }
}