using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Web;
using BackendCommon;

namespace BackendAppServer
{
    [ServiceContract]
    public interface IRestApiService
    {
        #region Sessions

        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "login/{login}/{password}")]
        void Login(string login, string password);

        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "logout")]
        void Logout();

        #endregion 

        #region Customers

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "customers")]
        void CreateCustomer(CustomerData customer);

        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "customers/{login}")]
        CustomerData GetCustomer(string login);

        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "customers")]
        List<CustomerData> GetCustomers();

        [OperationContract]
        [WebInvoke(Method = "DELETE", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "customers?id={id}")]
        void DeleteCustomer(string id);

        #endregion

        #region Dishes

        [OperationContract]
        [WebInvoke(Method = "POST", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "dishes")]
        List<long> CreateDishes(List<DishData> dishes);

        [OperationContract]
        [WebInvoke(Method = "PUT", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "dishes")]
        void UpdateDishes(List<DishData> dishes);

        // Get Dishes with orders for customerId and dishId or only for dishId if customerId = null
        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "dishes?cid={customerId}&start={startDate}&end={endDate}")]
        List<DishData> GetDishes(string customerId, string startDate, string endDate);

        #endregion

        #region Orders

        [OperationContract]
        [WebInvoke(Method = "POST", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "orders")]
        List<long> CreateOrders(List<OrderData> orders);

        [OperationContract]
        [WebInvoke(Method = "PUT", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "orders")]
        void UpdateOrders(List<OrderData> orders);

        #endregion
    }
}
