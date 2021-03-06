﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using BackendCommon;
using Npgsql;

namespace BackendDatabase
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
            this.Execute(
                "UPDATE Session SET Deleted='T' WHERE CustomerId=@p0 AND SessionToken IN (SELECT SessionToken FROM Session"
                + " WHERE CustomerId=@p1 AND Deleted='F' ORDER BY ExpirationDate DESC OFFSET @p1)",
                cmd =>
                {
                    this.AddParam(cmd, "p0", session.CustomerId);
                    this.AddParam(cmd, "p1", maxSessionCount > 0 ? maxSessionCount - 1 : 0);
                    cmd.ExecuteNonQuery();
                },
                "INSERT INTO Session(SessionToken,CustomerId,AccessRights,ExpirationDate,Deleted) VALUES(@p1,@p2,@p3,@p4,'F')",
                cmd =>
                {
                    this.AddParam(cmd, "p1", session.SessionToken, 32);
                    this.AddParam(cmd, "p2", session.CustomerId);
                    this.AddParam(cmd, "p3", session.AccessRights, 16);
                    this.AddParam(cmd, "p4", session.ExpirationDateEpochtime.HasValue ? session.ExpirationDateEpochtime : DateTime.Now.AddMonths(1).ToEpochtime());
                    cmd.ExecuteNonQuery();
                });
        }

        // Get login session (only undeleted)
        public SessionData GetSesson(string sessionToken)
        {
            return (SessionData)this.Execute(
                "SELECT Id,CustomerId,AccessRights,ExpirationDate"
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
                            session.ExpirationDateEpochtime = this.GetParamLong(reader, 3);
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
                "UPDATE Session SET Deleted='T' WHERE SessionToken=@p AND Deleted='F'",
                cmd =>
                {
                    this.AddParam(cmd, "p", sessionToken, 32);
                    cmd.ExecuteNonQuery();
                },
                e => false);       // Do not return exception, be just silent
        }

        // Delete all sessions of this customer
        public void DeleteSession(long customerId)
        {
            this.Execute(
                "UPDATE Session SET Deleted='T', WHERE CustomerId=@p AND Deleted='F'",
                cmd =>
                {
                    this.AddParam(cmd, "p", customerId);
                    cmd.ExecuteNonQuery();
                },
                e => false);       // Do not return exception, be just silent
        }

        #endregion

        #region Customers

        public long CreateCustomer(CustomerData customer)
        {
            return this.ExecuteCreate(
                customer,
                e =>
                {
                    if (e.Message.Contains("idx_customer_login"))
                    {
                        throw new Exception(string.Format("Пользователь с логином '{0}' уже существует", customer.Login));
                    }

                    return true;
                });
        }

        public CustomerData GetCustomer(long customerId, bool isAdmin)
        {
            CustomerData customer = new CustomerData();

            this.Execute(
                "SELECT Login,Password,AccessRights,FirstName,LastName"
                + " FROM Customer WHERE Id=@p AND Deleted=false",
                cmd =>
                {
                    this.AddParam(cmd, "p", customerId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            customer.Id = customerId;
                            customer.Login = this.GetParamString(reader, 0);
                            customer.Password = this.GetParamString(reader, 1);
                            customer.AccessRights = this.GetParamString(reader, 2);
                            customer.FirstName = this.GetParamString(reader, 3);
                            customer.LastName = this.GetParamString(reader, 4);

                            return;
                        }

                        throw new Exception(string.Format("Пользователя с id '{0}' не существует", customerId));
                    }
                });
            var currentWeekMonday = this.GetCurrentWeekMonday();
            customer.Orders =
                this.GetOrders(isAdmin ? (long?)null : customer.Id, null, currentWeekMonday.ToEpochtime(), currentWeekMonday.AddDays(7).ToEpochtime());
            
            return customer;
        }

        public CustomerData GetCustomer(string login)
        {
            CustomerData customer = new CustomerData();

            this.Execute(
                "SELECT Id,Password,AccessRights,FirstName,LastName"
                + " FROM Customer WHERE Login=@p AND Deleted=false",
                cmd =>
                {
                    this.AddParam(cmd, "p", login, 64);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            customer.Login = login;
                            customer.Id = reader.GetInt64(0);
                            customer.Password = this.GetParamString(reader, 1);
                            customer.AccessRights = this.GetParamString(reader, 2);
                            customer.FirstName = this.GetParamString(reader, 3);
                            customer.LastName = this.GetParamString(reader, 4);

                            return;
                        }

                        throw new Exception(string.Format("Пользователя с логином '{0}' не существует", login));
                    }
                });
            var currentWeekMonday = this.GetCurrentWeekMonday();
            customer.Orders =
                this.GetOrders(customer.Id, null, currentWeekMonday.ToEpochtime(), currentWeekMonday.AddDays(7).ToEpochtime());

            return customer;
        }

        public List<CustomerData> GetCustomers()
        {
            List<CustomerData> customers = new List<CustomerData>();
            this.Execute(
                "SELECT Id,Login,Password,AccessRights,FirstName,LastName FROM Customer WHERE Deleted='F'",
                cmd =>
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            CustomerData customer = new CustomerData();
                            customer.Id = reader.GetInt64(0);
                            customer.Login = this.GetParamString(reader, 1);
                            customer.Password = this.GetParamString(reader, 2);
                            customer.AccessRights = this.GetParamString(reader, 3);
                            customer.FirstName = this.GetParamString(reader, 4);
                            customer.LastName = this.GetParamString(reader, 5);
                            customer.Orders = this.GetOrders(customer.Id, null, null, null);
                            customers.Add(customer);
                        }
                    }
                });
            return customers;
        }

        public void DeleteCustomer(CustomerData customer)
        {
            this.ExecuteDelete(customer);
        }

        #endregion

        #region Dishes

        public long CreateDish(DishData dish)
        {
            return this.ExecuteCreate(
                dish,
                e =>
                {
                    if (e.Message.Contains("idx_dish_name_type_date"))
                    {
                        throw new Exception(string.Format("Блюдо с именем '{0}' и типом {1} с датой {2} уже существует", dish.Name, dish.DishType, dish.ValidDate?.ToShortDateString()));
                    }

                    return true;
                });
        }

        public void UpdateDish(DishData dish)
        {
            this.ExecuteUpdate(
                dish,
                e =>
                {
                    if (e.Message.Contains("idx_dish_name_type_date"))
                    {
                        throw new Exception(string.Format("Блюдо с именем '{0}' и типом {1} с датой {2} уже существует", dish.Name, dish.DishType, dish.ValidDate?.ToShortDateString()));
                    }

                    return true;
                });
        }

        public DishData GetDish(long dishId)
        {
            DishData dish = null;
            this.Execute(
                "SELECT Name,DishType,Price,ValidDate FROM Dish"
                + " WHERE Id=@p1 AND Deleted='F'",
                cmd =>
                {
                    this.AddParam(cmd, "p1", dishId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            dish = new DishData();
                            dish.Id = dishId;
                            dish.Name = this.GetParamString(reader, 0);
                            dish.DishType = this.GetParamString(reader, 1);
                            dish.Price = this.GetParamDouble(reader, 2);
                            dish.ValidDateEpochtime = this.GetParamLong(reader, 3);
                        }
                    }
                });
            return dish;
        }

        // Get Dishes with orders for customerId and dishId or only for dishId if customerId = null
        public List<DishData> GetDishes(long? customerId, long? startDate, long? endDate)
        {
            List<DishData> dishes = new List<DishData>();
            string startDateQuery = startDate != null ? " AND ValidDate>=@p1" : string.Empty;
            string endDateQuery = endDate != null ? " AND ValidDate<@p2" : string.Empty;
            this.Execute(
                "SELECT Id,Name,DishType,Price,ValidDate FROM Dish"
                + " WHERE Deleted='F'" + startDateQuery + endDateQuery,
                cmd =>
                {
                    if (startDate != null)
                    {
                        this.AddParam(cmd, "p1", startDate);
                    }

                    if (endDate != null)
                    {
                        this.AddParam(cmd, "p2", endDate);
                    }

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            DishData dish = new DishData();
                            dish.Id = reader.GetInt64(0);
                            dish.Name = this.GetParamString(reader, 1);
                            dish.DishType = this.GetParamString(reader, 2);
                            dish.Price = this.GetParamDouble(reader, 3);
                            dish.ValidDateEpochtime = this.GetParamLong(reader, 4);
                            dish.Orders = this.GetOrders(customerId, dish.Id, null, null);
                            dishes.Add(dish);
                        }
                    }
                });
            return dishes;
        }

        public void DeleteDish(DishData dish)
        {
            this.ExecuteDelete(dish);
        }

        #endregion

        #region Orders

        public long CreateOrder(OrderData order)
        {
            return this.ExecuteCreate(
                order,
                e =>
                {
                    if (e.Message.Contains("idx_order_customerid_dishid"))
                    {
                        throw new Exception(string.Format("Заказ у пользователя с id '{0}' блюда с id '{1}' уже существует", order.CustomerId, order.DishId));
                    }

                    return true;
                });
        }

        public void UpdateOrder(OrderData order)
        {
            this.ExecuteUpdate(
                order,
                e =>
                {
                    if (e.Message.Contains("idx_order_customerid_dishid"))
                    {
                        throw new Exception(string.Format("Заказ у пользователя с id '{0}' блюда с id '{1}' уже существует", order.CustomerId, order.DishId));
                    }

                    return true;
                });
        }

        public List<OrderData> GetOrders(long? customerId, long? dishId, long? startDate, long? endDate)
        {
            List<OrderData> orders = new List<OrderData>();
            string customerIdQuery = customerId != null ? " AND CustomerId=@p1" : string.Empty;
            string dishIdQuery = dishId != null ? " AND DishId=@p2" : string.Empty;
            string startDateQuery = startDate != null ? " AND ValidDate>=@p3" : string.Empty;
            string endDateQuery = endDate != null ? " AND ValidDate<@p4" : string.Empty;
            string dishDateQuery = (dishId == null && (startDate != null || endDate != null)) ?
                " AND DishId=ANY(Select Id FROM Dish WHERE Deleted='F'" + startDateQuery + endDateQuery + ")" : string.Empty;
            this.Execute(
                "SELECT Id,CustomerId,DishId,Count FROM Orders"
                + " WHERE Deleted='F'" + customerIdQuery + dishIdQuery + dishDateQuery,
                cmd =>
                {
                    if (customerId != null)
                    {
                        this.AddParam(cmd, "p1", customerId);
                    }

                    if (dishId != null)
                    {
                        this.AddParam(cmd, "p2", dishId);
                    }

                    if (startDate != null)
                    {
                        this.AddParam(cmd, "p3", startDate);
                    }

                    if (endDate != null)
                    {
                        this.AddParam(cmd, "p4", endDate);
                    }

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            OrderData order = new OrderData();
                            order.Id = reader.GetInt64(0);
                            order.CustomerId = reader.GetInt64(1);
                            order.DishId = reader.GetInt64(2);
                            order.Count = this.GetParamDouble(reader, 3);
                            orders.Add(order);
                        }
                    }
                });
            return orders;
        }

        public void DeleteOrder(OrderData order)
        {
            this.ExecuteDelete(order);
        }

        #endregion

        #region private

        private DateTime GetCurrentWeekMonday()
        {
            DateTime result = DateTime.Now;
            while (result.DayOfWeek != DayOfWeek.Monday)
            {
                result = result.AddDays(-1);
            }

            return new DateTime(result.Year, result.Month, result.Day);
        }

        private void CreateTables()
        {
            this.CreateTableSession();
            this.CreateTableCustomer();
            this.CreateTableDish();
            this.CreateTableOrder();
        }

        private void CreateTableSession()
        {
            this.Execute("CREATE SEQUENCE IF NOT EXISTS Seq_Session_Id");

            this.Execute(
                "CREATE TABLE IF NOT EXISTS Session"
                + "("
                + "Id BIGINT PRIMARY KEY DEFAULT NEXTVAL('Seq_Session_Id'),"
                + "SessionToken VARCHAR(32) NOT NULL,"
                + "CustomerId BIGINT NOT NULL,"
                + "AccessRights VARCHAR(16) NOT NULL,"
                + "ExpirationDate BIGINT NOT NULL,"
                + "Deleted BOOLEAN"
                + ")");

            this.Execute("CREATE INDEX IF NOT EXISTS Idx_Session_CustomerId ON Session(CustomerId, Deleted)");
            this.Execute("CREATE UNIQUE INDEX IF NOT EXISTS Idx_Session_SessionToken ON Session(SessionToken)");
        }

        private void CreateTableCustomer()
        {
            this.Execute("CREATE SEQUENCE IF NOT EXISTS Seq_Customer_Id");

            this.Execute(
                "CREATE TABLE IF NOT EXISTS Customer"
                + "("
                + "Id BIGINT PRIMARY KEY DEFAULT NEXTVAL('Seq_Customer_Id'),"
                + "Login VARCHAR(64) NOT NULL,"
                + "Password VARCHAR(64) NOT NULL,"
                + "AccessRights VARCHAR(16) NOT NULL,"
                + "FirstName VARCHAR(32) NOT NULL,"
                + "LastName VARCHAR(32) NOT NULL,"
                + "Deleted BOOLEAN"
                + ")");

            this.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_customer_login ON Customer(Login)");
        }

        private void CreateTableDish()
        {
            this.Execute("CREATE SEQUENCE IF NOT EXISTS Seq_Dish_Id");

            this.Execute(
                "CREATE TABLE IF NOT EXISTS Dish"
                + "("
                + "Id BIGINT PRIMARY KEY DEFAULT NEXTVAL('Seq_Dish_Id'),"
                + "Name VARCHAR(64) NOT NULL,"
                + "DishType VARCHAR(16) NOT NULL,"
                + "Price DOUBLE PRECISION NOT NULL,"
                + "ValidDate BIGINT NOT NULL,"
                + "Deleted BOOLEAN"
                + ")");
        }

        private void CreateTableOrder()
        {
            this.Execute("CREATE SEQUENCE IF NOT EXISTS Seq_Order_Id");

            this.Execute(
                "CREATE TABLE IF NOT EXISTS Orders"
                + "("
                + "Id BIGINT PRIMARY KEY DEFAULT NEXTVAL('Seq_Order_Id'),"
                + "CustomerId BIGINT NOT NULL REFERENCES Customer(Id) ON DELETE CASCADE,"
                + "DishId BIGINT NOT NULL REFERENCES Dish(Id) ON DELETE CASCADE,"
                + "Count DOUBLE PRECISION NOT NULL,"
                + "Deleted BOOLEAN"
                + ")");
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
        private void ExecuteUpdate(object data, Func<Exception, bool> exception = null)
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
        private void ExecuteDelete(object data, Func<Exception, bool> exception = null)
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
        private void ExecutePurge(object data, Func<Exception, bool> exception = null)
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
        private long ExecuteCreate(object data, Func<Exception, bool> exception = null)
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