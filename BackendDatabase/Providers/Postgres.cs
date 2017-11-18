using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using Infocom.Chargers.BackendCommon;
using Npgsql;

namespace Infocom.Chargers.BackendDatabase
{
    public sealed class Postgres : IDatabase, IDisposable
    {
        private string connectionString;    // Connection string with parameters
        private int maxAttempts;            // Max tries if it is connection problem
        private int delay;                  // Delay on reconnection/try in ms

        public void Initialize(ConnectionData connectionData)
        {
            this.maxAttempts = connectionData.MaxAttempts;
            this.delay = connectionData.Delay * 1000;
            this.connectionString = "Pooling=true;Minimum Pool Size=2";

            if (connectionData.ConnectionPoolSize > 0)
            {
                this.connectionString += ";Maximum Pool Size=" + connectionData.ConnectionPoolSize;
            }

            if (connectionData.Timeout > 0)
            {
                this.connectionString += ";Timeout=" + connectionData.Timeout;
                this.connectionString += ";Command Timeout=" + connectionData.Timeout;
            }

            if (connectionData.Host != null)
            {
                this.connectionString += ";Host=" + connectionData.Host;
            }

            if (connectionData.Port != null)
            {
                this.connectionString += ";Port=" + connectionData.Port;
            }

            if (connectionData.User != null)
            {
                this.connectionString += ";Username=" + connectionData.User;
            }

            if (connectionData.Password != null)
            {
                this.connectionString += ";Password=" + connectionData.Password;
            }

            if (connectionData.Database != null)
            {
                this.connectionString += ";Database=" + connectionData.Database;
            }

            this.CreateTables();

            return;
        }

        public void Dispose()
        {
        }

        #region Sessions

        // Create login session
        public void CreateSesson(SessionData session, int maxSessionCount)
        {
            long now = DateTime.Now.ToEpochtime();

            this.Execute(
                "UPDATE Session SET Deleted='T', DeletionDate=@p0 WHERE CustomerId=@p1 AND SessionToken IN (SELECT SessionToken FROM Session"
                + " WHERE CustomerId=@p1 AND Deleted='F' ORDER BY ExpirationDate DESC OFFSET @p2)",
                cmd =>
                {
                    this.AddParam(cmd, "p0", now);
                    this.AddParam(cmd, "p1", session.CustomerId);
                    this.AddParam(cmd, "p2", maxSessionCount > 0 ? maxSessionCount - 1 : 0);
                    cmd.ExecuteNonQuery();
                },
                "INSERT INTO Session(SessionToken,CustomerId,AccessRights,IP,CreationDate,ExpirationDate,UserAgent,Deleted) VALUES(@p1,@p2,@p3,@p4,@p5,@p6,@p7,'F')",
                cmd =>
                {
                    this.AddParam(cmd, "p1", session.SessionToken, 32);
                    this.AddParam(cmd, "p2", session.CustomerId);
                    this.AddParam(cmd, "p3", session.AccessRights, 16);
                    this.AddParam(cmd, "p4", session.IP, 15);
                    this.AddParam(cmd, "p5", session.CreationDateEpochtime.HasValue ? session.CreationDateEpochtime : now);
                    this.AddParam(cmd, "p6", session.ExpirationDateEpochtime.HasValue ? session.ExpirationDateEpochtime : DateTime.Now.AddMonths(1).ToEpochtime());
                    this.AddParam(cmd, "p7", session.UserAgent, 128);
                    cmd.ExecuteNonQuery();
                });
        }

