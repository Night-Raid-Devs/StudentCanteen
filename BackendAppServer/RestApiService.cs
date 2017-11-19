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
using BackendCommon;
using BackendDatabase;

namespace BackendAppServer
{
    public class RestApiService : IRestApiService
    {
        private static readonly string PasswordHashSalt = "made" + "_" + "by" + "_" + "Infocom" + "(c)";
        private static readonly int MaxUserSessionCount = 5;        // Max active sessions per user
        private static readonly int MaxAdminSessionCount = 1;       // Max active sessions per admin/operator
        private static readonly int MaxSessionLifeInMonths = 120;   // Life time of session in months
        private static Mutex sessionMutex = new Mutex();
        private static Dictionary<string, UserSession> sessionByToken = new Dictionary<string, UserSession>();
        private static Dictionary<long, HashSet<string>> sessionByCustomerId = new Dictionary<long, HashSet<string>>();

        #region Sessions

        public void Login(string login, string password)
        {
            CustomerData customer;
            bool success = false;

            this.Logout();

            try
            {
                customer = DatabaseManager.Instance.GetCustomer(login.Trim());
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
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Неверный логин или пароль"), HttpStatusCode.Unauthorized);
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

        #endregion 

        #region Customers

        public void CreateCustomer(CustomerData customer)
        {
            if (customer.Login == null)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указан логин"), HttpStatusCode.BadRequest);
            }

            if (customer.Password == null)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указан пароль"), HttpStatusCode.BadRequest);
            }

            if (customer.FirstName == null)
            {
                    throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указано имя"), HttpStatusCode.BadRequest);
            }

            if (customer.LastName == null)
            {
                    throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указана фамилия"), HttpStatusCode.BadRequest);
            }

            try
            {
                this.ValidateCustomerData(customer, new UserSession());

                customer.Deleted = false;
                customer.Id = DatabaseManager.Instance.CreateCustomer(customer);
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }

            this.CreateSession(customer);
        }

        public CustomerData GetCustomer(string login)
        {
            UserSession session = this.GetUserSession();
            CustomerData customer;

            try
            {
                customer = DatabaseManager.Instance.GetCustomer(login.Trim());
                if (!session.IsAdmin() && session.CustomerId != customer.Id)
                {
                    throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Нет прав"), HttpStatusCode.BadRequest);
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }

            return customer;
        }

        public List<CustomerData> GetCustomers()
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
                    return DatabaseManager.Instance.GetCustomers();
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }
        }

        public void DeleteCustomer(string id)
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
                    DatabaseManager.Instance.DeleteCustomer(customer);
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

