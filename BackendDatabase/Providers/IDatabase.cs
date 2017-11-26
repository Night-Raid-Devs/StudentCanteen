using System.Collections.Generic;
using BackendCommon;
using System.ServiceModel;

namespace BackendDatabase
{
    public interface IDatabase
    {
        void Initialize(ConnectionData data);

        #region Sessions

        void CreateSesson(SessionData session, int maxSessionCount);

        SessionData GetSesson(string sessionToken);

        void DeleteSession(string sessionToken);

        void DeleteSession(long customerId);

        #endregion

        #region Customers

        long CreateCustomer(CustomerData customer);

        CustomerData GetCustomer(long customerId);

        CustomerData GetCustomer(string login);

        List<CustomerData> GetCustomers();

        void DeleteCustomer(CustomerData customer);

        #endregion

        #region Dishes

        long CreateDish(DishData dish);

        void UpdateDish(DishData dish);

        // Get Dishes with orders for customerId and dishId or only for dishId if customerId = null
        List<DishData> GetDishes(long? customerId, long? startDate, long? endDate);

        void DeleteDish(DishData dish);

        #endregion

        #region Orders

        long CreateOrder(OrderData order);

        void UpdateOrder(OrderData order);

        List<OrderData> GetOrders(long? customerId, long? dishId, long? startDate, long? endDate);

        void DeleteOrder(OrderData order);

        #endregion
    }
}