        // Get login session (only undeleted)
        public SessionData GetSesson(string sessionToken)
        {
            return (SessionData)this.Execute(
                "SELECT Id,CustomerId,AccessRights,IP,CreationDate,ExpirationDate,UserAgent"
                + " FROM Session WHERE SessionToken=@p AND Deleted='F'",
                cmd =>
                {
                    this.AddParam(cmd, "p", sessionToken, 32);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            SessionData session = new SessionData();
                            session.Id = reader.GetInt64(0);
                            session.SessionToken = sessionToken;
                            session.CustomerId = reader.GetInt64(1);
                            session.AccessRights = this.GetParamString(reader, 2);
                            session.IP = this.GetParamString(reader, 3);
                            session.CreationDateEpochtime = this.GetParamLong(reader, 4);
                            session.ExpirationDateEpochtime = this.GetParamLong(reader, 5);
                            session.UserAgent = this.GetParamString(reader, 6);
                            session.Deleted = false;

                            return session;
                        }

                        throw new Exception(string.Format("Сессия '{0}' не существует", sessionToken));
                    }
                });
        }

        // Delete session, do not triger any exception
        public void DeleteSession(string sessionToken)
        {
            this.Execute(
                "UPDATE Session SET Deleted='T', DeletionDate=@p1 WHERE SessionToken=@p AND Deleted='F'",
                cmd =>
                {
                    this.AddParam(cmd, "p", sessionToken, 32);
                    this.AddParam(cmd, "p1", DateTime.Now.ToEpochtime());
                    cmd.ExecuteNonQuery();
                },
                e => false);       // Do not return exception, be just silent
        }

        // Delete all sessions of this customer
        public void DeleteSession(long customerId)
        {
            this.Execute(
                "UPDATE Session SET Deleted='T', DeletionDate=@p1 WHERE CustomerId=@p AND Deleted='F'",
                cmd =>
                {
                    this.AddParam(cmd, "p", customerId);
                    this.AddParam(cmd, "p1", DateTime.Now.ToEpochtime());
                    cmd.ExecuteNonQuery();
                },
                e => false);       // Do not return exception, be just silent
        }

        // Purge session, do not triger any exception, used only in unit test
        public void PurgeSession(string sessionToken)
        {
            this.Execute(
                "DELETE FROM Session WHERE SessionToken=@p",
                cmd =>
                {
                    this.AddParam(cmd, "p", sessionToken, 32);
                    cmd.ExecuteNonQuery();
                },
                e => false);       // Do not return exception, be just silent
        }

        // Purge all sessions of this customer, used only in unit test
        public void PurgeSession(long customerId)
        {
            this.Execute(
                "DELETE FROM Session WHERE CustomerId=@p",
                cmd =>
                {
                    this.AddParam(cmd, "p", customerId);
                    cmd.ExecuteNonQuery();
                },
                e => false);       // Do not return exception, be just silent
        }

        // Search for sessions
        public List<SessionData> SearchSessions(string lastId, long startTime, long stopTime, string accessRights, string ip, string phone, string email, string organizationName, string firstName, string middleName, string lastName, long maxCount)
        {
            List<SessionData> ret = new List<SessionData>();
            List<long> customerIds = new List<long>();
            bool customerQuery = !string.IsNullOrEmpty(phone) || !string.IsNullOrEmpty(email) || !string.IsNullOrEmpty(organizationName)
                || !string.IsNullOrEmpty(firstName) || !string.IsNullOrEmpty(middleName) || !string.IsNullOrEmpty(lastName);
        
            this.Execute(
                "SELECT Id,SessionToken,CustomerId,AccessRights,IP,CreationDate,ExpirationDate,DeletionDate,UserAgent,Deleted FROM Session"
                + " WHERE " + (string.IsNullOrEmpty(lastId) ? string.Empty : " Id>@p1 AND")
                + " CreationDate>=@p2 AND CreationDate<=@p3 AND COALESCE(AccessRights,'') LIKE @p4"
                + " AND COALESCE(IP,'') LIKE @p5 "
                + (customerQuery ? " AND CustomerId IN (SELECT Id FROM Customer WHERE CAST(Phone AS TEXT) LIKE @p6 AND COALESCE(Email,'') LIKE @p7"
                   + " AND LOWER(COALESCE(OrganizationName,'')) LIKE @p8 AND LOWER(COALESCE(FirstName,'')) LIKE @p9 "
                   + "AND LOWER(COALESCE(MiddleName,'')) LIKE @p10 AND LOWER(COALESCE(LastName,'')) LIKE @p11)" : string.Empty)
                + " ORDER BY Id LIMIT " + maxCount.ToString(),
                cmd =>
                {
                    if (!string.IsNullOrEmpty(lastId))
                    {
                        this.AddParam(cmd, "p1", Convert.ToInt64(lastId));
                    }

                    this.AddParam(cmd, "p2", startTime);
                    this.AddParam(cmd, "p3", stopTime);
                    this.AddSearchParam(cmd, "p4", accessRights);
                    this.AddSearchParam(cmd, "p5", ip);

                    if (customerQuery)
                    {
                        this.AddSearchParam(cmd, "p6", phone);
                        this.AddSearchParam(cmd, "p7", email, true);
                        this.AddSearchParam(cmd, "p8", organizationName, true);
                        this.AddSearchParam(cmd, "p9", firstName, true);
                        this.AddSearchParam(cmd, "p10", middleName, true);
                        this.AddSearchParam(cmd, "p11", lastName, true);
                    }

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            SessionData session = new SessionData();
                            session.Id = reader.GetInt64(0);
                            session.SessionToken = this.GetParamString(reader, 1);
                            session.CustomerId = reader.GetInt64(2);
                            session.AccessRights = this.GetParamString(reader, 3);
                            session.IP = this.GetParamString(reader, 4);
                            session.CreationDateEpochtime = this.GetParamLong(reader, 5);
                            session.ExpirationDateEpochtime = this.GetParamLong(reader, 6);
                            session.DeletionDateEpochtime = this.GetParamLong(reader, 7);
                            session.UserAgent = this.GetParamString(reader, 8);
                            session.Deleted = this.GetParamBoolean(reader, 9);
                            ret.Add(session);

                            customerIds.Add(session.CustomerId);
                        }
                    }
                });

            // Read customers
            Dictionary<long, CustomerData> customers = this.GetCustomer(customerIds, true);

            foreach (var session in ret)
            {
                CustomerData customer;

                if (customers.TryGetValue(session.CustomerId, out customer))
                {
                    session.Customer = customer;
                }
            }

            return ret;
        }

        #endregion 

        #region Customers

        // Create new customer, returns Id
        public long CreateCustomer(CustomerData customer, long who)
        {
            customer.Email = customer.Email.ToLower();

            return this.ExecuteCreate(
                customer,
                who,
                e =>
                {
                    if (e.Message.Contains("idx_customer_email"))
                    {
                        throw new Exception(string.Format("Пользователь с e-mail '{0}' уже существует", customer.Email));
                    }
                    else if (e.Message.Contains("idx_customer_phone"))
                    {
                        throw new Exception(string.Format("Пользователь с телефоном '+{0}' уже существует", customer.Phone));
                    }

                    return true;
                });
        }

        // Update info for the customer using Id, only not null fields are updated
        public void UpdateCustomer(CustomerData customer, long who)
        {
            if (!string.IsNullOrEmpty(customer.Email))
            {
                customer.Email = customer.Email.ToLower();
            }

            this.ExecuteUpdate(
                customer,
                who,
                e =>
                {
                    if (e.Message.Contains("idx_customer_email"))
                    {
                        throw new Exception(string.Format("Пользователь с e-mail '{0}' уже существует", customer.Email));
                    }
                    else if (e.Message.Contains("idx_customer_phone"))
                    {
                        throw new Exception(string.Format("Пользователь с телефоном '+{0}' уже существует", customer.Phone));
                    }

                    return true;
                });
        }

        // Get customer info using phone
        public CustomerData GetCustomer(long phone, bool showHidden)
        {
            CustomerData customer = new CustomerData();

            this.Execute(
                "SELECT Id,Email,FirstName,MiddleName,LastName,BirthDate,Password,Country,Region,"
                + "Town,Address,CreationDate,UpdateDate,Language,CustomerType,AccessRights,PostIndex,"
                + "SecretQuestion,SecretAnswer,Sex,OrganizationName"
                + (showHidden ? ",IMEA,Comments,Deleted" : string.Empty)
                + " FROM Customer WHERE Phone=@p"
                + (showHidden ? string.Empty : " AND Deleted=false"),
                cmd =>
                {
                    this.AddParam(cmd, "p", phone);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            customer.Phone = phone;
                            customer.Id = reader.GetInt64(0);
                            customer.Email = this.GetParamString(reader, 1);
                            customer.FirstName = this.GetParamString(reader, 2);
                            customer.MiddleName = this.GetParamString(reader, 3);
                            customer.LastName = this.GetParamString(reader, 4);
                            customer.BirthDateEpochtime = this.GetParamLong(reader, 5);
                            customer.Password = this.GetParamString(reader, 6);
                            customer.Country = this.GetParamString(reader, 7);
                            customer.Region = this.GetParamString(reader, 8);
                            customer.Town = this.GetParamString(reader, 9);
                            customer.Address = this.GetParamString(reader, 10);
                            customer.CreationDateEpochtime = this.GetParamLong(reader, 11);
                            customer.UpdateDateEpochtime = this.GetParamLong(reader, 12);
                            customer.Language = this.GetParamString(reader, 13);
                            customer.CustomerType = this.GetParamString(reader, 14);
                            customer.AccessRights = this.GetParamString(reader, 15);
                            customer.PostIndex = this.GetParamString(reader, 16);
                            customer.SecretQuestion = this.GetParamString(reader, 17);
                            customer.SecretAnswer = this.GetParamString(reader, 18);
                            customer.Sex = this.GetParamBoolean(reader, 19);
                            customer.OrganizationName = this.GetParamString(reader, 20);

                            if (showHidden)
                            {
                                customer.IMEA = this.GetParamString(reader, 21);
                                customer.Comments = this.GetParamString(reader, 22);
                                customer.Deleted = this.GetParamBoolean(reader, 23);
                            }

                            return;
                        }

                        throw new Exception(string.Format("Пользователя с номером '+{0}' не существует", phone));
                    }
                });

            // Read car list
            customer.Cars = this.GetCars(customer.Id, showHidden);

            // Read RFID list
            customer.RFIDs = this.GetRFIDs(customer.Id, showHidden);

            return customer;
        }

        // Get customer info using email
        public CustomerData GetCustomer(string email, bool showHidden)
        {
            CustomerData customer = new CustomerData();

            this.Execute(
                "SELECT Id,Phone,FirstName,MiddleName,LastName,BirthDate,Password,Country,Region,"
                + "Town,Address,CreationDate,UpdateDate,Language,CustomerType,AccessRights,PostIndex,"
                + "SecretQuestion,SecretAnswer,Sex,OrganizationName"
                + (showHidden ? ",IMEA,Comments,Deleted" : string.Empty)
                + " FROM Customer WHERE Email=@p"
                + (showHidden ? string.Empty : " AND Deleted=false"),
                cmd =>
                {
                    this.AddParam(cmd, "p", email, 64);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            customer.Email = email;
                            customer.Id = reader.GetInt64(0);
                            customer.Phone = reader.GetInt64(1);
                            customer.FirstName = this.GetParamString(reader, 2);
                            customer.MiddleName = this.GetParamString(reader, 3);
                            customer.LastName = this.GetParamString(reader, 4);
                            customer.BirthDateEpochtime = this.GetParamLong(reader, 5);
                            customer.Password = this.GetParamString(reader, 6);
                            customer.Country = this.GetParamString(reader, 7);
                            customer.Region = this.GetParamString(reader, 8);
                            customer.Town = this.GetParamString(reader, 9);
                            customer.Address = this.GetParamString(reader, 10);
                            customer.CreationDateEpochtime = this.GetParamLong(reader, 11);
                            customer.UpdateDateEpochtime = this.GetParamLong(reader, 12);
                            customer.Language = this.GetParamString(reader, 13);
                            customer.CustomerType = this.GetParamString(reader, 14);
                            customer.AccessRights = this.GetParamString(reader, 15);
                            customer.PostIndex = this.GetParamString(reader, 16);
                            customer.SecretQuestion = this.GetParamString(reader, 17);
                            customer.SecretAnswer = this.GetParamString(reader, 18);
                            customer.Sex = this.GetParamBoolean(reader, 19);
                            customer.OrganizationName = this.GetParamString(reader, 20);

                            if (showHidden)
                            {
                                customer.IMEA = this.GetParamString(reader, 21);
                                customer.Comments = this.GetParamString(reader, 22);
                                customer.Deleted = this.GetParamBoolean(reader, 23);
                            }

                            return;
                        }

                        throw new Exception(string.Format("Пользователя с e-mail '{0}' не существует", email));
                    }
                });

            // Read car list
            customer.Cars = this.GetCars(customer.Id, showHidden);

            // Read RFID list
            customer.RFIDs = this.GetRFIDs(customer.Id, showHidden);

            return customer;
        }

        // Get customer info using Id
        public CustomerData GetCustomerById(long id, bool showHidden)
        {
            CustomerData customer = new CustomerData();

            this.Execute(
                "SELECT Phone,Email,FirstName,MiddleName,LastName,BirthDate,Password,Country,Region,"
                + "Town,Address,CreationDate,UpdateDate,Language,CustomerType,AccessRights,PostIndex,"
                + "SecretQuestion,SecretAnswer,Sex,OrganizationName"
                + (showHidden ? ",IMEA,Comments,Deleted" : string.Empty)
                + " FROM Customer WHERE Id=@p"
                + (showHidden ? string.Empty : " AND Deleted=false"),
                cmd =>
                {
                    this.AddParam(cmd, "p", id);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            customer.Phone = reader.GetInt64(0);
                            customer.Id = id;
                            customer.Email = this.GetParamString(reader, 1);
                            customer.FirstName = this.GetParamString(reader, 2);
                            customer.MiddleName = this.GetParamString(reader, 3);
                            customer.LastName = this.GetParamString(reader, 4);
                            customer.BirthDateEpochtime = this.GetParamLong(reader, 5);
                            customer.Password = this.GetParamString(reader, 6);
                            customer.Country = this.GetParamString(reader, 7);
                            customer.Region = this.GetParamString(reader, 8);
                            customer.Town = this.GetParamString(reader, 9);
                            customer.Address = this.GetParamString(reader, 10);
                            customer.CreationDateEpochtime = this.GetParamLong(reader, 11);
                            customer.UpdateDateEpochtime = this.GetParamLong(reader, 12);
                            customer.Language = this.GetParamString(reader, 13);
                            customer.CustomerType = this.GetParamString(reader, 14);
                            customer.AccessRights = this.GetParamString(reader, 15);
                            customer.PostIndex = this.GetParamString(reader, 16);
                            customer.SecretQuestion = this.GetParamString(reader, 17);
                            customer.SecretAnswer = this.GetParamString(reader, 18);
                            customer.Sex = this.GetParamBoolean(reader, 19);
                            customer.OrganizationName = this.GetParamString(reader, 20);

                            if (showHidden)
                            {
                                customer.IMEA = this.GetParamString(reader, 21);
                                customer.Comments = this.GetParamString(reader, 22);
                                customer.Deleted = this.GetParamBoolean(reader, 23);
                            }

                            return;
                        }

                        throw new Exception(string.Format("Пользователя с Id '{0}' не существует", id));
                    }
                });

            // Read car list
            customer.Cars = this.GetCars(id, showHidden);

            // Read RFID list
            customer.RFIDs = this.GetRFIDs(id, showHidden);

            return customer;
        }

        // Get customer info using RFID
        public CustomerData GetCustomerByRFID(string rfid, bool showHidden)
        {
            CustomerData customer = new CustomerData();

            this.Execute(
                "SELECT Id,Phone,Email,FirstName,MiddleName,LastName,BirthDate,Password,Country,Region,"
                + "Town,Address,CreationDate,UpdateDate,Language,CustomerType,AccessRights,PostIndex,"
                + "SecretQuestion,SecretAnswer,Sex,OrganizationName"
                + (showHidden ? ",IMEA,Comments,Deleted" : string.Empty)
                + " FROM Customer WHERE Id=(SELECT CustomerId FROM RFID WHERE Value=@p"
                + (showHidden ? ")" : " AND Blocked=false AND Deleted=false) AND Deleted=false"),
                cmd =>
                {
                    this.AddParam(cmd, "p", rfid, 20);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            customer.Id = reader.GetInt64(0);
                            customer.Phone = reader.GetInt64(1);
                            customer.Email = this.GetParamString(reader, 2);
                            customer.FirstName = this.GetParamString(reader, 3);
                            customer.MiddleName = this.GetParamString(reader, 4);
                            customer.LastName = this.GetParamString(reader, 5);
                            customer.BirthDateEpochtime = this.GetParamLong(reader, 6);
                            customer.Password = this.GetParamString(reader, 7);
                            customer.Country = this.GetParamString(reader, 8);
                            customer.Region = this.GetParamString(reader, 9);
                            customer.Town = this.GetParamString(reader, 10);
                            customer.Address = this.GetParamString(reader, 11);
                            customer.CreationDateEpochtime = this.GetParamLong(reader, 12);
                            customer.UpdateDateEpochtime = this.GetParamLong(reader, 13);
                            customer.Language = this.GetParamString(reader, 14);
                            customer.CustomerType = this.GetParamString(reader, 15);
                            customer.AccessRights = this.GetParamString(reader, 16);
                            customer.PostIndex = this.GetParamString(reader, 17);
                            customer.SecretQuestion = this.GetParamString(reader, 18);
                            customer.SecretAnswer = this.GetParamString(reader, 19);
                            customer.Sex = this.GetParamBoolean(reader, 20);
                            customer.OrganizationName = this.GetParamString(reader, 21);

                            if (showHidden)
                            {
                                customer.IMEA = this.GetParamString(reader, 22);
                                customer.Comments = this.GetParamString(reader, 23);
                                customer.Deleted = this.GetParamBoolean(reader, 24);
                            }

                            return;
                        }

                        throw new Exception(string.Format("Пользователя с RFID '{0}' не существует", rfid));
                    }
                });

            // Read car list
            customer.Cars = this.GetCars(customer.Id, showHidden);

            // Read RFID list
            customer.RFIDs = this.GetRFIDs(customer.Id, showHidden);

            return customer;
        }

        // Get customer info using Id
        public Dictionary<long, CustomerData> GetCustomer(List<long> ids, bool showHidden)
        {
            Dictionary<long, CustomerData> ret = new Dictionary<long, CustomerData>();

            if (ids.Count == 0)
            {
                return ret;
            }

            this.Execute(
                "SELECT Id,Phone,Email,FirstName,MiddleName,LastName,BirthDate,Password,Country,Region,"
                + "Town,Address,CreationDate,UpdateDate,Language,CustomerType,AccessRights,PostIndex,"
                + "SecretQuestion,SecretAnswer,Sex,OrganizationName"
                + (showHidden ? ",IMEA,Comments,Deleted" : string.Empty)
                + " FROM Customer WHERE Id=any(@p)",
                /* + (showHidden ? string.Empty : " AND Deleted=false"),*/
                cmd =>
                {
                    cmd.Parameters.AddWithValue("p", ids);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            CustomerData customer = new CustomerData();

                            customer.Id = reader.GetInt64(0);
                            customer.Phone = reader.GetInt64(1);
                            customer.Email = this.GetParamString(reader, 2);
                            customer.FirstName = this.GetParamString(reader, 3);
                            customer.MiddleName = this.GetParamString(reader, 4);
                            customer.LastName = this.GetParamString(reader, 5);
                            customer.BirthDateEpochtime = this.GetParamLong(reader, 6);
                            customer.Password = this.GetParamString(reader, 7);
                            customer.Country = this.GetParamString(reader, 8);
                            customer.Region = this.GetParamString(reader, 9);
                            customer.Town = this.GetParamString(reader, 10);
                            customer.Address = this.GetParamString(reader, 11);
                            customer.CreationDateEpochtime = this.GetParamLong(reader, 12);
                            customer.UpdateDateEpochtime = this.GetParamLong(reader, 13);
                            customer.Language = this.GetParamString(reader, 14);
                            customer.CustomerType = this.GetParamString(reader, 15);
                            customer.AccessRights = this.GetParamString(reader, 16);
                            customer.PostIndex = this.GetParamString(reader, 17);
                            customer.SecretQuestion = this.GetParamString(reader, 18);
                            customer.SecretAnswer = this.GetParamString(reader, 19);
                            customer.Sex = this.GetParamBoolean(reader, 20);
                            customer.OrganizationName = this.GetParamString(reader, 21);

                            if (showHidden)
                            {
                                customer.IMEA = this.GetParamString(reader, 22);
                                customer.Comments = this.GetParamString(reader, 23);
                                customer.Deleted = this.GetParamBoolean(reader, 24);
                            }

                            ret[customer.Id] = customer;
                        }
                    }
                });

            // Read car list
            Dictionary<long, List<CarData>> cars = this.GetCars(ids, true);

            foreach (var customer in ret)
            {
                List<CarData> carList;

                if (cars.TryGetValue(customer.Key, out carList))
                {
                    customer.Value.Cars = carList;
                }
            }

            // Read RFID list
            Dictionary<long, List<RFIDData>> rfids = this.GetRFIDs(ids, true);

            foreach (var customer in ret)
            {
                List<RFIDData> rfidList;

                if (rfids.TryGetValue(customer.Key, out rfidList))
                {
                    customer.Value.RFIDs = rfidList;
                }
            }

            return ret;
        }

        // Search for customers
        public List<CustomerData> SearchCustomers(string lastId, string phone, string email, string organizationName, string firstName, string middleName, string lastName, long maxCount)
        {
            List<CustomerData> ret = new List<CustomerData>();
            List<long> customerIds = new List<long>();

            this.Execute(
                "SELECT Id,Phone,Email,FirstName,MiddleName,LastName,BirthDate,Password,Country,Region,"
                + "Town,Address,CreationDate,UpdateDate,Language,CustomerType,AccessRights,PostIndex,"
                + "SecretQuestion,SecretAnswer,Sex,IMEA,OrganizationName,Comments,Deleted FROM Customer"
                + " WHERE" + (string.IsNullOrEmpty(lastId) ? string.Empty : " Id>@p1 AND")
                + " CAST(Phone AS TEXT) LIKE @p2 AND COALESCE(Email,'') LIKE @p3"
                + " AND LOWER(COALESCE(OrganizationName,'')) LIKE @p4 AND LOWER(COALESCE(FirstName,'')) LIKE @p5"
                + " AND LOWER(COALESCE(MiddleName,'')) LIKE @p6 AND LOWER(COALESCE(LastName,'')) LIKE @p7"
                + " ORDER BY Id LIMIT " + maxCount.ToString(),
                cmd =>
                {
                    if (!string.IsNullOrEmpty(lastId))
                    {
                        this.AddParam(cmd, "p1", Convert.ToInt64(lastId));
                    }

                    this.AddSearchParam(cmd, "p2", phone);
                    this.AddSearchParam(cmd, "p3", email, true);
                    this.AddSearchParam(cmd, "p4", organizationName, true);
                    this.AddSearchParam(cmd, "p5", firstName, true);
                    this.AddSearchParam(cmd, "p6", middleName, true);
                    this.AddSearchParam(cmd, "p7", lastName, true);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            CustomerData customer = new CustomerData();
                            customer.Id = reader.GetInt64(0);
                            customer.Phone = reader.GetInt64(1);
                            customer.Email = this.GetParamString(reader, 2);
                            customer.FirstName = this.GetParamString(reader, 3);
                            customer.MiddleName = this.GetParamString(reader, 4);
                            customer.LastName = this.GetParamString(reader, 5);
                            customer.BirthDateEpochtime = this.GetParamLong(reader, 6);
                            customer.Password = this.GetParamString(reader, 7);
                            customer.Country = this.GetParamString(reader, 8);
                            customer.Region = this.GetParamString(reader, 9);
                            customer.Town = this.GetParamString(reader, 10);
                            customer.Address = this.GetParamString(reader, 11);
                            customer.CreationDateEpochtime = this.GetParamLong(reader, 12);
                            customer.UpdateDateEpochtime = this.GetParamLong(reader, 13);
                            customer.Language = this.GetParamString(reader, 14);
                            customer.CustomerType = this.GetParamString(reader, 15);
                            customer.AccessRights = this.GetParamString(reader, 16);
                            customer.PostIndex = this.GetParamString(reader, 17);
                            customer.SecretQuestion = this.GetParamString(reader, 18);
                            customer.SecretAnswer = this.GetParamString(reader, 19);
                            customer.Sex = this.GetParamBoolean(reader, 20);
                            customer.IMEA = this.GetParamString(reader, 21);
                            customer.OrganizationName = this.GetParamString(reader, 22);
                            customer.Comments = this.GetParamString(reader, 23);
                            customer.Deleted = this.GetParamBoolean(reader, 24);
                            ret.Add(customer);

                            customerIds.Add(customer.Id);
                        }
                    }
                });

            // Read car list
            Dictionary<long, List<CarData>> cars = this.GetCars(customerIds, true);

            foreach (var customer in ret)
            {
                List<CarData> carList;

                if (cars.TryGetValue(customer.Id, out carList))
                {
                    customer.Cars = carList;
                }
            }

            // Read RFID list
            Dictionary<long, List<RFIDData>> rfids = this.GetRFIDs(customerIds, true);

            foreach (var customer in ret)
            {
                List<RFIDData> rfidList;

                if (rfids.TryGetValue(customer.Id, out rfidList))
                {
                    customer.RFIDs = rfidList;
                }
            }

            return ret;
        }

        // Delete the customer
        public void DeleteCustomer(CustomerData customer, long who)
        {
            this.ExecuteDelete(customer, who);
        }

        // Purge the customer
        public void PurgeCustomer(CustomerData customer, long who)
        {
            this.ExecutePurge(customer, who);
        }

        // Purge the existing user, used only for testing
        public void PurgeCustomer(long phone)
        {
            long id = 0;

            this.Execute(
                "SELECT Id FROM Customer WHERE Phone=@p",
                cmd =>
                {
                    this.AddParam(cmd, "p", phone);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            id = reader.GetInt64(0);
                        }
                    }
                });

            if (id > 0)
            {
                this.Execute(
                    "DELETE FROM Customer WHERE Id=@p",
                    cmd =>
                    {
                        this.AddParam(cmd, "p", id);
                        cmd.ExecuteNonQuery();
                    });
            }
        }

        #endregion 

        #region Cars

        // Create new car, returns Id
        public long CreateCar(CarData car, long who)
        {
            long count = 0;

            this.Execute(
                "SELECT COUNT(*) FROM Car WHERE CustomerId=@p",
                cmd =>
                {
                    this.AddParam(cmd, "p", car.CustomerId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            count = reader.GetInt64(0);
                        }
                    }
                });

            if (count > MaxCars)
            {
                throw new Exception("Создано слишком много авто. Обратитесь в поддержку");
            }

            return this.ExecuteCreate(
                car, 
                who,
                e =>
                {
                    if (e.Message.Contains("car_customerid_fkey"))
                    {
                        throw new Exception(string.Format("Клиент с Id '{0}' не существует или удален", car.CustomerId));
                    }

                    return true;
                });
        }

        // Update info for the car using Id, only not null fields are updated
        public void UpdateCar(CarData car, long who)
        {
            this.ExecuteUpdate(
                car,
                who,
                e =>
                {
                    if (e.Message.Contains("car_customerid_fkey"))
                    {
                        throw new Exception(string.Format("Клиент с Id '{0}' не существует или удален", car.CustomerId));
                    }

                    return true;
                });
        }

        // Get list of cars with info using customer Id
        public List<CarData> GetCars(long customerId, bool showHidden)
        {
            return (List<CarData>)this.Execute(
                "SELECT Id,Brand,Model,Year,RegNumber,VIN,UpdateDate"
                + (showHidden ? ",Comments,Deleted" : string.Empty)
                + " FROM Car WHERE CustomerId=@p"
                + (showHidden ? string.Empty : " AND Deleted=false")
                + " ORDER BY Id",
                cmd =>
                {
                    this.AddParam(cmd, "p", customerId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        List<CarData> carList = new List<CarData>();

                        while (reader.Read())
                        {
                            CarData car = new CarData();
                            car.Id = reader.GetInt64(0);
                            car.CustomerId = customerId;
                            car.Brand = this.GetParamString(reader, 1);
                            car.Model = this.GetParamString(reader, 2);
                            car.Year = this.GetParamLong(reader, 3);
                            car.RegNumber = this.GetParamString(reader, 4);
                            car.VIN = this.GetParamString(reader, 5);
                            car.UpdateDateEpochtime = this.GetParamLong(reader, 6);

                            if (showHidden)
                            {
                                car.Comments = this.GetParamString(reader, 7);
                                car.Deleted = this.GetParamBoolean(reader, 8);
                            }

                            carList.Add(car);
                        }

                        return carList;
                    }
                });
        }

        // Get list of cars with info using customer Id
        public Dictionary<long, List<CarData>> GetCars(List<long> customerIds, bool showHidden)
        {
            Dictionary<long, List<CarData>> ret = new Dictionary<long, List<CarData>>();

            if (customerIds.Count == 0)
            {
                return ret;
            }

            this.Execute(
                "SELECT Id,CustomerId,Brand,Model,Year,RegNumber,VIN,UpdateDate"
                + (showHidden ? ",Comments,Deleted" : string.Empty)
                + " FROM Car WHERE CustomerId=any(@p)"
                + (showHidden ? string.Empty : " AND Deleted=false")
                + " ORDER BY Id",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("p", customerIds);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            CarData car = new CarData();
                            car.Id = reader.GetInt64(0);
                            car.CustomerId = reader.GetInt64(1);
                            car.Brand = this.GetParamString(reader, 2);
                            car.Model = this.GetParamString(reader, 3);
                            car.Year = this.GetParamLong(reader, 4);
                            car.RegNumber = this.GetParamString(reader, 5);
                            car.VIN = this.GetParamString(reader, 6);
                            car.UpdateDateEpochtime = this.GetParamLong(reader, 7);

                            if (showHidden)
                            {
                                car.Comments = this.GetParamString(reader, 8);
                                car.Deleted = this.GetParamBoolean(reader, 9);
                            }

                            if (car.CustomerId.HasValue)
                            {
                                List<CarData> cars;

                                if (ret.TryGetValue(car.CustomerId.Value, out cars))
                                {
                                    cars.Add(car);
                                }
                                else
                                {
                                    cars = new List<CarData>();
                                    cars.Add(car);
                                    ret[car.CustomerId.Value] = cars;
                                }
                            }
                        }
                    }
                });

            return ret;
        }

        // Get car with info using Id
        public Dictionary<long, CarData> GetCar(List<long> ids, bool showHidden)
        {
            Dictionary<long, CarData> ret = new Dictionary<long, CarData>();

            if (ids.Count == 0)
            {
                return ret;
            }

            this.Execute(
                "SELECT Id,CustomerId,Brand,Model,Year,RegNumber,VIN,UpdateDate"
                + (showHidden ? ",Comments,Deleted" : string.Empty)
                + " FROM Car WHERE Id=any(@p)"
                /* + (showHidden ? string.Empty : " AND Deleted=false"), */
                + " ORDER BY Id",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("p", ids);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            CarData car = new CarData();
                            car.Id = reader.GetInt64(0);
                            car.CustomerId = reader.GetInt64(1);
                            car.Brand = this.GetParamString(reader, 2);
                            car.Model = this.GetParamString(reader, 3);
                            car.Year = this.GetParamLong(reader, 4);
                            car.RegNumber = this.GetParamString(reader, 5);
                            car.VIN = this.GetParamString(reader, 6);
                            car.UpdateDateEpochtime = this.GetParamLong(reader, 7);

                            if (showHidden)
                            {
                                car.Comments = this.GetParamString(reader, 8);
                                car.Deleted = this.GetParamBoolean(reader, 9);
                            }

                            ret[car.Id] = car;
                        }
                    }
                });

            return ret;
        }

        // Delete the car
        public void DeleteCar(CarData car, long who)
        {
            this.ExecuteDelete(car, who);
        }

        // Purge the existing car
        public void PurgeCar(CarData car, long who)
        {
            this.ExecutePurge(car, who);
        }

        #endregion 

        #region RFIDs

        // Create new RFID, returns Id
        public long CreateRFID(RFIDData rfid, long who)
        {
            return this.ExecuteCreate(
                rfid,
                who,
                e =>
                {
                    if (e.Message.Contains("rfid_customerid_fkey"))
                    {
                        throw new Exception(string.Format("Клиент с Id '{0}' не существует или удален", rfid.CustomerId));
                    }

                    if (e.Message.Contains("idx_rfid_value"))
                    {
                        throw new Exception("Данная RFID карта уже привязана к другому клиенту");
                    }

                    return true;
                });
        }

        // Update info for the RFID using Id, only not null fields are updated
        public void UpdateRFID(RFIDData rfid, long who)
        {
            this.ExecuteUpdate(
                rfid,
                who,
                e =>
                {
                    if (e.Message.Contains("rfid_customerid_fkey"))
                    {
                        throw new Exception(string.Format("Клиент с Id '{0}' не существует или удален", rfid.CustomerId));
                    }

                    if (e.Message.Contains("rfid_value_fkey"))
                    {
                        throw new Exception("Данная RFID карта уже привязана к другому клиенту");
                    }

                    return true;
                });
        }

        // Get list of RFIDs with info using customer Id
        public List<RFIDData> GetRFIDs(long customerId, bool showHidden)
        {
            return (List<RFIDData>)this.Execute(
                "SELECT Id,Blocked"
                + (showHidden ? ",Value,CreationDate,Comments,Deleted" : string.Empty)
                + " FROM RFID WHERE CustomerId=@p"
                + (showHidden ? string.Empty : " AND Deleted=false")
                + " ORDER BY Id",
                cmd =>
                {
                    this.AddParam(cmd, "p", customerId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        List<RFIDData> rfidList = new List<RFIDData>();

                        while (reader.Read())
                        {
                            RFIDData rfid = new RFIDData();
                            rfid.Id = reader.GetInt64(0);
                            rfid.CustomerId = customerId;
                            rfid.Blocked = this.GetParamBoolean(reader, 1);

                            if (showHidden)
                            {
                                rfid.Value = this.GetParamString(reader, 2);
                                rfid.CreationDateEpochtime = this.GetParamLong(reader, 3);
                                rfid.Comments = this.GetParamString(reader, 4);
                                rfid.Deleted = this.GetParamBoolean(reader, 5);
                            }

                            rfidList.Add(rfid);
                        }

                        return rfidList;
                    }
                });
        }

        // Get list of RFIDs with info using customer Id
        public Dictionary<long, List<RFIDData>> GetRFIDs(List<long> customerIds, bool showHidden)
        {
            Dictionary<long, List<RFIDData>> ret = new Dictionary<long, List<RFIDData>>();

            if (customerIds.Count == 0)
            {
                return ret;
            }

            this.Execute(
                "SELECT Id,CustomerId,Blocked"
                + (showHidden ? ",Value,CreationDate,Comments,Deleted" : string.Empty)
                + " FROM RFID WHERE CustomerId=any(@p)"
                + (showHidden ? string.Empty : " AND Deleted=false")
                + " ORDER BY Id",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("p", customerIds);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            RFIDData rfid = new RFIDData();
                            rfid.Id = reader.GetInt64(0);
                            rfid.CustomerId = reader.GetInt64(1);
                            rfid.Blocked = this.GetParamBoolean(reader, 2);

                            if (showHidden)
                            {
                                rfid.Value = this.GetParamString(reader, 3);
                                rfid.CreationDateEpochtime = this.GetParamLong(reader, 4);
                                rfid.Comments = this.GetParamString(reader, 5);
                                rfid.Deleted = this.GetParamBoolean(reader, 6);
                            }

                            if (rfid.CustomerId.HasValue)
                            {
                                List<RFIDData> rfids;

                                if (ret.TryGetValue(rfid.CustomerId.Value, out rfids))
                                {
                                    rfids.Add(rfid);
                                }
                                else
                                {
                                    rfids = new List<RFIDData>();
                                    rfids.Add(rfid);
                                    ret[rfid.CustomerId.Value] = rfids;
                                }
                            }
                        }
                    }
                });

            return ret;
        }

        // Search for RFIDs
        public List<RFIDData> SearchRFIDs(string lastId, string value, long maxCount)
        {
            List<RFIDData> ret = new List<RFIDData>();

            this.Execute(
                "SELECT Id,CustomerId,Value,Blocked,CreationDate,Comments,Deleted FROM RFID"
                + " WHERE" + (string.IsNullOrEmpty(lastId) ? string.Empty : " Id>@p1 AND")
                + " COALESCE(Value,'') LIKE @p2 ORDER BY Id LIMIT " + maxCount.ToString(),
                cmd =>
                {
                    if (!string.IsNullOrEmpty(lastId))
                    {
                        this.AddParam(cmd, "p1", Convert.ToInt64(lastId));
                    }

                    this.AddSearchParam(cmd, "p2", value);
 
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            RFIDData rfid = new RFIDData();
                            rfid.Id = reader.GetInt64(0);
                            rfid.CustomerId = reader.GetInt64(1);
                            rfid.Value = this.GetParamString(reader, 2);
                            rfid.Blocked = this.GetParamBoolean(reader, 3);
                            rfid.CreationDateEpochtime = this.GetParamLong(reader, 4);
                            rfid.Comments = this.GetParamString(reader, 5);
                            rfid.Deleted = this.GetParamBoolean(reader, 6);
                            ret.Add(rfid);
                        }
                    }
                });

            return ret;
        }

        // Delete the RFID
        public void DeleteRFID(RFIDData rfid, long who)
        {
            this.ExecuteDelete(rfid, who);
        }

        // Purge the RFID
        public void PurgeRFID(RFIDData rfid, long who)
        {
           this.ExecutePurge(rfid, who);
        }

        #endregion 

        #region Stations

        // Create new station, returns Id
        public long CreateStation(StationData station, long who)
        {
            return this.ExecuteCreate(
                station,
                who,
                e =>
                {
                    if (e.Message.Contains("idx_station_name"))
                    {
                        throw new Exception(string.Format("Зарядная станция с именем '{0}' уже существует", station.Name));
                    }

                    return true;
                });
        }

        // Update info for the station using Id, only not null fields are updated
        public void UpdateStation(StationData station, long who)
        {
            this.ExecuteUpdate(
                station,
                who,
                e =>
                {
                    if (e.Message.Contains("idx_station_name"))
                    {
                        throw new Exception(string.Format("Зарядная станция с именем '{0}' уже существует", station.Name));
                    }

                    return true;
                });
        }

        // Get station with info using Station Id
        public StationData GetStation(long id, bool showHidden)
        {
            StationData station = new StationData();

            this.Execute(
                "SELECT Name,Description,Latitude,Longitude,InfoMessage,NetworkName,Phone,Country,Region,Town,PostIndex,Address,Web,"
                + "OpenHours,AccessType,PaymentType,Status"
                + (showHidden ? ",CreationDate,UpdateDate,Comments,Deleted" : string.Empty)
                + " FROM Station WHERE Id=@p"
                + (showHidden ? string.Empty : " AND Deleted=false"),
                cmd =>
                {
                    this.AddParam(cmd, "p", id);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            station.Id = id;
                            station.Name = this.GetParamString(reader, 0);
                            station.Description = this.GetParamString(reader, 1);
                            station.Latitude = this.GetParamDouble(reader, 2);
                            station.Longitude = this.GetParamDouble(reader, 3);
                            station.InfoMessage = this.GetParamString(reader, 4);
                            station.NetworkName = this.GetParamString(reader, 5);
                            station.Phone = this.GetParamLong(reader, 6);
                            station.Country = this.GetParamString(reader, 7);
                            station.Region = this.GetParamString(reader, 8);
                            station.Town = this.GetParamString(reader, 9);
                            station.PostIndex = this.GetParamString(reader, 10);
                            station.Address = this.GetParamString(reader, 11);
                            station.Web = this.GetParamString(reader, 12);
                            station.OpenHours = this.GetParamString(reader, 13);
                            station.AccessType = this.GetParamString(reader, 14);
                            station.PaymentType = this.GetParamString(reader, 15);
                            station.Status = this.GetParamString(reader, 16);

                            if (showHidden)
                            {
                                station.CreationDateEpochtime = this.GetParamLong(reader, 17);
                                station.UpdateDateEpochtime = this.GetParamLong(reader, 18);
                                station.Comments = this.GetParamString(reader, 19);
                                station.Deleted = this.GetParamBoolean(reader, 20);
                            }

                            return;
                        }

                        throw new Exception(string.Format("Зарядной станции с Id '{0}' не существует", id));
                    }
                });

            // Read port list
            station.Ports = this.GetPorts(id, showHidden);

            return station;
        }

        // Get station with info using Station Id
        public Dictionary<long, StationData> GetStation(List<long> ids, bool showHidden)
        {
            Dictionary<long, StationData> ret = new Dictionary<long, StationData>();

            if (ids.Count == 0)
            {
                return ret;
            }

            this.Execute(
                "SELECT Id,Name,Description,Latitude,Longitude,InfoMessage,NetworkName,Phone,Country,Region,Town,PostIndex,Address,Web,"
                + "OpenHours,AccessType,PaymentType,Status"
                + (showHidden ? ",CreationDate,UpdateDate,Comments,Deleted" : string.Empty)
                + " FROM Station WHERE Id=any(@p)",
                /* + (showHidden ? string.Empty : " AND Deleted=false"), */
                cmd =>
                {
                    cmd.Parameters.AddWithValue("p", ids);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            StationData station = new StationData();
                            station.Id = reader.GetInt64(0);
                            station.Name = this.GetParamString(reader, 1);
                            station.Description = this.GetParamString(reader, 2);
                            station.Latitude = this.GetParamDouble(reader, 3);
                            station.Longitude = this.GetParamDouble(reader, 4);
                            station.InfoMessage = this.GetParamString(reader, 5);
                            station.NetworkName = this.GetParamString(reader, 6);
                            station.Phone = this.GetParamLong(reader, 7);
                            station.Country = this.GetParamString(reader, 8);
                            station.Region = this.GetParamString(reader, 9);
                            station.Town = this.GetParamString(reader, 10);
                            station.PostIndex = this.GetParamString(reader, 11);
                            station.Address = this.GetParamString(reader, 12);
                            station.Web = this.GetParamString(reader, 13);
                            station.OpenHours = this.GetParamString(reader, 14);
                            station.AccessType = this.GetParamString(reader, 15);
                            station.PaymentType = this.GetParamString(reader, 16);
                            station.Status = this.GetParamString(reader, 17);

                            if (showHidden)
                            {
                                station.CreationDateEpochtime = this.GetParamLong(reader, 18);
                                station.UpdateDateEpochtime = this.GetParamLong(reader, 19);
                                station.Comments = this.GetParamString(reader, 20);
                                station.Deleted = this.GetParamBoolean(reader, 21);
                            }

                            ret[station.Id] = station;
                        }
                    }
                });

            // Read port list
            Dictionary<long, List<PortData>> ports = this.GetPorts(ids, null, null, null, showHidden);

            foreach (var station in ret)
            {
                List<PortData> portList;

                if (ports.TryGetValue(station.Key, out portList))
                {
                    station.Value.Ports = portList;
                }
            }

            return ret;
        }

        // Search for stations and ports on the map
        public List<StationData> SearchStationsOnMap(string lastId, long[] ignoreIds, double minLatitude, double maxLatitude, double minLongitude, double maxLongitude, string[] accessTypes, string[] stationStatuses, string[] portTypes, string[] portLevels, string[] portStatuses, bool showHidden, long maxCount)
        {
            List<StationData> ret = new List<StationData>();
            List<long> stationIds = new List<long>();
            string portQuery = string.Empty;

            // Port query
            if (portTypes != null || portLevels != null || portStatuses != null)
            {
                portQuery = " Id IN (SELECT StationId FROM Port WHERE"
                    + (portTypes == null ? string.Empty : " PortType=any(@pt) AND")
                    + (portLevels == null ? string.Empty : " Level=any(@pl) AND")
                    + (portStatuses == null ? string.Empty : " Status=any(@ps) AND")
                    + (showHidden ? " 1=1" : " Deleted=false")
                    + ") AND";
            }

            this.Execute(
                "SELECT Id,Name,Description,Latitude,Longitude,InfoMessage,NetworkName,Phone,Country,Region,Town,PostIndex,Address,Web,"
                + "OpenHours,AccessType,PaymentType,Status"
                + (showHidden ? ",CreationDate,UpdateDate,Comments,Deleted" : string.Empty)
                + " FROM Station WHERE"
                + (string.IsNullOrEmpty(lastId) ? string.Empty : " Id>@p1 AND")
                + (ignoreIds == null ? string.Empty : " NOT(Id=any(@ig)) AND")
                + (accessTypes == null ? string.Empty : " AccessType=any(@at) AND")
                + (stationStatuses == null ? string.Empty : " Status=any(@ss) AND")
                + portQuery
                + " Latitude>=@la AND Latitude<=@la2 AND Longitude>=@lo AND Longitude<=@lo2"
                + (showHidden ? string.Empty : " AND Deleted=false")
                + " ORDER BY Id LIMIT " + maxCount.ToString(),
                cmd =>
                {
                    if (!string.IsNullOrEmpty(lastId))
                    {
                        this.AddParam(cmd, "p1", Convert.ToInt64(lastId));
                    }

                    if (ignoreIds != null)
                    {
                        cmd.Parameters.AddWithValue("ig", ignoreIds);
                    }

                    if (accessTypes != null)
                    {
                        cmd.Parameters.AddWithValue("at", accessTypes);
                    }

                    if (stationStatuses != null)
                    {
                        cmd.Parameters.AddWithValue("ss", stationStatuses);
                    }

                    if (portTypes != null)
                    {
                        cmd.Parameters.AddWithValue("pt", portTypes);
                    }

                    if (portLevels != null)
                    {
                        cmd.Parameters.AddWithValue("pl", portLevels);
                    }

                    if (portStatuses != null)
                    {
                        cmd.Parameters.AddWithValue("ps", portStatuses);
                    }

                    this.AddParam(cmd, "la", minLatitude);
                    this.AddParam(cmd, "la2", maxLatitude);
                    this.AddParam(cmd, "lo", minLongitude);
                    this.AddParam(cmd, "lo2", maxLongitude);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            StationData station = new StationData();
                            station.Id = reader.GetInt64(0);
                            station.Name = this.GetParamString(reader, 1);
                            station.Description = this.GetParamString(reader, 2);
                            station.Latitude = this.GetParamDouble(reader, 3);
                            station.Longitude = this.GetParamDouble(reader, 4);
                            station.InfoMessage = this.GetParamString(reader, 5);
                            station.NetworkName = this.GetParamString(reader, 6);
                            station.Phone = this.GetParamLong(reader, 7);
                            station.Country = this.GetParamString(reader, 8);
                            station.Region = this.GetParamString(reader, 9);
                            station.Town = this.GetParamString(reader, 10);
                            station.PostIndex = this.GetParamString(reader, 11);
                            station.Address = this.GetParamString(reader, 12);
                            station.Web = this.GetParamString(reader, 13);
                            station.OpenHours = this.GetParamString(reader, 14);
                            station.AccessType = this.GetParamString(reader, 15);
                            station.PaymentType = this.GetParamString(reader, 16);
                            station.Status = this.GetParamString(reader, 17);

                            if (showHidden)
                            {
                                station.CreationDateEpochtime = this.GetParamLong(reader, 18);
                                station.UpdateDateEpochtime = this.GetParamLong(reader, 19);
                                station.Comments = this.GetParamString(reader, 20);
                                station.Deleted = this.GetParamBoolean(reader, 21);
                            }

                            ret.Add(station);

                            stationIds.Add(station.Id);
                        }
                    }
                });

            // Read port list
            Dictionary<long, List<PortData>> ports = this.GetPorts(stationIds, portTypes, portLevels, portStatuses, showHidden);

            foreach (var station in ret)
            {
                List<PortData> portList;

                if (ports.TryGetValue(station.Id, out portList))
                {
                    station.Ports = portList;
                }
            }

            return ret;
        }

        // Search for stations and ports in the town
        public List<StationData> SearchStationsInTown(string lastId, string country, string region, string town, string[] accessTypes, string[] stationStatuses, string[] portTypes, string[] portLevels, string[] portStatuses, bool showHidden, long maxCount)
        {
            List<StationData> ret = new List<StationData>();
            List<long> stationIds = new List<long>();
            string portQuery = string.Empty;

            // Port query
            if (portTypes != null || portLevels != null || portStatuses != null)
            {
                portQuery = " Id IN (SELECT StationId FROM Port WHERE"
                    + (portTypes == null ? string.Empty : " PortType=any(@pt) AND")
                    + (portLevels == null ? string.Empty : " Level=any(@pl) AND")
                    + (portStatuses == null ? string.Empty : " Status=any(@ps) AND")
                    + (showHidden ? " 1=1" : " Deleted=false")
                    + ") AND";
            }

            this.Execute(
                "SELECT Id,Name,Description,Latitude,Longitude,InfoMessage,NetworkName,Phone,Country,Region,Town,PostIndex,Address,Web,"
                + "OpenHours,AccessType,PaymentType,Status"
                + (showHidden ? ",CreationDate,UpdateDate,Comments,Deleted" : string.Empty)
                + " FROM Station WHERE"
                + (string.IsNullOrEmpty(lastId) ? string.Empty : " Id>@p1 AND")
                + (accessTypes == null ? string.Empty : " AccessType=any(@at) AND")
                + (stationStatuses == null ? string.Empty : " Status=any(@ss) AND")
                + (country == null ? string.Empty : " Country=@c AND")
                + (region == null ? string.Empty : " Region=@r AND")
                + (town == null ? string.Empty : " Town=@t AND")
                + portQuery
                + (showHidden ? " 1=1" : " Deleted=false")
                + " ORDER BY Id LIMIT " + maxCount.ToString(),
                cmd =>
                {
                    if (!string.IsNullOrEmpty(lastId))
                    {
                        this.AddParam(cmd, "p1", Convert.ToInt64(lastId));
                    }

                    if (accessTypes != null)
                    {
                        cmd.Parameters.AddWithValue("at", accessTypes);
                    }

                    if (stationStatuses != null)
                    {
                        cmd.Parameters.AddWithValue("ss", stationStatuses);
                    }

                    if (portTypes != null)
                    {
                        cmd.Parameters.AddWithValue("pt", portTypes);
                    }

                    if (portLevels != null)
                    {
                        cmd.Parameters.AddWithValue("pl", portLevels);
                    }

                    if (portStatuses != null)
                    {
                        cmd.Parameters.AddWithValue("ps", portStatuses);
                    }

                    if (country != null)
                    {
                        cmd.Parameters.AddWithValue("c", country);
                    }

                    if (region != null)
                    {
                        cmd.Parameters.AddWithValue("r", region);
                    }

                    if (town != null)
                    {
                        cmd.Parameters.AddWithValue("t", town);
                    }

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            StationData station = new StationData();
                            station.Id = reader.GetInt64(0);
                            station.Name = this.GetParamString(reader, 1);
                            station.Description = this.GetParamString(reader, 2);
                            station.Latitude = this.GetParamDouble(reader, 3);
                            station.Longitude = this.GetParamDouble(reader, 4);
                            station.InfoMessage = this.GetParamString(reader, 5);
                            station.NetworkName = this.GetParamString(reader, 6);
                            station.Phone = this.GetParamLong(reader, 7);
                            station.Country = this.GetParamString(reader, 8);
                            station.Region = this.GetParamString(reader, 9);
                            station.Town = this.GetParamString(reader, 10);
                            station.PostIndex = this.GetParamString(reader, 11);
                            station.Address = this.GetParamString(reader, 12);
                            station.Web = this.GetParamString(reader, 13);
                            station.OpenHours = this.GetParamString(reader, 14);
                            station.AccessType = this.GetParamString(reader, 15);
                            station.PaymentType = this.GetParamString(reader, 16);
                            station.Status = this.GetParamString(reader, 17);

                            if (showHidden)
                            {
                                station.CreationDateEpochtime = this.GetParamLong(reader, 18);
                                station.UpdateDateEpochtime = this.GetParamLong(reader, 19);
                                station.Comments = this.GetParamString(reader, 20);
                                station.Deleted = this.GetParamBoolean(reader, 21);
                            }

                            ret.Add(station);

                            stationIds.Add(station.Id);
                        }
                    }
                });

            // Read port list
            Dictionary<long, List<PortData>> ports = this.GetPorts(stationIds, portTypes, portLevels, portStatuses, showHidden);

            foreach (var station in ret)
            {
                List<PortData> portList;

                if (ports.TryGetValue(station.Id, out portList))
                {
                    station.Ports = portList;
                }
            }

            return ret;
        }

        // Delete the station
        public void DeleteStation(StationData station, long who)
        {
            this.ExecuteDelete(station, who);
        }

        // Purge the station
        public void PurgeStation(StationData station, long who)
        {
            this.ExecutePurge(station, who);
        }

        // Purge the existing station, used only for testing
        public void PurgeStation(string name)
        {
            this.Execute(
                "DELETE FROM Station WHERE Name=@p",
                cmd =>
                {
                    this.AddParam(cmd, "p", name, 64);
                    cmd.ExecuteNonQuery();
                });
        }

        #endregion 
        
        #region Ports

        // Create new port, returns Id
        public long CreatePort(PortData port, long who)
        {
            long count = 0;

            if (!port.TariffId.HasValue)
            {
                throw new Exception("Не задан Id тарифа для порта");
            }

            this.CheckTariffExists(port.TariffId.Value);

            this.Execute(
                "SELECT COUNT(*) FROM Port WHERE StationId=@p",
                cmd =>
                {
                    this.AddParam(cmd, "p", port.StationId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            count = reader.GetInt64(0);
                        }
                    }
                });

            if (count > MaxPorts)
            {
                throw new Exception("Создано слишком много портов для одной станции. Обратитесь в поддержку");
            }

            return this.ExecuteCreate(
                port, 
                who,
                e =>
                {
                    if (e.Message.Contains("port_tariffid_fkey"))
                    {
                        throw new Exception(string.Format("Тариф с Id '{0}' не существует или удален", port.TariffId));
                    }

                    if (e.Message.Contains("port_stationid_fkey"))
                    {
                        throw new Exception(string.Format("Зарядная станция с Id '{0}' не существует или удалена", port.StationId));
                    }

                    return true;
                });
        }

        // Update info for the port using Id, only not null fields are updated
        public void UpdatePort(PortData port, long who)
        {
            if (port.TariffId.HasValue)
            {
                this.CheckTariffExists(port.TariffId.Value);
            }

            this.ExecuteUpdate(
                port, 
                who,
                e =>
                {
                    if (e.Message.Contains("port_tariffid_fkey"))
                    {
                        throw new Exception(string.Format("Тариф с Id '{0}' не существует или удален", port.TariffId));
                    }

                    if (e.Message.Contains("port_stationid_fkey"))
                    {
                        throw new Exception(string.Format("Зарядная станция с Id '{0}' не существует или удалена", port.StationId));
                    }

                    return true;
                });
        }

        // Get list of ports with info using Station Id
        public List<PortData> GetPorts(long stationId, bool showHidden)
        {
            List<PortData> portList = new List<PortData>();
            List<long> tariffIds = new List<long>();

            this.Execute(
                "SELECT Id,Name,PortType,Level,Voltage,Current,MaxPower,PowerSupply,TariffId,Status"
                + (showHidden ? ",Brand,Model,SerialNumber,CreationDate,UpdateDate,DriverName,ConnectionString,PortOrder,Comments,Deleted" : string.Empty)
                + " FROM Port WHERE StationId=@p"
                + (showHidden ? string.Empty : " AND Deleted=false")
                + " ORDER BY Id",
                cmd =>
                {
                    this.AddParam(cmd, "p", stationId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            PortData port = new PortData();
                            port.Id = reader.GetInt64(0);
                            port.StationId = stationId;
                            port.Name = this.GetParamString(reader, 1);
                            port.PortType = this.GetParamString(reader, 2);
                            port.Level = this.GetParamString(reader, 3);
                            port.Voltage = this.GetParamLong(reader, 4);
                            port.Current = this.GetParamLong(reader, 5);
                            port.MaxPower = this.GetParamDouble(reader, 6);
                            port.PowerSupply = this.GetParamString(reader, 7);
                            port.TariffId = this.GetParamLong(reader, 8);
                            port.Status = this.GetParamString(reader, 9);

                            if (showHidden)
                            {
                                port.Brand = this.GetParamString(reader, 10);
                                port.Model = this.GetParamString(reader, 11);
                                port.SerialNumber = this.GetParamString(reader, 12);
                                port.CreationDateEpochtime = this.GetParamLong(reader, 13);
                                port.UpdateDateEpochtime = this.GetParamLong(reader, 14);
                                port.DriverName = this.GetParamString(reader, 15);
                                port.ConnectionString = this.GetParamString(reader, 16);
                                port.PortOrder = this.GetParamLong(reader, 17);
                                port.Comments = this.GetParamString(reader, 18);
                                port.Deleted = this.GetParamBoolean(reader, 19);
                            }

                            portList.Add(port);

                            if (port.TariffId.HasValue)
                            {
                                tariffIds.Add(port.TariffId.Value);
                            }
                        }
                    }
                });

            // Read tariffs
            Dictionary<long, TariffData> tariffs = this.GetTariff(tariffIds, showHidden);
            
            foreach (var port in portList)
            {
                TariffData tariff;

                if (port.TariffId.HasValue && tariffs.TryGetValue(port.TariffId.Value, out tariff))
                {
                    port.Tariff = tariff;
                }
            }

            return portList;
        }

        // Get list of ports with not empty DriverName and not deleted, it does not read tariff and some other fields
        public List<PortData> GetPortsWithDriverName(bool showMore)
        {
            List<PortData> portList = new List<PortData>();

            this.Execute(
                "SELECT Id,StationId,DriverName,ConnectionString"
                + (showMore ? ",Name,PortType,Level,Voltage,Current,MaxPower,PowerSupply,Brand,Model,SerialNumber,PortOrder" : string.Empty)
                + " FROM Port WHERE DriverName<>'' AND Deleted=false ORDER BY Id",
                cmd =>
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            PortData port = new PortData();
                            port.Id = reader.GetInt64(0);
                            port.StationId = reader.GetInt64(1);
                            port.DriverName = this.GetParamString(reader, 2);
                            port.ConnectionString = this.GetParamString(reader, 3);

                            if (showMore)
                            {
                                port.Name = this.GetParamString(reader, 4);
                                port.PortType = this.GetParamString(reader, 5);
                                port.Level = this.GetParamString(reader, 6);
                                port.Voltage = this.GetParamLong(reader, 7);
                                port.Current = this.GetParamLong(reader, 8);
                                port.MaxPower = this.GetParamDouble(reader, 9);
                                port.PowerSupply = this.GetParamString(reader, 10);
                                port.Brand = this.GetParamString(reader, 11);
                                port.Model = this.GetParamString(reader, 12);
                                port.SerialNumber = this.GetParamString(reader, 13);
                                port.PortOrder = this.GetParamLong(reader, 14);
                            }

                            portList.Add(port);
                        }
                    }
                });

            return portList;
        }

        public Dictionary<long, List<PortData>> GetPorts(List<long> stationIds, string[] portTypes, string[] portLevels, string[] portStatuses, bool showHidden)
        {
            Dictionary<long, List<PortData>> ret = new Dictionary<long, List<PortData>>();
            List<long> tariffIds = new List<long>();

            if (stationIds.Count == 0)
            {
                return ret;
            }

            this.Execute(
                "SELECT Id,StationId,Name,PortType,Level,Voltage,Current,MaxPower,PowerSupply,TariffId,Status"
                + (showHidden ? ",Brand,Model,SerialNumber,CreationDate,UpdateDate,DriverName,ConnectionString,PortOrder,Comments,Deleted" : string.Empty)
                + " FROM Port WHERE StationId=any(@p)"
                + (portTypes == null ? string.Empty : " AND PortType=any(@pt)")
                + (portLevels == null ? string.Empty : " AND Level=any(@pl)")
                + (portStatuses == null ? string.Empty : " AND Status=any(@ps)")
                + (showHidden ? string.Empty : " AND Deleted=false"),
                cmd =>
                {
                    cmd.Parameters.AddWithValue("p", stationIds);

                    if (portTypes != null)
                    {
                        cmd.Parameters.AddWithValue("pt", portTypes);
                    }

                    if (portLevels != null)
                    {
                        cmd.Parameters.AddWithValue("pl", portLevels);
                    }

                    if (portStatuses != null)
                    {
                        cmd.Parameters.AddWithValue("ps", portStatuses);
                    }

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            PortData port = new PortData();
                            port.Id = reader.GetInt64(0);
                            port.StationId = this.GetParamLong(reader, 1);
                            port.Name = this.GetParamString(reader, 2);
                            port.PortType = this.GetParamString(reader, 3);
                            port.Level = this.GetParamString(reader, 4);
                            port.Voltage = this.GetParamLong(reader, 5);
                            port.Current = this.GetParamLong(reader, 6);
                            port.MaxPower = this.GetParamDouble(reader, 7);
                            port.PowerSupply = this.GetParamString(reader, 8);
                            port.TariffId = this.GetParamLong(reader, 9);
                            port.Status = this.GetParamString(reader, 10);

                            if (showHidden)
                            {
                                port.Brand = this.GetParamString(reader, 11);
                                port.Model = this.GetParamString(reader, 12);
                                port.SerialNumber = this.GetParamString(reader, 13);
                                port.CreationDateEpochtime = this.GetParamLong(reader, 14);
                                port.UpdateDateEpochtime = this.GetParamLong(reader, 15);
                                port.DriverName = this.GetParamString(reader, 16);
                                port.ConnectionString = this.GetParamString(reader, 17);
                                port.PortOrder = this.GetParamLong(reader, 18);
                                port.Comments = this.GetParamString(reader, 19);
                                port.Deleted = this.GetParamBoolean(reader, 20);
                            }

                            if (port.StationId.HasValue)
                            {
                                List<PortData> ports;

                                if (ret.TryGetValue(port.StationId.Value, out ports))
                                {
                                    ports.Add(port);
                                }
                                else
                                {
                                    ports = new List<PortData>();
                                    ports.Add(port);
                                    ret[port.StationId.Value] = ports;
                                }
                            }

                            if (port.TariffId.HasValue)
                            {
                                tariffIds.Add(port.TariffId.Value);
                            }
                        }
                    }
                });

            // Read tariffs
            Dictionary<long, TariffData> tariffs = this.GetTariff(tariffIds, showHidden);

            foreach (var station in ret)
            {
                foreach (var port in station.Value)
                {
                    TariffData tariff;

                    if (port.TariffId.HasValue && tariffs.TryGetValue(port.TariffId.Value, out tariff))
                    {
                        port.Tariff = tariff;
                    }
                }
            }

            return ret;
        }

        // Get port info using Id
        public PortData GetPort(long id, bool showHidden)
        {
            PortData port = new PortData();

            this.Execute(
                "SELECT StationId,Name,PortType,Level,Voltage,Current,MaxPower,PowerSupply,TariffId,Status"
                + (showHidden ? ",Brand,Model,SerialNumber,CreationDate,UpdateDate,DriverName,ConnectionString,PortOrder,Comments,Deleted" : string.Empty)
                + " FROM Port WHERE Id=@p"
                + (showHidden ? string.Empty : " AND Deleted=false"),
                cmd =>
                {
                    cmd.Parameters.AddWithValue("p", id);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            port.Id = id;
                            port.StationId = this.GetParamLong(reader, 0);
                            port.Name = this.GetParamString(reader, 1);
                            port.PortType = this.GetParamString(reader, 2);
                            port.Level = this.GetParamString(reader, 3);
                            port.Voltage = this.GetParamLong(reader, 4);
                            port.Current = this.GetParamLong(reader, 5);
                            port.MaxPower = this.GetParamDouble(reader, 6);
                            port.PowerSupply = this.GetParamString(reader, 7);
                            port.TariffId = this.GetParamLong(reader, 8);
                            port.Status = this.GetParamString(reader, 9);

                            if (showHidden)
                            {
                                port.Brand = this.GetParamString(reader, 10);
                                port.Model = this.GetParamString(reader, 11);
                                port.SerialNumber = this.GetParamString(reader, 12);
                                port.CreationDateEpochtime = this.GetParamLong(reader, 13);
                                port.UpdateDateEpochtime = this.GetParamLong(reader, 14);
                                port.DriverName = this.GetParamString(reader, 15);
                                port.ConnectionString = this.GetParamString(reader, 16);
                                port.PortOrder = this.GetParamLong(reader, 17);
                                port.Comments = this.GetParamString(reader, 18);
                                port.Deleted = this.GetParamBoolean(reader, 19);
                            }

                            return;
                        }

                        throw new Exception(string.Format("Порта с Id '{0}' не существует", id));
                    }
                });

            // Read tariff
            port.Tariff = this.GetTariff(port.TariffId.Value, showHidden);

            return port;
        }

        public Dictionary<long, PortData> GetPort(List<long> ids, bool showHidden)
        {
            Dictionary<long, PortData> ret = new Dictionary<long, PortData>();
            List<long> tariffIds = new List<long>();

            if (ids.Count == 0)
            {
                return ret;
            }

            this.Execute(
                "SELECT Id,StationId,Name,PortType,Level,Voltage,Current,MaxPower,PowerSupply,TariffId,Status"
                + (showHidden ? ",Brand,Model,SerialNumber,CreationDate,UpdateDate,DriverName,ConnectionString,PortOrder,Comments,Deleted" : string.Empty)
                + " FROM Port WHERE Id=any(@p)",
                /* + (showHidden ? string.Empty : " AND Deleted=false"), */
                cmd =>
                {
                    cmd.Parameters.AddWithValue("p", ids);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            PortData port = new PortData();
                            port.Id = reader.GetInt64(0);
                            port.StationId = this.GetParamLong(reader, 1);
                            port.Name = this.GetParamString(reader, 2);
                            port.PortType = this.GetParamString(reader, 3);
                            port.Level = this.GetParamString(reader, 4);
                            port.Voltage = this.GetParamLong(reader, 5);
                            port.Current = this.GetParamLong(reader, 6);
                            port.MaxPower = this.GetParamDouble(reader, 7);
                            port.PowerSupply = this.GetParamString(reader, 8);
                            port.TariffId = this.GetParamLong(reader, 9);
                            port.Status = this.GetParamString(reader, 10);

                            if (showHidden)
                            {
                                port.Brand = this.GetParamString(reader, 11);
                                port.Model = this.GetParamString(reader, 12);
                                port.SerialNumber = this.GetParamString(reader, 13);
                                port.CreationDateEpochtime = this.GetParamLong(reader, 14);
                                port.UpdateDateEpochtime = this.GetParamLong(reader, 15);
                                port.DriverName = this.GetParamString(reader, 16);
                                port.ConnectionString = this.GetParamString(reader, 17);
                                port.PortOrder = this.GetParamLong(reader, 18);
                                port.Comments = this.GetParamString(reader, 19);
                                port.Deleted = this.GetParamBoolean(reader, 20);
                            }

                            ret[port.Id] = port;

                            if (port.TariffId.HasValue)
                            {
                                tariffIds.Add(port.TariffId.Value);
                            }
                        }
                    }
                });

            // Read tariffs
            Dictionary<long, TariffData> tariffs = this.GetTariff(tariffIds, showHidden);

            foreach (var port in ret)
            {
                TariffData tariff;

                if (port.Value.TariffId.HasValue && tariffs.TryGetValue(port.Value.TariffId.Value, out tariff))
                {
                    port.Value.Tariff = tariff;
                }
            }

            return ret;
        }

        // Delete the port
        public void DeletePort(PortData port, long who)
        {
            this.ExecuteDelete(port, who);
        }

        // Purge the port
        public void PurgePort(PortData port, long who)
        {
            this.ExecutePurge(port, who);
        }

        // Purge the existing port, used only for testing
        public void PurgePort(long stationId)
        {
            this.Execute(
                "DELETE FROM Port WHERE StationId=@p",
                cmd =>
                {
                    this.AddParam(cmd, "p", stationId);
                    cmd.ExecuteNonQuery();
                });
        }

        #endregion 

        #region Tariffs

        // Create new tariff, returns Id
        public long CreateTariff(TariffData tariff, long who)
        {
            return this.ExecuteCreate(
                tariff,
                who,
                e =>
                {
                    if (e.Message.Contains("idx_tariff_name"))
                    {
                        throw new Exception(string.Format("Тариф с именем '{0}' уже существует", tariff.Name));
                    }

                    return true;
                });
        }

        // Update info for the tariff using Id, only not null fields are updated
        public void UpdateTariff(TariffData tariff, long who)
        {
            this.ExecuteUpdate(
                tariff,
                who,
                e =>
                {
                    if (e.Message.Contains("idx_tariff_name"))
                    {
                        throw new Exception(string.Format("Тариф с именем '{0}' уже существует", tariff.Name));
                    }

                    return true;
                });
        }

        // Get tariff with info using Id
        public TariffData GetTariff(long id, bool showHidden)
        {
            TariffData tariff = new TariffData();

            this.Execute(
                "SELECT Description,PaymentReqired,PriceType"
                + (showHidden ? ",Name,Price,Currency,Comments,Deleted" : string.Empty)
                + " FROM Tariff WHERE Id=@p"
                + (showHidden ? string.Empty : " AND Deleted=false"),
                cmd =>
                {
                    this.AddParam(cmd, "p", id);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            tariff.Id = id;
                            tariff.Description = this.GetParamString(reader, 0);
                            tariff.PaymentReqired = this.GetParamBoolean(reader, 1);
                            tariff.PriceType = this.GetParamString(reader, 2);

                            if (showHidden)
                            {
                                tariff.Name = this.GetParamString(reader, 3);
                                tariff.Price = this.GetParamDouble(reader, 4);
                                tariff.Currency = this.GetParamString(reader, 5);
                                tariff.Comments = this.GetParamString(reader, 6);
                                tariff.Deleted = this.GetParamBoolean(reader, 7);
                            }

                            return;
                        }

                        throw new Exception(string.Format("Тарифа с Id '{0}' не существует", id));
                    }
                });

            return tariff;
        }

        // Get tariff with info using Id
        public Dictionary<long, TariffData> GetTariff(List<long> ids, bool showHidden)
        {
            Dictionary<long, TariffData> ret = new Dictionary<long, TariffData>();

            if (ids.Count == 0)
            {
                return ret;
            }

            this.Execute(
                "SELECT Id,Description,PaymentReqired,PriceType"
                + (showHidden ? ",Name,Price,Currency,Comments,Deleted" : string.Empty)
                + " FROM Tariff WHERE Id=any(@p)",
                /* + (showHidden ? string.Empty : " AND Deleted=false"), */
                cmd =>
                {
                    cmd.Parameters.AddWithValue("p", ids);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            TariffData tariff = new TariffData();
                            tariff.Id = reader.GetInt64(0);
                            tariff.Description = this.GetParamString(reader, 1);
                            tariff.PaymentReqired = this.GetParamBoolean(reader, 2);
                            tariff.PriceType = this.GetParamString(reader, 3);

                            if (showHidden)
                            {
                                tariff.Name = this.GetParamString(reader, 4);
                                tariff.Price = this.GetParamDouble(reader, 5);
                                tariff.Currency = this.GetParamString(reader, 6);
                                tariff.Comments = this.GetParamString(reader, 7);
                                tariff.Deleted = this.GetParamBoolean(reader, 8);
                            }

                            ret[tariff.Id] = tariff;
                        }
                    }
                });

            return ret;
        }

        // Search for tariffs
        public List<TariffData> SearchTariffs(string lastId, string name, string description, long maxCount)
        {
            List<TariffData> ret = new List<TariffData>();

            this.Execute(
                "SELECT Id,Name,Description,PaymentReqired,PriceType,Price,Currency,Comments,Deleted FROM Tariff"
                + " WHERE" + (string.IsNullOrEmpty(lastId) ? string.Empty : " Id>@p1 AND")
                + " LOWER(COALESCE(Name,'')) LIKE @p2 AND LOWER(COALESCE(Description,'')) LIKE @p3"
                + " ORDER BY Id LIMIT " + maxCount.ToString(),
                cmd =>
                {
                    if (!string.IsNullOrEmpty(lastId))
                    {
                        this.AddParam(cmd, "p1", Convert.ToInt64(lastId));
                    }

                    this.AddSearchParam(cmd, "p2", name, true);
                    this.AddSearchParam(cmd, "p3", description, true);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            TariffData tariff = new TariffData();
                            tariff.Id = reader.GetInt64(0);
                            tariff.Name = this.GetParamString(reader, 1);
                            tariff.Description = this.GetParamString(reader, 2);
                            tariff.PaymentReqired = this.GetParamBoolean(reader, 3);
                            tariff.PriceType = this.GetParamString(reader, 4);
                            tariff.Price = this.GetParamDouble(reader, 5);
                            tariff.Currency = this.GetParamString(reader, 6);
                            tariff.Comments = this.GetParamString(reader, 7);
                            tariff.Deleted = this.GetParamBoolean(reader, 8);
                            ret.Add(tariff);
                        }
                    }
                });

            return ret;
        }

        // Delete the tariff
        public void DeleteTariff(TariffData tariff, long who)
        {
            this.ExecuteDelete(tariff, who);
        }

        // Purge the tariff
        public void PurgeTariff(TariffData tariff, long who)
        {
            this.ExecutePurge(
                tariff,
                who,
                e =>
                {
                    if (e.Message.Contains("port_tariffid_fkey"))
                    {
                        throw new Exception(string.Format("Тариф '{0}' нельзя стереть, поскольку есть порты, что его еще используют", tariff.Name));
                    }

                    return true;
                });
        }

        // Purge the existing tariff, used only for testing
        public void PurgeTariff(string name)
        {
            this.Execute(
                "DELETE FROM Tariff WHERE Name=@p",
                cmd =>
                {
                    this.AddParam(cmd, "p", name, 64);
                    cmd.ExecuteNonQuery();
                });
        }

        // Check that Tariff exists and not deleted
        public void CheckTariffExists(long id)
        {
            if (!(bool)this.Execute(
                "SELECT Id FROM Tariff WHERE Id=@p AND Deleted=false",
                cmd =>
                {
                    this.AddParam(cmd, "p", id);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return true;
                        }

                        return false;
                    }
                }))
            {
                throw new Exception(string.Format("Тариф с Id '{0}' не существует или удален", id));
            }
        }

        #endregion 

        #region ChargingSessions

        // Create the new charging session, returns Id, if it has not empty PaymentData, then it is prepaid
        public long CreateChargingSession(ChargingSessionData session, long who)
        {
            return this.ExecuteCreate(session, who);
        }

        // Update info for the charging session using Id, only not null fields are updated
        public void UpdateChargingSession(ChargingSessionData session, long who)
        {
            this.ExecuteUpdate(session, who);
        }

        // Get charging session with info using Id
        public ChargingSessionData GetChargingSession(long id, long customerId, bool showHidden)
        {
            ChargingSessionData session = new ChargingSessionData();

            this.Execute(
                "SELECT CustomerId,CarId,StationId,PortId,SessionStartDate,SessionStopDate,ChargeStartDate,ChargeStopDate,ChargeTime,EnergyConsumed,"
                + "PaymentAmount,PriceType,Price,Currency,Status,PaymentId,MaxChargeTime,MaxEnergyConsumed,Voltage,Current,Power,DriverStatus,"
                + "EnergyMeterStart,EnergyMeterStop,StopReason,LastValueDate FROM ChargingSession WHERE Id=@p AND CustomerId=@p2",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("p", id);
                    cmd.Parameters.AddWithValue("p2", customerId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            session.Id = id;
                            session.CustomerId = this.GetParamLong(reader, 0);
                            session.CarId = this.GetParamLong(reader, 1);
                            session.StationId = this.GetParamLong(reader, 2);
                            session.PortId = this.GetParamLong(reader, 3);
                            session.SessionStartDateEpochtime = this.GetParamLong(reader, 4);
                            session.SessionStopDateEpochtime = this.GetParamLong(reader, 5);
                            session.ChargeStartDateEpochtime = this.GetParamLong(reader, 6);
                            session.ChargeStopDateEpochtime = this.GetParamLong(reader, 7);
                            session.ChargeTime = this.GetParamLong(reader, 8);
                            session.EnergyConsumed = this.GetParamDouble(reader, 9);
                            session.PaymentAmount = this.GetParamDouble(reader, 10);
                            session.PriceType = this.GetParamString(reader, 11);
                            session.Price = this.GetParamDouble(reader, 12);
                            session.Currency = this.GetParamString(reader, 13);
                            session.Status = this.GetParamString(reader, 14);
                            session.PaymentId = this.GetParamLong(reader, 15);
                            session.MaxChargeTime = this.GetParamLong(reader, 16);
                            session.MaxEnergyConsumed = this.GetParamDouble(reader, 17);
                            session.Voltage = this.GetParamDouble(reader, 18);
                            session.Current = this.GetParamDouble(reader, 19);
                            session.Power = this.GetParamDouble(reader, 20);
                            session.DriverStatus = this.GetParamString(reader, 21);
                            session.EnergyMeterStart = this.GetParamDouble(reader, 22);
                            session.EnergyMeterStop = this.GetParamDouble(reader, 23);
                            session.StopReason = this.GetParamString(reader, 24);
                            session.LastValueDateEpochtime = this.GetParamLong(reader, 25);

                            return;
                        }

                        throw new Exception(string.Format("Зарядной сессии с Id '{0}' не существует", id));
                    }
                });

            // Read station
            try
            {
                session.Station = this.GetStation(session.StationId.Value, showHidden);
            }
            catch
            {
                // Nothing if it does not exist
            }

            // Read port
            try
            {
                session.Port = this.GetPort(session.PortId.Value, showHidden);
            }
            catch
            {
                // Nothing if it does not exist
            }

            // Read payment
            if (session.PaymentId.HasValue && session.PaymentId.Value > 0)
            {
                try
                {
                    session.Payment = this.GetPayment(session.PaymentId.Value, showHidden);
                }
                catch
                {
                    // Nothing if it does not exist
                }
            }

            return session;
        }

        // Search for stations and ports in the town
        public List<ChargingSessionData> SearchChargingSession(string lastId, long? customerId, long? stationId, long? portId, long startTime, long stopTime, string[] statuses, bool showHidden, long maxCount)
        {
            List<ChargingSessionData> ret = new List<ChargingSessionData>();
            List<long> customerIds = new List<long>();
            List<long> carIds = new List<long>();
            List<long> stationIds = new List<long>();
            List<long> portIds = new List<long>();
            List<long> paymentIds = new List<long>();

            this.Execute(
                "SELECT Id,CustomerId,CarId,StationId,PortId,SessionStartDate,SessionStopDate,ChargeStartDate,ChargeStopDate,ChargeTime,EnergyConsumed,"
                + "PaymentAmount,PriceType,Price,Currency,Status,PaymentId,MaxChargeTime,MaxEnergyConsumed,Voltage,Current,Power,DriverStatus,"
                + "EnergyMeterStart,EnergyMeterStop,StopReason,LastValueDate FROM ChargingSession WHERE"
                + (string.IsNullOrEmpty(lastId) ? string.Empty : " Id>@p1 AND")
                + (!customerId.HasValue ? string.Empty : " CustomerId=@p2 AND")
                + (!stationId.HasValue ? string.Empty : " StationId=@p3 AND")
                + (!portId.HasValue ? string.Empty : " PortId=@p4 AND")
                + (statuses == null ? string.Empty : " Status=any(@s) AND")
                + " SessionStartDate>=@p5 AND SessionStartDate<=@p6"
                + " ORDER BY Id LIMIT " + maxCount.ToString(),
                cmd =>
                {
                    if (!string.IsNullOrEmpty(lastId))
                    {
                        this.AddParam(cmd, "p1", Convert.ToInt64(lastId));
                    }

                    if (customerId.HasValue)
                    {
                        this.AddParam(cmd, "p2", customerId);
                    }

                    if (stationId.HasValue)
                    {
                        this.AddParam(cmd, "p3", stationId);
                    }

                    if (portId.HasValue)
                    {
                        this.AddParam(cmd, "p4", portId);
                    }

                    if (statuses != null)
                    {
                        cmd.Parameters.AddWithValue("s", statuses);
                    }

                    this.AddParam(cmd, "p5", startTime);
                    this.AddParam(cmd, "p6", stopTime);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ChargingSessionData session = new ChargingSessionData();
                            session.Id = reader.GetInt64(0);
                            session.CustomerId = this.GetParamLong(reader, 1);
                            session.CarId = this.GetParamLong(reader, 2);
                            session.StationId = this.GetParamLong(reader, 3);
                            session.PortId = this.GetParamLong(reader, 4);
                            session.SessionStartDateEpochtime = this.GetParamLong(reader, 5);
                            session.SessionStopDateEpochtime = this.GetParamLong(reader, 6);
                            session.ChargeStartDateEpochtime = this.GetParamLong(reader, 7);
                            session.ChargeStopDateEpochtime = this.GetParamLong(reader, 8);
                            session.ChargeTime = this.GetParamLong(reader, 9);
                            session.EnergyConsumed = this.GetParamDouble(reader, 10);
                            session.PaymentAmount = this.GetParamDouble(reader, 11);
                            session.PriceType = this.GetParamString(reader, 12);
                            session.Price = this.GetParamDouble(reader, 13);
                            session.Currency = this.GetParamString(reader, 14);
                            session.Status = this.GetParamString(reader, 15);
                            session.PaymentId = this.GetParamLong(reader, 16);
                            session.MaxChargeTime = this.GetParamLong(reader, 17);
                            session.MaxEnergyConsumed = this.GetParamDouble(reader, 18);
                            session.Voltage = this.GetParamDouble(reader, 19);
                            session.Current = this.GetParamDouble(reader, 20);
                            session.Power = this.GetParamDouble(reader, 21);
                            session.DriverStatus = this.GetParamString(reader, 22);
                            session.EnergyMeterStart = this.GetParamDouble(reader, 23);
                            session.EnergyMeterStop = this.GetParamDouble(reader, 24);
                            session.StopReason = this.GetParamString(reader, 25);
                            session.LastValueDateEpochtime = this.GetParamLong(reader, 26);

                            ret.Add(session);

                            if (showHidden)
                            {
                                customerIds.Add(session.CustomerId.Value);

                                if (session.CarId.HasValue)
                                {
                                    carIds.Add(session.CarId.Value);
                                }
                            }

                            stationIds.Add(session.StationId.Value);
                            portIds.Add(session.PortId.Value);

                            if (session.PaymentId.HasValue)
                            {
                                paymentIds.Add(session.PaymentId.Value);
                            }
                        }
                    }
                });

            // Only for admins
            if (showHidden)
            {
                // Read customers
                Dictionary<long, CustomerData> customers = this.GetCustomer(customerIds, showHidden);

                foreach (var session in ret)
                {
                    CustomerData customer;

                    if (session.CustomerId.HasValue && customers.TryGetValue(session.CustomerId.Value, out customer))
                    {
                        session.Customer = customer;
                    }
                }

                // Read cars
                Dictionary<long, CarData> cars = this.GetCar(carIds, showHidden);

                foreach (var session in ret)
                {
                    CarData car;

                    if (session.CarId.HasValue && cars.TryGetValue(session.CarId.Value, out car))
                    {
                        session.Car = car;
                    }
                }
            }

            // Read stations
            Dictionary<long, StationData> stations = this.GetStation(stationIds, showHidden);

            foreach (var session in ret)
            {
                StationData station;

                if (session.StationId.HasValue && stations.TryGetValue(session.StationId.Value, out station))
                {
                    session.Station = station;
                }
            }

            // Read ports
            Dictionary<long, PortData> ports = this.GetPort(portIds, showHidden);

            foreach (var session in ret)
            {
                PortData port;

                if (session.PortId.HasValue && ports.TryGetValue(session.PortId.Value, out port))
                {
                    session.Port = port;
                }
            }

            // Read payments
            Dictionary<long, PaymentData> payments = this.GetPayment(paymentIds, showHidden);

            foreach (var session in ret)
            {
                PaymentData payment;

                if (session.PaymentId.HasValue && payments.TryGetValue(session.PaymentId.Value, out payment))
                {
                    session.Payment = payment;
                }
            }

            return ret;
        }

        // Purge the existing charging sessions, used only for testing
        public void PurgeChargingSession(long customerId)
        {
            this.Execute(
                "DELETE FROM ChargingSession WHERE CustomerId=@p",
                cmd =>
                {
                    this.AddParam(cmd, "p", customerId);
                    cmd.ExecuteNonQuery();
                });
        }

        #endregion

        #region Payments

        // Create the new payment, returns Id
        public long CreatePayment(PaymentData payment, long who)
        {
            return this.ExecuteCreate(payment, who);
        }

        // Update info for the payment using Id, only not null fields are updated
        public void UpdatePayment(PaymentData payment, long who)
        {
            this.ExecuteUpdate(payment, who);
        }

        // Get payment with info using Id
        public PaymentData GetPayment(long id, bool showHidden)
        {
            PaymentData payment = new PaymentData();

            this.Execute(
                "SELECT CustomerId,AmountPaid,Currency,CreationDate,PaidDate,PaymentService,Status"
                + (showHidden ? ",AmountHold,Commission,AmountReceived,HoldDate,ExternalPaymentId,ErrorDescription,IP" : string.Empty)
                + " FROM Payment WHERE Id=@p",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("p", id);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            payment.Id = id;
                            payment.CustomerId = reader.GetInt64(0);
                            payment.AmountPaid = this.GetParamDouble(reader, 1);
                            payment.Currency = this.GetParamString(reader, 2);
                            payment.CreationDateEpochtime = this.GetParamLong(reader, 3);
                            payment.PaidDateEpochtime = this.GetParamLong(reader, 4);
                            payment.PaymentService = this.GetParamString(reader, 5);
                            payment.Status = this.GetParamString(reader, 6);

                            if (showHidden)
                            {
                                payment.AmountHold = this.GetParamDouble(reader, 7);
                                payment.Commission = this.GetParamDouble(reader, 8);
                                payment.AmountReceived = this.GetParamDouble(reader, 9);
                                payment.HoldDateEpochtime = this.GetParamLong(reader, 10);
                                payment.ExternalPaymentId = this.GetParamLong(reader, 11);
                                payment.ErrorDescription = this.GetParamString(reader, 12);
                                payment.IP = this.GetParamString(reader, 13);
                            }

                            return;
                        }

                        throw new Exception(string.Format("Платежа с Id '{0}' не существует", id));
                    }
                });

            return payment;
        }

        // Get list of payments with info using Id
        public Dictionary<long, PaymentData> GetPayment(List<long> ids, bool showHidden)
        {
            Dictionary<long, PaymentData> ret = new Dictionary<long, PaymentData>();

            if (ids.Count == 0)
            {
                return ret;
            }

            this.Execute(
                "SELECT Id,CustomerId,AmountPaid,Currency,CreationDate,PaidDate,PaymentService,Status"
                + (showHidden ? ",AmountHold,Commission,AmountReceived,HoldDate,ExternalPaymentId,ErrorDescription,IP" : string.Empty)
                + " FROM Payment WHERE Id=any(@p)",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("p", ids);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            PaymentData payment = new PaymentData();
                            payment.Id = reader.GetInt64(0);
                            payment.CustomerId = reader.GetInt64(1);
                            payment.AmountPaid = this.GetParamDouble(reader, 2);
                            payment.Currency = this.GetParamString(reader, 3);
                            payment.CreationDateEpochtime = this.GetParamLong(reader, 4);
                            payment.PaidDateEpochtime = this.GetParamLong(reader, 5);
                            payment.PaymentService = this.GetParamString(reader, 6);
                            payment.Status = this.GetParamString(reader, 7);

                            if (showHidden)
                            {
                                payment.AmountHold = this.GetParamDouble(reader, 8);
                                payment.Commission = this.GetParamDouble(reader, 9);
                                payment.AmountReceived = this.GetParamDouble(reader, 10);
                                payment.HoldDateEpochtime = this.GetParamLong(reader, 11);
                                payment.ExternalPaymentId = this.GetParamLong(reader, 12);
                                payment.ErrorDescription = this.GetParamString(reader, 13);
                                payment.IP = this.GetParamString(reader, 14);
                            }

                            ret[payment.Id] = payment;
                        }
                    }
                });

            return ret;
        }

        // Purge the payment, used only for testing
        public void PurgePayment(long customerId)
        {
            this.Execute(
                "DELETE FROM Payment WHERE CustomerId=@p",
                cmd =>
                {
                    this.AddParam(cmd, "p", customerId);
                    cmd.ExecuteNonQuery();
                });
        }

        #endregion

        #region private

        private void CreateTables()
        {
            this.CreateTableAudit();
            this.CreateTableSession();
            this.CreateTableCustomer();
            this.CreateTableCar();
            this.CreateTableRFID();
            this.CreateTableTariff();
            this.CreateTableStation();
            this.CreateTablePort();
            this.CreateTablePayment();
            this.CreateTableChargingSession();
        }

        private void CreateTableAudit()
        {
            this.Execute(
                "CREATE TABLE IF NOT EXISTS Audit"
                + "("
                + "TableName VARCHAR(32) NOT NULL,"
                + "Id BIGINT NOT NULL,"
                + "ColumnName VARCHAR(32) NOT NULL,"
                + "CreationDate BIGINT NOT NULL,"
                + "Value TEXT,"
                + "CustomerId BIGINT NOT NULL"
                + ")");

            this.Execute("CREATE UNIQUE INDEX IF NOT EXISTS Idx_Audit_TableName ON Audit(TableName, Id, ColumnName, CreationDate)");

            this.Execute("CREATE INDEX IF NOT EXISTS Idx_Audit_CustomerId ON Audit(CustomerId, CreationDate)");
        }

        private void CreateTableCar()
        {
            this.Execute("CREATE SEQUENCE IF NOT EXISTS Seq_Car_Id");

            this.Execute(
                "CREATE TABLE IF NOT EXISTS Car"
                + "("
                + "Id BIGINT PRIMARY KEY DEFAULT NEXTVAL('Seq_Car_Id'),"
                + "CustomerId BIGINT NOT NULL REFERENCES Customer(Id) ON DELETE CASCADE,"
                + "Brand VARCHAR(32),"
                + "Model VARCHAR(32),"
                + "Year BIGINT,"
                + "RegNumber VARCHAR(10),"
                + "VIN VARCHAR(17),"
                + "UpdateDate BIGINT,"
                + "Comments VARCHAR(128),"
                + "Deleted BOOLEAN"
                + ")");

            this.Execute("CREATE INDEX IF NOT EXISTS Idx_Car_CustomerId ON Car(CustomerId, Deleted)");
        }

        private void CreateTableRFID()
        {
            this.Execute("CREATE SEQUENCE IF NOT EXISTS Seq_RFID_Id");

            this.Execute(
                "CREATE TABLE IF NOT EXISTS RFID"
                + "("
                + "Id BIGINT PRIMARY KEY DEFAULT NEXTVAL('Seq_RFID_Id'),"
                + "CustomerId BIGINT NOT NULL REFERENCES Customer(Id) ON DELETE CASCADE,"
                + "Value VARCHAR(20) NOT NULL,"
                + "Blocked BOOLEAN,"
                + "CreationDate BIGINT,"
                + "Comments VARCHAR(128),"
                + "Deleted BOOLEAN"
                + ")");

            this.Execute("CREATE INDEX IF NOT EXISTS Idx_RFID_CustomerId ON RFID(CustomerId, Deleted)");
            this.Execute("CREATE UNIQUE INDEX IF NOT EXISTS Idx_RFID_Value ON RFID(Value)");
        }

        private void CreateTableCustomer()
        {
            this.Execute("CREATE SEQUENCE IF NOT EXISTS Seq_Customer_Id");

            this.Execute(
                "CREATE TABLE IF NOT EXISTS Customer"
                + "("
                + "Id BIGINT PRIMARY KEY DEFAULT NEXTVAL('Seq_Customer_Id'),"
                + "Phone BIGINT NOT NULL,"
                + "Email VARCHAR(64) NOT NULL,"
                + "OrganizationName VARCHAR(128),"
                + "FirstName VARCHAR(32),"
                + "MiddleName VARCHAR(32),"
                + "LastName VARCHAR(32),"
                + "Sex BOOLEAN,"
                + "BirthDate BIGINT,"
                + "Password VARCHAR(64) NOT NULL,"
                + "Country VARCHAR(32),"
                + "Region VARCHAR(32),"
                + "Town VARCHAR(32),"
                + "PostIndex VARCHAR(16),"
                + "Address VARCHAR(128),"
                + "CreationDate BIGINT,"
                + "UpdateDate BIGINT,"
                + "Language VARCHAR(2),"
                + "CustomerType VARCHAR(16),"
                + "AccessRights VARCHAR(16),"
                + "SecretQuestion VARCHAR(64),"
                + "SecretAnswer VARCHAR(32),"
                + "IMEA VARCHAR(20),"
                + "Comments VARCHAR(128),"
                + "Deleted BOOLEAN"
                + ")");

            this.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_customer_email ON Customer(Email)");

            this.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_customer_phone ON Customer(Phone)");
        }

        private void CreateTablePort()
        {
            this.Execute("CREATE SEQUENCE IF NOT EXISTS Seq_Port_Id");

            this.Execute(
                "CREATE TABLE IF NOT EXISTS Port"
                + "("
                + "Id BIGINT PRIMARY KEY DEFAULT NEXTVAL('Seq_Port_Id'),"
                + "StationId BIGINT NOT NULL REFERENCES Station(Id) ON DELETE CASCADE,"
                + "Brand VARCHAR(32),"
                + "Model VARCHAR(32),"
                + "SerialNumber VARCHAR(32),"
                + "Name VARCHAR(64),"
                + "PortType VARCHAR(32) NOT NULL,"
                + "Level VARCHAR(16) NOT NULL,"
                + "Voltage BIGINT,"
                + "Current BIGINT,"
                + "MaxPower DOUBLE PRECISION,"
                + "PowerSupply VARCHAR(16),"
                + "TariffId BIGINT NOT NULL REFERENCES Tariff(Id),"
                + "Status VARCHAR(32) NOT NULL,"
                + "CreationDate BIGINT,"
                + "UpdateDate BIGINT,"
                + "DriverName VARCHAR(16),"
                + "ConnectionString VARCHAR(128),"
                + "PortOrder BIGINT,"
                + "Comments VARCHAR(128),"
                + "Deleted BOOLEAN"
                + ")");

            this.Execute("CREATE INDEX IF NOT EXISTS Idx_Port_StationId ON Port(StationId)");

            this.Execute("CREATE INDEX IF NOT EXISTS Idx_Port_PortType ON Port(PortType,Deleted)");

            this.Execute("CREATE INDEX IF NOT EXISTS Idx_Level_PortType ON Port(Level,Deleted)");

            this.Execute("CREATE INDEX IF NOT EXISTS Idx_Level_Status ON Port(Status,Deleted)");

            // Fix: Add DriverName if it does not exist
            if (!this.CheckTableColumnName("Port", "DriverName"))
            {
                this.Execute("ALTER TABLE Port ADD COLUMN DriverName VARCHAR(16)");
            }

            // Fix: Add ConnectionString if it does not exist
            if (!this.CheckTableColumnName("Port", "ConnectionString"))
            {
                this.Execute("ALTER TABLE Port ADD COLUMN ConnectionString VARCHAR(128)");
            }

            // Fix: Add PortOrder if it does not exist
            if (!this.CheckTableColumnName("Port", "PortOrder"))
            {
                this.Execute("ALTER TABLE Port ADD COLUMN PortOrder BIGINT");
            }
        }

        private void CreateTableSession()
        {
            this.Execute("CREATE SEQUENCE IF NOT EXISTS Seq_Session_Id");

            // if DeletionDate does not exist, delete the table
            if (!this.CheckTableColumnName("Session", "DeletionDate"))
            {
                try
                {
                    this.Execute("DROP INDEX Idx_Session_CustomerId");
                    this.Execute("DROP TABLE Session");
                }
                catch
                {
                }
            }

            this.Execute(
                "CREATE TABLE IF NOT EXISTS Session"
                + "("
                + "Id BIGINT PRIMARY KEY DEFAULT NEXTVAL('Seq_Session_Id'),"
                + "SessionToken VARCHAR(32) NOT NULL,"
                + "CustomerId BIGINT NOT NULL,"
                + "AccessRights VARCHAR(16),"
                + "IP VARCHAR(15),"
                + "CreationDate BIGINT,"
                + "ExpirationDate BIGINT,"
                + "DeletionDate BIGINT,"
                + "UserAgent VARCHAR(128),"
                + "Deleted BOOLEAN"
                + ")");

            this.Execute("CREATE INDEX IF NOT EXISTS Idx_Session_CustomerId ON Session(CustomerId, Deleted)");
            this.Execute("CREATE INDEX IF NOT EXISTS Idx_Session_CreationDate ON Session(CreationDate)");
            this.Execute("CREATE UNIQUE INDEX IF NOT EXISTS Idx_Session_SessionToken ON Session(SessionToken)");
        }

        private void CreateTableTariff()
        {
            this.Execute("CREATE SEQUENCE IF NOT EXISTS Seq_Tariff_Id");

            this.Execute(
                "CREATE TABLE IF NOT EXISTS Tariff"
                + "("
                + "Id BIGINT PRIMARY KEY DEFAULT NEXTVAL('Seq_Tariff_Id'),"
                + "Name VARCHAR(64) NOT NULL,"
                + "Description VARCHAR(255),"
                + "PaymentReqired BOOLEAN,"
                + "PriceType VARCHAR(16),"
                + "Price DOUBLE PRECISION,"
                + "Currency VARCHAR(3),"
                + "Comments VARCHAR(128),"
                + "Deleted BOOLEAN"
                + ")");

            this.Execute("CREATE UNIQUE INDEX IF NOT EXISTS Idx_Tariff_Name ON Tariff(Name)");
        }

        private void CreateTableStation()
        {
            this.Execute("CREATE SEQUENCE IF NOT EXISTS Seq_Station_Id");

            this.Execute(
                "CREATE TABLE IF NOT EXISTS Station"
                + "("
                + "Id BIGINT PRIMARY KEY DEFAULT NEXTVAL('Seq_Station_Id'),"
                + "Name VARCHAR(64) NOT NULL,"
                + "Description VARCHAR(255),"
                + "Latitude DOUBLE PRECISION NOT NULL,"
                + "Longitude DOUBLE PRECISION NOT NULL,"
                + "InfoMessage VARCHAR(255),"
                + "NetworkName VARCHAR(128) NOT NULL,"
                + "Phone BIGINT,"
                + "Country VARCHAR(32),"
                + "Region VARCHAR(32),"
                + "Town VARCHAR(32),"
                + "PostIndex VARCHAR(16),"
                + "Address VARCHAR(128),"
                + "Web VARCHAR(128),"
                + "OpenHours VARCHAR(64),"
                + "AccessType VARCHAR(16),"
                + "PaymentType VARCHAR(64),"
                + "Status VARCHAR(32),"
                + "CreationDate BIGINT,"
                + "UpdateDate BIGINT,"
                + "Comments VARCHAR(128),"
                + "Deleted BOOLEAN"
                + ")");

            this.Execute("CREATE INDEX IF NOT EXISTS Idx_Station_GPS ON Station(Latitude,Longitude)");

            this.Execute("CREATE UNIQUE INDEX IF NOT EXISTS Idx_Station_Name ON Station(Name)");

            this.Execute("CREATE INDEX IF NOT EXISTS Idx_Station_NetworkName ON Station(NetworkName)");

            this.Execute("CREATE INDEX IF NOT EXISTS Idx_Station_AccessType ON Station(AccessType,Deleted)");

            this.Execute("CREATE INDEX IF NOT EXISTS Idx_Station_Status ON Station(Status,Deleted)");

            this.Execute("CREATE INDEX IF NOT EXISTS Idx_Station_Country ON Station(Country,Region,Town,Deleted)");

            this.Execute("CREATE INDEX IF NOT EXISTS Idx_Station_Region ON Station(Region,Deleted)");

            this.Execute("CREATE INDEX IF NOT EXISTS Idx_Station_Town ON Station(Town,Deleted)");
        }

        private void CreateTableChargingSession()
        {
            this.Execute("CREATE SEQUENCE IF NOT EXISTS Seq_ChargingSession_Id MINVALUE " + DateTime.Now.ToEpochtime().ToString());

            this.Execute(
                "CREATE TABLE IF NOT EXISTS ChargingSession"
                + "("
                + "Id BIGINT PRIMARY KEY DEFAULT NEXTVAL('Seq_ChargingSession_Id'),"
                + "CustomerId BIGINT NOT NULL,"
                + "CarId BIGINT,"
                + "StationId BIGINT NOT NULL,"
                + "PortId BIGINT NOT NULL,"
                + "SessionStartDate BIGINT NOT NULL,"
                + "SessionStopDate BIGINT,"
                + "ChargeStartDate BIGINT,"
                + "ChargeStopDate BIGINT,"
                + "LastValueDate BIGINT,"
                + "EnergyMeterStart DOUBLE PRECISION,"
                + "EnergyMeterStop DOUBLE PRECISION,"
                + "ChargeTime BIGINT,"
                + "EnergyConsumed DOUBLE PRECISION,"
                + "PaymentAmount DOUBLE PRECISION NOT NULL,"
                + "PriceType VARCHAR(16),"
                + "Price DOUBLE PRECISION,"
                + "Currency VARCHAR(3) NOT NULL,"
                + "Status VARCHAR(32) NOT NULL,"
                + "DriverStatus VARCHAR(32),"
                + "StopReason VARCHAR(32),"
                + "PaymentId BIGINT,"
                + "MaxChargeTime BIGINT,"
                + "MaxEnergyConsumed DOUBLE PRECISION,"
                + "Voltage DOUBLE PRECISION,"
                + "Current DOUBLE PRECISION,"
                + "Power DOUBLE PRECISION"
                + ")");

            this.Execute("CREATE INDEX IF NOT EXISTS Idx_ChargingSession_CustomerId ON ChargingSession(CustomerId,SessionStartDate)");
            this.Execute("CREATE INDEX IF NOT EXISTS Idx_ChargingSession_StationId ON ChargingSession(StationId,SessionStartDate)");
            this.Execute("CREATE INDEX IF NOT EXISTS Idx_ChargingSession_PortId ON ChargingSession(PortId,SessionStartDate)");
            this.Execute("CREATE INDEX IF NOT EXISTS Idx_ChargingSession_SessionStartDate ON ChargingSession(SessionStartDate)");
            this.Execute("CREATE INDEX IF NOT EXISTS Idx_ChargingSession_Status ON ChargingSession(Status,SessionStartDate)");

            // Fix: Add MaxChargeTime if it does not exist
            if (!this.CheckTableColumnName("ChargingSession", "MaxChargeTime"))
            {
                this.Execute("ALTER TABLE ChargingSession ADD COLUMN MaxChargeTime BIGINT");
            }

            // Fix: Add MaxEnergyConsumed if it does not exist
            if (!this.CheckTableColumnName("ChargingSession", "MaxEnergyConsumed"))
            {
                this.Execute("ALTER TABLE ChargingSession ADD COLUMN MaxEnergyConsumed DOUBLE PRECISION");
            }

            // Fix: Add Voltage if it does not exist
            if (!this.CheckTableColumnName("ChargingSession", "Voltage"))
            {
                this.Execute("ALTER TABLE ChargingSession ADD COLUMN Voltage DOUBLE PRECISION");
            }

            // Fix: Add Current if it does not exist
            if (!this.CheckTableColumnName("ChargingSession", "Current"))
            {
                this.Execute("ALTER TABLE ChargingSession ADD COLUMN Current DOUBLE PRECISION");
            }

            // Fix: Add Power if it does not exist
            if (!this.CheckTableColumnName("ChargingSession", "Power"))
            {
                this.Execute("ALTER TABLE ChargingSession ADD COLUMN Power DOUBLE PRECISION");
            }

            // Fix: Add DriverStatus if it does not exist
            if (!this.CheckTableColumnName("ChargingSession", "DriverStatus"))
            {
                this.Execute("ALTER TABLE ChargingSession ADD COLUMN DriverStatus VARCHAR(32)");
            }

            // Fix: Status size
            this.Execute("ALTER TABLE ChargingSession ALTER COLUMN Status TYPE VARCHAR(32);");

            // Fix: Add EnergyMeterStart if it does not exist
            if (!this.CheckTableColumnName("ChargingSession", "EnergyMeterStart"))
            {
                this.Execute("ALTER TABLE ChargingSession ADD COLUMN EnergyMeterStart DOUBLE PRECISION");
            }

            // Fix: Add EnergyMeterStop if it does not exist
            if (!this.CheckTableColumnName("ChargingSession", "EnergyMeterStop"))
            {
                this.Execute("ALTER TABLE ChargingSession ADD COLUMN EnergyMeterStop DOUBLE PRECISION");
            }

            // Fix: Add StopReason if it does not exist
            if (!this.CheckTableColumnName("ChargingSession", "StopReason"))
            {
                this.Execute("ALTER TABLE ChargingSession ADD COLUMN StopReason VARCHAR(32)");
            }

            // Fix: Add LastValueDate if it does not exist
            if (!this.CheckTableColumnName("ChargingSession", "LastValueDate"))
            {
                this.Execute("ALTER TABLE ChargingSession ADD COLUMN LastValueDate BIGINT");
            }
        }

        private void CreateTablePayment()
        {
            this.Execute("CREATE SEQUENCE IF NOT EXISTS Seq_Payment_Id MINVALUE " + DateTime.Now.ToEpochtime().ToString());

            this.Execute(
                "CREATE TABLE IF NOT EXISTS Payment"
                + "("
                + "Id BIGINT PRIMARY KEY DEFAULT NEXTVAL('Seq_Payment_Id'),"
                + "CustomerId BIGINT NOT NULL,"
                + "AmountHold DOUBLE PRECISION,"
                + "AmountPaid DOUBLE PRECISION NOT NULL,"
                + "Commission DOUBLE PRECISION,"
                + "AmountReceived DOUBLE PRECISION NOT NULL,"
                + "Currency VARCHAR(3) NOT NULL,"
                + "CreationDate BIGINT NOT NULL,"
                + "HoldDate BIGINT,"
                + "PaidDate BIGINT,"
                + "PaymentService VARCHAR(16) NOT NULL,"
                + "ExternalPaymentId BIGINT,"
                + "Status VARCHAR(16) NOT NULL,"
                + "ErrorDescription VARCHAR(32),"
                + "IP VARCHAR(15)"
                + ")");

            this.Execute("CREATE INDEX IF NOT EXISTS Idx_Payment_CustomerId ON Payment(CustomerId,CreationDate)");
            this.Execute("CREATE INDEX IF NOT EXISTS Idx_Payment_CreationDate ON Payment(CreationDate)");

            // Fix: Add MaxChargeTime if it does not exist
            if (!this.CheckTableColumnName("Payment", "ErrorDescription"))
            {
                this.Execute("ALTER TABLE Payment ADD COLUMN ErrorDescription VARCHAR(32)");
            }
        }

        private void AddParam(NpgsqlCommand cmd, string name, long? value)
        {
            if (value == null)
            {
                cmd.Parameters.AddWithValue(name, DBNull.Value);
            }
            else
            {
                cmd.Parameters.AddWithValue(name, value);
            }
        }

        private void AddParam(NpgsqlCommand cmd, string name, long value)
        {
            cmd.Parameters.AddWithValue(name, value);
        }

        private void AddParam(NpgsqlCommand cmd, string name, double value)
        {
            cmd.Parameters.AddWithValue(name, value);
        }

        private void AddParam(NpgsqlCommand cmd, string name, bool? value)
        {
            if (value == null)
            {
                cmd.Parameters.AddWithValue(name, DBNull.Value);
            }
            else
            {
                cmd.Parameters.AddWithValue(name, value);
            }
        }

        private void AddParam(NpgsqlCommand cmd, string name, bool value)
        {
            cmd.Parameters.AddWithValue(name, value);
        }

        private void AddParam(NpgsqlCommand cmd, string name, string value, int size)
        {
            if (string.IsNullOrEmpty(value))
            {
                cmd.Parameters.AddWithValue(name, DBNull.Value);
            }
            else if (value.Length <= size)
            {
                cmd.Parameters.AddWithValue(name, value);
            }
            else
            {
                cmd.Parameters.AddWithValue(name, value.Substring(0, size));
            }
        }

        private void AddSearchParam(NpgsqlCommand cmd, string name, string value, bool lowerCase = false)
        {
            if (string.IsNullOrEmpty(value))
            {
                cmd.Parameters.AddWithValue(name, "%");
            }
            else if (lowerCase)
            {
                cmd.Parameters.AddWithValue(name, "%" + value.ToLower() + "%");
            }
            else
            {
                cmd.Parameters.AddWithValue(name, "%" + value + "%");
            }
        }

        private string GetParamString(NpgsqlDataReader reader, int index)
        {
            if (reader.IsDBNull(index))
            {
                return null;
            }

            return reader.GetString(index);
        }

        private bool? GetParamBoolean(NpgsqlDataReader reader, int index)
        {
            if (reader.IsDBNull(index))
            {
                return null;
            }

            return reader.GetBoolean(index);
        }

        private long? GetParamLong(NpgsqlDataReader reader, int index)
        {
            if (reader.IsDBNull(index))
            {
                return null;
            }

            return reader.GetInt64(index);
        }

        private double? GetParamDouble(NpgsqlDataReader reader, int index)
        {
            if (reader.IsDBNull(index))
            {
                return null;
            }

            return reader.GetDouble(index);
        }

        // Common execute query with return value
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "No user input")]
        private object Execute(string sql, Func<NpgsqlCommand, object> command, Func<Exception, bool> exception = null)
        {
            for (int attempt = 1; attempt <= this.maxAttempts; attempt++)
            {
                try
                {
                    using (var connection = new NpgsqlConnection(this.connectionString))
                    {
                        connection.Open();

                        using (var cmd = new NpgsqlCommand(sql, connection))
                        {
                            return command(cmd);
                        }
                    }
                }
                catch (NpgsqlException e)
                {
                    if (e.IsTransient && attempt < this.maxAttempts)
                    {
                        Console.WriteLine(string.Format("DB connection problem: {0}. Try to reconnect...", e.Message));
                        Thread.Sleep(this.delay);
                        continue;
                    }

                    if (exception != null && !exception(e))
                    {
                        // if we do not need to generate exception
                        return null;
                    }

                    Console.WriteLine(string.Format("Exception in DB '{0}': {1}", sql, e.Message));
                    throw new Exception(string.Format("Ошибка в базе данных: {0}", e.Message));
                }
                catch (Exception e)
                {
                    if (e.Source != "Npgsql")
                    {
                        throw;
                    }

                    if (attempt < this.maxAttempts)
                    {
                        Console.WriteLine(string.Format("DB connection problem: {0}. Try to reconnect...", e.Message));
                        Thread.Sleep(this.delay);
                        continue;
                    }

                    if (exception != null && !exception(e))
                    {
                        // if we do not need to generate exception
                        return null;
                    }

                    Console.WriteLine(string.Format("Exception in DB '{0}': {1}", sql, e.Message));
                    throw new Exception(string.Format("Ошибка в базе данных: {0}", e.Message));
                }
            }

            // Will be never reached
            return null;
        }

        // Common execute query with no return value
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "No user input")]
        private void Execute(string sql, Action<NpgsqlCommand> command = null, Func<Exception, bool> exception = null)
        {
            for (int attempt = 1; attempt <= this.maxAttempts; attempt++)
            {
                try
                {
                    using (var connection = new NpgsqlConnection(this.connectionString))
                    {
                        connection.Open();

                        using (var cmd = new NpgsqlCommand(sql, connection))
                        {
                            if (command != null)
                            {
                                command(cmd);
                            }
                            else
                            {
                                cmd.ExecuteNonQuery();
                            }

                            return;
                        }
                    }
                }
                catch (NpgsqlException e)
                {
                    if (e.IsTransient && attempt < this.maxAttempts)
                    {
                        Console.WriteLine(string.Format("DB connection problem: {0}. Try to reconnect...", e.Message));
                        Thread.Sleep(this.delay);
                        continue;
                    }

                    if (exception != null && !exception(e))
                    {
                        // if we do not need to generate exception
                        return;
                    }

                    Console.WriteLine(string.Format("Exception in DB '{0}': {1}", sql, e.Message));
                    throw new Exception(string.Format("Ошибка в базе данных: {0}", e.Message));
                }
                catch (Exception e)
                {
                    if (e.Source != "Npgsql")
                    {
                        throw;
                    }

                    if (attempt < this.maxAttempts)
                    {
                        Console.WriteLine(string.Format("DB connection problem: {0}. Try to reconnect...", e.Message));
                        Thread.Sleep(this.delay);
                        continue;
                    }

                    if (exception != null && !exception(e))
                    {
                        // if we do not need to generate exception
                        return;
                    }

                    Console.WriteLine(string.Format("Exception in DB '{0}': {1}", sql, e.Message));
                    throw new Exception(string.Format("Ошибка в базе данных: {0}", e.Message));
                }
            }
        }

        // Common execute update query with no return value, Audit records are created
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "No user input")]
        private void ExecuteUpdate(object data, long who, Func<Exception, bool> exception = null)
        {
            string tableName;
            var tableAttr = (TableAttr[])data.GetType().GetCustomAttributes(typeof(TableAttr));
            PropertyInfo[] props = data.GetType().GetProperties();
            StringBuilder sql = new StringBuilder();

            if (tableAttr != null && tableAttr.Length > 0)
            {
                tableName = tableAttr[0].Name;

                if (tableName.Length == 0)
                {
                    throw new Exception(string.Format("Missing Name in TableAtrr of the class/struct '{0}'", data.GetType().Name));
                }
            }
            else
            {
                throw new Exception(string.Format("Missing TableAtrr in the class/struct '{0}'", data.GetType().Name));
            }

            for (int attempt = 1; attempt <= this.maxAttempts; attempt++)
            {
                try
                {
                    using (var connection = new NpgsqlConnection(this.connectionString))
                    {
                        connection.Open();

                        using (var cmd = new NpgsqlCommand())
                        {
                            int countColumn = 0;
                            int countWhere = 0;
                            object id = null;
                            StringBuilder where = new StringBuilder();

                            sql.Clear();
                            sql.Append("UPDATE ");
                            sql.Append(tableName);
                            sql.Append(" SET ");

                            foreach (var prop in props)
                            {
                                object columnValue = prop.GetValue(data);
                                if (columnValue != null)
                                {
                                    var columnAttr = (ColumnAttr[])prop.GetCustomAttributes(typeof(ColumnAttr));
                                    if (columnAttr != null && columnAttr.Length > 0)
                                    {
                                        if (columnAttr[0].IsId || columnAttr[0].IsUpdateKey)
                                        {
                                            if (columnAttr[0].IsId)
                                            {
                                                if (id != null)
                                                {
                                                    throw new Exception(string.Format("Only one IsId should be in ColumnAttr of the class/struct '{0}'", data.GetType().Name));
                                                }

                                                id = columnValue;
                                            }

                                            if (columnAttr[0].IsUpdateKey)
                                            {
                                                if (countWhere++ > 0)
                                                {
                                                    where.Append(" AND ");
                                                }

                                                string columnName = string.IsNullOrEmpty(columnAttr[0].Name) ? prop.Name : columnAttr[0].Name;
                                                where.Append(columnName);
                                                where.Append("=@");
                                                where.Append(columnName);
                                                cmd.Parameters.AddWithValue(columnName, columnValue);
                                            }
                                        }
                                        else if (columnAttr[0].IsUpdatable)
                                        {
                                            if (countColumn++ > 0)
                                            {
                                                sql.Append(",");
                                            }

                                            string columnName = string.IsNullOrEmpty(columnAttr[0].Name) ? prop.Name : columnAttr[0].Name;
                                            sql.Append(columnName);
                                            sql.Append("=@");
                                            sql.Append(columnName);

                                            if (columnAttr[0].MaxLength > 0 && columnValue != null && columnValue.ToString().Length > columnAttr[0].MaxLength)
                                            {
                                                cmd.Parameters.AddWithValue(columnName, columnValue.ToString().Substring(0, columnAttr[0].MaxLength));
                                            }
                                            else
                                            {
                                                cmd.Parameters.AddWithValue(columnName, columnValue);
                                            }
                                        }
                                    }
                                }
                            }

                            if (id == null)
                            {
                                throw new Exception(string.Format("No IsId found in ColumnAttr of the class/struct '{0}'", data.GetType().Name));
                            }

                            if (countWhere == 0)
                            {
                                throw new Exception(string.Format("No IsUpdateKey found in ColumnAttr of the class/struct '{0}'", data.GetType().Name));
                            }

                            sql.Append(" WHERE ");
                            sql.Append(where);

                            if (countColumn > 0)
                            {
                                cmd.CommandText = sql.ToString();
                                cmd.Connection = connection;
                                cmd.ExecuteNonQuery();

                                // After success add to audit queue
                                long now = DateTime.Now.ToEpochtime();

                                this.auditMutex.WaitOne();
                                try
                                {
                                    foreach (var prop in props)
                                    {
                                        object columnValue = prop.GetValue(data);
                                        if (columnValue != null)
                                        {
                                            var columnAttr = (ColumnAttr[])prop.GetCustomAttributes(typeof(ColumnAttr));
                                            if (columnAttr != null && columnAttr.Length > 0 && columnAttr[0].IsAuditable)
                                            {
                                                string columnName = string.IsNullOrEmpty(columnAttr[0].Name) ? prop.Name : columnAttr[0].Name;
                                                this.auditQueue.Enqueue(new AuditData(tableName, (long)id, columnName, now, prop.GetValue(data).ToString(), who));
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                }
                                finally
                                {
                                    this.auditMutex.ReleaseMutex();
                                }
                            }

                            return;
                        }
                    }
                }
                catch (NpgsqlException e)
                {
                    if (e.IsTransient && attempt < this.maxAttempts)
                    {
                        Console.WriteLine(string.Format("DB connection problem: {0}. Try to reconnect...", e.Message));
                        Thread.Sleep(this.delay);
                        continue;
                    }

                    if (exception != null && !exception(e))
                    {
                        // if we do not need to generate exception
                        return;
                    }

                    Console.WriteLine(string.Format("Exception in DB '{0}': {1}", sql.ToString(), e.Message));
                    throw new Exception(string.Format("Ошибка в базе данных: {0}", e.Message));
                }
                catch (Exception e)
                {
                    if (e.Source != "Npgsql")
                    {
                        throw;
                    }

                    if (attempt < this.maxAttempts)
                    {
                        Console.WriteLine(string.Format("DB connection problem: {0}. Try to reconnect...", e.Message));
                        Thread.Sleep(this.delay);
                        continue;
                    }

                    if (exception != null && !exception(e))
                    {
                        // if we do not need to generate exception
                        return;
                    }

                    Console.WriteLine(string.Format("Exception in DB '{0}': {1}", sql, e.Message));
                    throw new Exception(string.Format("Ошибка в базе данных: {0}", e.Message));
                }
            }
        }

        // Common execute update query with no return value, Audit records are created
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "No user input")]
        private void ExecuteDelete(object data, long who, Func<Exception, bool> exception = null)
        {
            string tableName;
            var tableAttr = (TableAttr[])data.GetType().GetCustomAttributes(typeof(TableAttr));
            PropertyInfo[] props = data.GetType().GetProperties();
            StringBuilder sql = new StringBuilder();

            if (tableAttr != null && tableAttr.Length > 0)
            {
                tableName = tableAttr[0].Name;

                if (tableName.Length == 0)
                {
                    throw new Exception(string.Format("Missing Name in TableAtrr of the class/struct '{0}'", data.GetType().Name));
                }
            }
            else
            {
                throw new Exception(string.Format("Missing TableAtrr in the class/struct '{0}'", data.GetType().Name));
            }

            for (int attempt = 1; attempt <= this.maxAttempts; attempt++)
            {
                try
                {
                    using (var connection = new NpgsqlConnection(this.connectionString))
                    {
                        connection.Open();

                        using (var cmd = new NpgsqlCommand())
                        {
                            int countWhere = 0;
                            object id = null;

                            sql.Clear();
                            sql.Append("UPDATE ");
                            sql.Append(tableName);
                            sql.Append(" SET Deleted=true WHERE ");

                            foreach (var prop in props)
                            {
                                object columnValue = prop.GetValue(data);
                                if (columnValue != null)
                                {
                                    var columnAttr = (ColumnAttr[])prop.GetCustomAttributes(typeof(ColumnAttr));
                                    if (columnAttr != null && columnAttr.Length > 0)
                                    {
                                        if (columnAttr[0].IsId)
                                        {
                                            if (id != null)
                                            {
                                                throw new Exception(string.Format("Only one IsId should be in ColumnAttr of the class/struct '{0}'", data.GetType().Name));
                                            }

                                            id = columnValue;
                                        }

                                        if (columnAttr[0].IsUpdateKey)
                                        {
                                            if (countWhere++ > 0)
                                            {
                                                sql.Append(" AND ");
                                            }

                                            string columnName = string.IsNullOrEmpty(columnAttr[0].Name) ? prop.Name : columnAttr[0].Name;
                                            sql.Append(columnName);
                                            sql.Append("=@");
                                            sql.Append(columnName);
                                            cmd.Parameters.AddWithValue(columnName, columnValue);
                                        }
                                    }
                                }
                            }

                            if (id == null)
                            {
                                throw new Exception(string.Format("No IsId found in ColumnAttr of the class/struct '{0}'", data.GetType().Name));
                            }

                            if (countWhere == 0)
                            {
                                throw new Exception(string.Format("No IsUpdateKey found in ColumnAttr of the class/struct '{0}'", data.GetType().Name));
                            }

                            cmd.CommandText = sql.ToString();
                            cmd.Connection = connection;
                            cmd.ExecuteNonQuery();

                            // After success add to audit queue
                            this.auditMutex.WaitOne();
                            try
                            {
                                this.auditQueue.Enqueue(new AuditData(tableName, (long)id, "Deleted", DateTime.Now.ToEpochtime(), "true", who));
                            }
                            catch
                            {
                            }
                            finally
                            {
                                this.auditMutex.ReleaseMutex();
                            }

                            return;
                        }
                    }
                }
                catch (NpgsqlException e)
                {
                    if (e.IsTransient && attempt < this.maxAttempts)
                    {
                        Console.WriteLine(string.Format("DB connection problem: {0}. Try to reconnect...", e.Message));
                        Thread.Sleep(this.delay);
                        continue;
                    }

                    if (exception != null && !exception(e))
                    {
                        // if we do not need to generate exception
                        return;
                    }

                    Console.WriteLine(string.Format("Exception in DB '{0}': {1}", sql.ToString(), e.Message));
                    throw new Exception(string.Format("Ошибка в базе данных: {0}", e.Message));
                }
                catch (Exception e)
                {
                    if (e.Source != "Npgsql")
                    {
                        throw;
                    }

                    if (attempt < this.maxAttempts)
                    {
                        Console.WriteLine(string.Format("DB connection problem: {0}. Try to reconnect...", e.Message));
                        Thread.Sleep(this.delay);
                        continue;
                    }

                    if (exception != null && !exception(e))
                    {
                        // if we do not need to generate exception
                        return;
                    }

                    Console.WriteLine(string.Format("Exception in DB '{0}': {1}", sql, e.Message));
                    throw new Exception(string.Format("Ошибка в базе данных: {0}", e.Message));
                }
            }
        }

        // Common execute purge query with no return value, Audit records are created
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "No user input")]
        private void ExecutePurge(object data, long who, Func<Exception, bool> exception = null)
        {
            string tableName;
            var tableAttr = (TableAttr[])data.GetType().GetCustomAttributes(typeof(TableAttr));
            PropertyInfo[] props = data.GetType().GetProperties();
            StringBuilder sql = new StringBuilder();

            if (tableAttr != null && tableAttr.Length > 0)
            {
                tableName = tableAttr[0].Name;

                if (tableName.Length == 0)
                {
                    throw new Exception(string.Format("Missing Name in TableAtrr of the class/struct '{0}'", data.GetType().Name));
                }
            }
            else
            {
                throw new Exception(string.Format("Missing TableAtrr in the class/struct '{0}'", data.GetType().Name));
            }

            for (int attempt = 1; attempt <= this.maxAttempts; attempt++)
            {
                try
                {
                    using (var connection = new NpgsqlConnection(this.connectionString))
                    {
                        connection.Open();

                        using (var cmd = new NpgsqlCommand())
                        {
                            int count = 0;
                            object id = null;

                            sql.Clear();
                            sql.Append("DELETE FROM ");
                            sql.Append(tableName);
                            sql.Append(" WHERE ");

                            foreach (var prop in props)
                            {
                                object columnValue = prop.GetValue(data);
                                if (columnValue != null)
                                {
                                    var columnAttr = (ColumnAttr[])prop.GetCustomAttributes(typeof(ColumnAttr));
                                    if (columnAttr != null && columnAttr.Length > 0)
                                    {
                                        if (columnAttr[0].IsId)
                                        {
                                            if (id != null)
                                            {
                                                throw new Exception(string.Format("Only one IsId should be in ColumnAttr of the class/struct '{0}'", data.GetType().Name));
                                            }

                                            id = columnValue;
                                        }

                                        if (columnAttr[0].IsUpdateKey)
                                        {
                                            if (count++ > 0)
                                            {
                                                sql.Append(" AND ");
                                            }

                                            string columnName = string.IsNullOrEmpty(columnAttr[0].Name) ? prop.Name : columnAttr[0].Name;
                                            sql.Append(columnName);
                                            sql.Append("=@");
                                            sql.Append(columnName);
                                            cmd.Parameters.AddWithValue(columnName, columnValue);
                                        }
                                    }
                                }
                            }

                            if (id == null)
                            {
                                throw new Exception(string.Format("No IsId found in ColumnAttr of the class/struct '{0}'", data.GetType().Name));
                            }

                            if (count == 0)
                            {
                                throw new Exception(string.Format("No IsUpdateKey found in ColumnAttr of the class/struct '{0}'", data.GetType().Name));
                            }

                            cmd.CommandText = sql.ToString();
                            cmd.Connection = connection;
                            cmd.ExecuteNonQuery();

                            // After success add to audit queue
                            this.auditMutex.WaitOne();
                            try
                            {
                                this.auditQueue.Enqueue(new AuditData(tableName, (long)id, "Purge", DateTime.Now.ToEpochtime(), null, who));
                            }
                            catch
                            {
                            }
                            finally
                            {
                                this.auditMutex.ReleaseMutex();
                            }

                            return;
                        }
                    }
                }
                catch (NpgsqlException e)
                {
                    if (e.IsTransient && attempt < this.maxAttempts)
                    {
                        Console.WriteLine(string.Format("DB connection problem: {0}. Try to reconnect...", e.Message));
                        Thread.Sleep(this.delay);
                        continue;
                    }

                    if (exception != null && !exception(e))
                    {
                        // if we do not need to generate exception
                        return;
                    }

                    Console.WriteLine(string.Format("Exception in DB '{0}': {1}", sql.ToString(), e.Message));
                    throw new Exception(string.Format("Ошибка в базе данных: {0}", e.Message));
                }
                catch (Exception e)
                {
                    if (e.Source != "Npgsql")
                    {
                        throw;
                    }

                    if (attempt < this.maxAttempts)
                    {
                        Console.WriteLine(string.Format("DB connection problem: {0}. Try to reconnect...", e.Message));
                        Thread.Sleep(this.delay);
                        continue;
                    }

                    if (exception != null && !exception(e))
                    {
                        // if we do not need to generate exception
                        return;
                    }

                    Console.WriteLine(string.Format("Exception in DB '{0}': {1}", sql, e.Message));
                    throw new Exception(string.Format("Ошибка в базе данных: {0}", e.Message));
                }
            }
        }

        // Common execute update query with no return value, Audit records are created
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "No user input")]
        private long ExecuteCreate(object data, long who, Func<Exception, bool> exception = null)
        {
            string tableName;
            var tableAttr = (TableAttr[])data.GetType().GetCustomAttributes(typeof(TableAttr));
            PropertyInfo[] props = data.GetType().GetProperties();
            StringBuilder sql = new StringBuilder();
            long ret = 0;

            if (tableAttr != null && tableAttr.Length > 0)
            {
                tableName = tableAttr[0].Name;

                if (tableName.Length == 0)
                {
                    throw new Exception(string.Format("Missing Name in TableAtrr of the class/struct '{0}'", data.GetType().Name));
                }
            }
            else
            {
                throw new Exception(string.Format("Missing TableAtrr in the class/struct '{0}'", data.GetType().Name));
            }

            for (int attempt = 1; attempt <= this.maxAttempts; attempt++)
            {
                try
                {
                    using (var connection = new NpgsqlConnection(this.connectionString))
                    {
                        connection.Open();

                        using (var cmd = new NpgsqlCommand())
                        {
                            int countColumn = 0;
                            string idName = null;
                            StringBuilder values = new StringBuilder();

                            sql.Clear();
                            sql.Append("INSERT INTO ");
                            sql.Append(tableName);
                            sql.Append("(");

                            foreach (var prop in props)
                            {
                                object columnValue = prop.GetValue(data);
                                if (columnValue != null)
                                {
                                    var columnAttr = (ColumnAttr[])prop.GetCustomAttributes(typeof(ColumnAttr));
                                    if (columnAttr != null && columnAttr.Length > 0)
                                    {
                                        string columnName = string.IsNullOrEmpty(columnAttr[0].Name) ? prop.Name : columnAttr[0].Name;

                                        if (columnAttr[0].IsId)
                                        {
                                            if (idName != null)
                                            {
                                                throw new Exception(string.Format("Only one IsId should be in ColumnAttr of the class/struct '{0}'", data.GetType().Name));
                                            }

                                            idName = columnName;
                                        }
                                        else
                                        {
                                            if (countColumn++ > 0)
                                            {
                                                sql.Append(",");
                                                values.Append(",");
                                            }

                                            sql.Append(columnName);
                                            values.Append("@");
                                            values.Append(columnName);

                                            if (columnAttr[0].MaxLength > 0 && columnValue != null && columnValue.ToString().Length > columnAttr[0].MaxLength)
                                            {
                                                cmd.Parameters.AddWithValue(columnName, columnValue.ToString().Substring(0, columnAttr[0].MaxLength));
                                            }
                                            else
                                            {
                                                cmd.Parameters.AddWithValue(columnName, columnValue);
                                            }
                                        }
                                    }
                                }
                            }

                            if (idName == null)
                            {
                                throw new Exception(string.Format("No IsId found in ColumnAttr of the class/struct '{0}'", data.GetType().Name));
                            }

                            sql.Append(") VALUES(");
                            sql.Append(values);
                            sql.Append(") RETURNING ");
                            sql.Append(idName);

                            cmd.CommandText = sql.ToString();
                            cmd.Connection = connection;

                            using (var reader = cmd.ExecuteReader())
                            {
                                reader.Read();
                                ret = reader.GetInt64(0);
                            }

                            // After success add to audit queue
                            long now = DateTime.Now.ToEpochtime();

                            this.auditMutex.WaitOne();
                            try
                            {
                                foreach (var prop in props)
                                {
                                    object columnValue = prop.GetValue(data);
                                    if (columnValue != null)
                                    {
                                        var columnAttr = (ColumnAttr[])prop.GetCustomAttributes(typeof(ColumnAttr));
                                        if (columnAttr != null && columnAttr.Length > 0 && columnAttr[0].IsAuditable)
                                        {
                                            string columnName = string.IsNullOrEmpty(columnAttr[0].Name) ? prop.Name : columnAttr[0].Name;
                                            this.auditQueue.Enqueue(new AuditData(tableName, ret, columnName, now, prop.GetValue(data).ToString(), who));
                                        }
                                    }
                                }
                            }
                            catch
                            {
                            }
                            finally
                            {
                                this.auditMutex.ReleaseMutex();
                            }

                            return ret;
                        }
                    }
                }
                catch (NpgsqlException e)
                {
                    if (e.IsTransient && attempt < this.maxAttempts)
                    {
                        Console.WriteLine(string.Format("DB connection problem: {0}. Try to reconnect...", e.Message));
                        Thread.Sleep(this.delay);
                        continue;
                    }

                    if (exception != null && !exception(e))
                    {
                        // if we do not need to generate exception
                        return ret;
                    }

                    Console.WriteLine(string.Format("Exception in DB '{0}': {1}", sql.ToString(), e.Message));
                    throw new Exception(string.Format("Ошибка в базе данных: {0}", e.Message));
                }
                catch (Exception e)
                {
                    if (e.Source != "Npgsql")
                    {
                        throw;
                    }

                    if (attempt < this.maxAttempts)
                    {
                        Console.WriteLine(string.Format("DB connection problem: {0}. Try to reconnect...", e.Message));
                        Thread.Sleep(this.delay);
                        continue;
                    }

                    if (exception != null && !exception(e))
                    {
                        // if we do not need to generate exception
                        return ret;
                    }

                    Console.WriteLine(string.Format("Exception in DB '{0}': {1}", sql, e.Message));
                    throw new Exception(string.Format("Ошибка в базе данных: {0}", e.Message));
                }
            }

            return ret;
        }

        // Common execute for many queries using transactions with no return value
        /*        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "No user input")]
                private void Execute(params Tuple<string, Action<NpgsqlCommand>>[] queries)
                {
                    for (int attempt = 1; attempt <= this.maxAttempts; attempt++)
                    {
                        try
                        {
                            using (var connection = new NpgsqlConnection(this.connectionString))
                            {
                                connection.Open();

                                using (var transaction = connection.BeginTransaction())
                                {
                                    foreach (var sql in queries)
                                    {
                                        using (var cmd = new NpgsqlCommand(sql.Item1, connection, transaction))
                                        {
                                            sql.Item2(cmd);
                                        }
                                    }

                                    transaction.Commit();
                                    return;
                                }
                            }
                        }
                        catch (NpgsqlException e)
                        {
                            if (e.IsTransient && attempt < this.maxAttempts)
                            {
                                Console.WriteLine(string.Format("DB connection problem: {0}. Try to reconnect...", e.Message));
                                Thread.Sleep(this.delay);
                                continue;
                            }

                            Console.WriteLine(string.Format("Exception in DB '{0}': {1}", sql, e.Message));
                            throw new Exception(string.Format("Ошибка в базе данных: {0}", e.Message));
                        }
                catch (Exception e)
                {
                    if (e.Source != "Npgsql") throw;

                    if (attempt < this.maxAttempts)
                    {
                        Console.WriteLine(string.Format("DB connection problem: {0}. Try to reconnect...", e.Message));
                        Thread.Sleep(this.delay);
                        continue;
                    }

                    Console.WriteLine(string.Format("Exception in DB '{0}': {1}", sql, e.Message));
                    throw new Exception(string.Format("Ошибка в базе данных: {0}", e.Message));
                }

                    }
                }
                */

        // Common execute for 2 queries using transactions with no return value
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "No user input")]
        private void Execute(string sql, Action<NpgsqlCommand> command, string sql2, Action<NpgsqlCommand> command2, Func<Exception, bool> exception = null)
        {
            for (int attempt = 1; attempt <= this.maxAttempts; attempt++)
            {
                try
                {
                    using (var connection = new NpgsqlConnection(this.connectionString))
                    {
                        connection.Open();

                        using (var transaction = connection.BeginTransaction())
                        {
                            using (var cmd = new NpgsqlCommand(sql, connection, transaction))
                            {
                                command(cmd);
                            }

                            using (var cmd = new NpgsqlCommand(sql2, connection, transaction))
                            {
                                command2(cmd);
                            }

                            transaction.Commit();
                            return;
                        }
                    }
                }
                catch (NpgsqlException e)
                {
                    if (e.IsTransient && attempt < this.maxAttempts)
                    {
                        Console.WriteLine(string.Format("DB connection problem: {0}. Try to reconnect...", e.Message));
                        Thread.Sleep(this.delay);
                        continue;
                    }

                    if (exception != null && !exception(e))
                    {
                        // if we do not need to generate exception
                        return;
                    }

                    Console.WriteLine(string.Format("Exception in DB '{0}': {1}", sql, e.Message));
                    throw new Exception(string.Format("Ошибка в базе данных: {0}", e.Message));
                }
                catch (Exception e)
                {
                    if (e.Source != "Npgsql")
                    {
                        throw;
                    }

                    if (attempt < this.maxAttempts)
                    {
                        Console.WriteLine(string.Format("DB connection problem: {0}. Try to reconnect...", e.Message));
                        Thread.Sleep(this.delay);
                        continue;
                    }

                    if (exception != null && !exception(e))
                    {
                        // if we do not need to generate exception
                        return;
                    }

                    Console.WriteLine(string.Format("Exception in DB '{0}': {1}", sql, e.Message));
                    throw new Exception(string.Format("Ошибка в базе данных: {0}", e.Message));
                }
            }
        }

        private bool CheckTableColumnName(string tableName, string columnName)
        {
            return (bool)this.Execute(
                "SELECT data_type FROM information_schema.columns WHERE table_name=@p1 AND column_name=@p2",
                cmd =>
                {
                    this.AddParam(cmd, "p1", tableName.ToLower(), 32);
                    this.AddParam(cmd, "p2", columnName.ToLower(), 32);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return true;
                        }
                    }

                    return false;
                });
        }

        #endregion 
    }
}