            if (customer.Login != null)
            {
                customer.Login = customer.Login.Trim();
                if (customer.Login.Length == 0)
                {
                    throw new Exception("Не указан логин");
                }

                foreach (char c in customer.Login)
                {
                    if (!char.IsLetterOrDigit(c))
                    {
                        throw new Exception("Пароль содержит недопустимые символы");
                    }
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

            if (customer.FirstName != null)
            {
                customer.FirstName = customer.FirstName.Trim();
                if (customer.FirstName.Length == 0)
                {
                    throw new Exception("Не указано имя");
                }
            }

            if (customer.LastName != null)
            {
                customer.LastName = customer.LastName.Trim();
                if (customer.LastName.Length == 0)
                {
                    throw new Exception("Не указана фамилия");
                }
            }
        }

        #endregion

        #region Dishes

        public List<long> CreateDishes(List<DishData> dishes)
        {
            UserSession session = this.GetUserSession();
            List<long> dishesIds = new List<long>();
            foreach (DishData dish in dishes)
            {
                if (dish.Name == null)
                {
                    throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указано название блюда"), HttpStatusCode.BadRequest);
                }

                if (dish.DishType == null)
                {
                    throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указан тип блюда"), HttpStatusCode.BadRequest);
                }

                if (dish.ValidDateEpochtime == null)
                {
                    throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указана дата доступности блюда"), HttpStatusCode.BadRequest);
                }
            }

            try
            {
                if (!session.IsAdmin())
                {
                    // Station cannot create cars
                    throw new Exception("Нет прав");
                }

                foreach (DishData dish in dishes)
                {
                    this.ValidateDishData(dish, session);
                    dish.Deleted = false;

                    dishesIds.Add(DatabaseManager.Instance.CreateDish(dish));
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }

            return dishesIds;
        }

        public void UpdateDishes(List<DishData> dishes)
        {
            UserSession session = this.GetUserSession();
            try
            {
                if (!session.IsAdmin())
                {
                    // Station cannot create cars
                    throw new Exception("Нет прав");
                }

                foreach (DishData dish in dishes)
                {
                    this.ValidateDishData(dish, session);
                    DatabaseManager.Instance.UpdateDish(dish);
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }
        }

        public List<DishData> GetDishes(string customerId, string startDate, string endDate)
        {
            UserSession session = this.GetUserSession();
            List<DishData> dishList;
            try
            {
                long startDateLong = Convert.ToInt64(startDate);
                long endDateLong = Convert.ToInt64(endDate);
                if (!session.IsAdmin())
                {
                    dishList = DatabaseManager.Instance.GetDishes(session.CustomerId, startDateLong, endDateLong);
                }
                else
                {
                    // Get car of any user with admin rights
                    dishList = DatabaseManager.Instance.GetDishes(Convert.ToInt64(customerId), startDateLong, endDateLong);
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }

            return dishList;
        }

        public void ValidateDishData(DishData dish, UserSession session)
        {
            if (dish.Name != null)
            {
                dish.Name = dish.Name.Trim();
                if (dish.Name.Length == 0)
                {
                    throw new Exception("Не указано название блюда");
                }
            }

            if (dish.DishType != null)
            {
                if (!Enum.IsDefined(typeof(DishType), dish.DishType))
                {
                    throw new Exception("Не верно указан тип блюда");
                }
            }
        }

        #endregion

        #region Orders

        public List<long> CreateOrders(List<OrderData> orders)
        {
            UserSession session = this.GetUserSession();
            List<long> ordersIds = new List<long>();
            foreach (OrderData order in orders)
            {
                if (order.CustomerId == null)
                {
                    throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указан id пользователя"), HttpStatusCode.BadRequest);
                }

                if (order.DishId == null)
                {
                    throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указан id блюда"), HttpStatusCode.BadRequest);
                }

                if (order.Count == null)
                {
                    throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage("Не указано количество порций"), HttpStatusCode.BadRequest);
                }
            }

            try
            {
                foreach (OrderData order in orders)
                {
                    this.ValidateOrderData(order, session);
                    order.Deleted = false;

                    ordersIds.Add(DatabaseManager.Instance.CreateOrder(order));
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }

            return ordersIds;
        }

        public void UpdateOrders(List<OrderData> orders)
        {
            UserSession session = this.GetUserSession();
            try
            {
                foreach (OrderData order in orders)
                {
                    this.ValidateOrderData(order, session);
                    DatabaseManager.Instance.UpdateOrder(order);
                }
            }
            catch (Exception e)
            {
                throw new WebFaultException<RestApiErrorMessage>(new RestApiErrorMessage(e.Message), HttpStatusCode.BadRequest);
            }
        }

        public void ValidateOrderData(OrderData order, UserSession session)
        {
            if (!session.IsAdmin())
            {
                order.CustomerId = session.CustomerId;
            }

            if (order.Count != null)
            {
                order.Count = Math.Round(2 * order.Count.Value) / 2;
                if (order.Count <= 0 && order.Count > 5)
                {
                    throw new Exception("Не верно указано количество порций");
                }
            }
        }

        #endregion

        #region private

        private void CreateSession(CustomerData customer)
        {
            SessionData session = new SessionData();

            session.SessionToken = new string(Guid.NewGuid().ToString().Where(c => char.IsLetterOrDigit(c)).ToArray());
            session.CustomerId = customer.Id;
            session.AccessRights = customer.AccessRights;
            session.ExpirationDateEpochtime = DateTime.Now.AddMonths(MaxSessionLifeInMonths).ToEpochtime();

            try
            {
                DatabaseManager.Instance.CreateSesson(
                    session,
                    (customer.AccessRights == AccessRightEnum.Admin.ToString()) ? MaxAdminSessionCount : MaxUserSessionCount);
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
        }
    }
}