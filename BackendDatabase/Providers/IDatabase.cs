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

        #endregion

        #region Customers

        long CreateCustomer(CustomerData customer);

        CustomerData GetCustomer(string login);

        List<CustomerData> GetCustomers();

        void DeleteCustomer(CustomerData customer);

        #endregion

        #region Dishes

        long CreateDish(DishData dish);

        void UpdateDish(DishData dish);

        List<DishData> GetDishes(long customerId, long startDate, long endDate);

        List<DishData> GetMenuDishes(long startDate, long endDate);

        void DeleteDish(DishData dish);

        #endregion
    }
}