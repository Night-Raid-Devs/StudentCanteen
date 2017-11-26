using System.Collections.Generic;
using BackendCommon;

namespace BackendDatabase
{
    // Singletone class
    public sealed class DatabaseManager : IDatabase
    {
        private static DatabaseManager instance = null;
        private static Postgres db = null;

        private DatabaseManager()
        {
            db = new Postgres();
        }

        public static DatabaseManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DatabaseManager();
                }

                return instance;
            }
        }

        public void Initialize(ConnectionData data)
        {
            db.Initialize(data);
        }

        #region Sessions

        public void CreateSesson(SessionData session, int maxSessionCount)
        {
            db.CreateSesson(session, maxSessionCount);
        }

        public SessionData GetSesson(string sessionToken)
        {
            return db.GetSesson(sessionToken);
        }

        public void DeleteSession(string sessionToken)
        {
            db.DeleteSession(sessionToken);
        }

        public void DeleteSession(long customerId)
        {
            db.DeleteSession(customerId);
        }

        #endregion

        #region Customers

        public long CreateCustomer(CustomerData customer)
        {
            return db.CreateCustomer(customer);
        }

        public CustomerData GetCustomer(long customerId)
        {
            return db.GetCustomer(customerId);
        }

        public CustomerData GetCustomer(string login)
        {
            return db.GetCustomer(login);
        }

        public List<CustomerData> GetCustomers()
        {
            return db.GetCustomers();
        }

        public void DeleteCustomer(CustomerData customer)
        {
            db.DeleteCustomer(customer);
        }

        #endregion

        #region Dishes

        public long CreateDish(DishData dish)
        {
            return db.CreateDish(dish);
        }

        public void UpdateDish(DishData dish)
        {
            db.UpdateDish(dish);
        }

        // Get Dishes with orders for customerId and dishId or only for dishId if customerId = null
        // if startDate or endDate -> all orders which dishes have ValidDate within this range
        public List<DishData> GetDishes(long? customerId, long? startDate, long? endDate)
        {
            return db.GetDishes(customerId, startDate, endDate);
        }

        public void DeleteDish(DishData dish)
        {
            db.DeleteDish(dish);
        }

        #endregion

        #region Orders

        public long CreateOrder(OrderData order)
        {
            return db.CreateOrder(order);
        }

        public void UpdateOrder(OrderData order)
        {
            db.UpdateOrder(order);
        }

        public List<OrderData> GetOrders(long? customerId = null, long? dishId = null, long? startDate = null, long? endDate = null)
        {
            return db.GetOrders(customerId, dishId, startDate, endDate);
        }

        public void DeleteOrder(OrderData order)
        {
            db.DeleteOrder(order);
        }

        #endregion

    }
}
