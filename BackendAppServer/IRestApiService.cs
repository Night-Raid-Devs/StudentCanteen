using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Web;
using Infocom.Chargers.BackendCommon;

namespace Infocom.Chargers.BackendAppServer
{
    [ServiceContract]
    public interface IRestApiService
    {
        #region Common

        // Ping the server
        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "ping")]
        bool Ping();

        // Get echo request back from the server
        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "echo/{echo}")]
        string Echo(string echo);

        // Get echo request back from the server
        [OperationContract]
        [WebInvoke(Method = "POST", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "echo")]
        string EchoPost(string echo);

        // Get echo request back from the server
        [OperationContract]
        [WebInvoke(Method = "PUT", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "echo")]
        string EchoPut(string echo);

        #endregion 

        #region Sessions

        // Login to the server using phone or email
        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "login/{phoneOrEmail}/{password}")]
        void Login(string phoneOrEmail, string password);

        // Login to the server using RFID card
        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "login?rfid={rfid}")]
        void LoginRFID(string rfid);

        // Logout from the server
        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "logout")]
        void Logout();

        // Search for user sessions,
        // where
        //   lastid={lastId} - last id returned in the previous query, is used when we have limit and scrolling to get the next batch of data
        //   start={startTime} - Start time of interval for CreationDate 
        //   stop={stopTime} - Stop time of interval for CreationDate
        //   ar={accessRights} - search for users with specific access rights, e.g. Admin
        //   ip={ip} - IP address of user, search as substring of numbers, e.g. 192.168
        //   p={phone} - phone number of customer, search as substring of numbers, e.g. 38067
        //   e={email} - e-mail of customer, search as substring of chars, case insensitive, e.g. gmail.com
        //   o={organizationName} - organizaion name, search as substring of chars, case insensitive, e.g. infocom
        //   f={firstName} - first name of customer, search as substring of chars, case insensitive, e.g. vlad
        //   m={middleName} - middle name of customer, search as substring of chars, case insensitive, e.g. anat
        //   l={lastName} - last name of customer, search as substring of chars, case insensitive, e.g. smirn
        //   limit={limit} - maximum returned amount of records in one query
        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare,
            UriTemplate = "sessions?lastid={lastId}&start={startTime}&stop={stopTime}&ar={accessRights}&ip={ip}&p={phone}&e={email}&o={organizationName}&f={firstName}&m={middleName}&l={lastName}&limit={limit}")]
        List<SessionData> SearchSessions(string lastId, string startTime, string stopTime, string accessRights, string ip, string phone, string email, string organizationName, string firstName, string middleName, string lastName, string limit);

        #endregion 

        #region Customers

        // Create new customer and login
        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "customers")]
        void CreateCustomer(CustomerData customer);

        // Update info for the customer using Id, only not null fields are updated
        [OperationContract]
        [WebInvoke(Method = "PUT", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "customers")]
        void UpdateCustomer(CustomerData customer);

        // Get customer info using phone or email
        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "customers/{phoneOrEmail}")]
        CustomerData GetCustomer(string phoneOrEmail);

        // Search for customers,
        // where
        //   lastid={lastId} - last id returned in the previous query, is used when we have limit and scrolling to get the next batch of data
        //   p={phone} - phone number of customer, search as substring of numbers, e.g. 38067
        //   e={email} - e-mail of customer, search as substring of chars, case insensitive, e.g. gmail.com
        //   o={organizationName} - organizaion name, search as substring of chars, case insensitive, e.g. infocom
        //   f={firstName} - first name of customer, search as substring of chars, case insensitive, e.g. vlad
        //   m={middleName} - middle name of customer, search as substring of chars, case insensitive, e.g. anat
        //   l={lastName} - last name of customer, search as substring of chars, case insensitive, e.g. smirn
        //   limit={limit} - maximum returned amount of records in one query
        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare,
            UriTemplate = "customers?lastid={lastId}&p={phone}&e={email}&o={organizationName}&f={firstName}&m={middleName}&l={lastName}&limit={limit}")]
        List<CustomerData> SearchCustomers(string lastId, string phone, string email, string organizationName, string firstName, string middleName, string lastName, string limit);

        // Delete/Purge the customer
        [OperationContract]
        [WebInvoke(Method = "DELETE", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "customers?id={id}&purge={purge}")]
        void DeleteCustomer(string id, string purge);

        #endregion

        #region Cars

        // Create new car, returns Id
        [OperationContract]
        [WebInvoke(Method = "POST", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "cars")]
        long CreateCar(CarData car);

        // Update info for the car using Id, only not null fields are updated
        [OperationContract]
        [WebInvoke(Method = "PUT", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "cars")]
        void UpdateCar(CarData car);

        // Get list of cars with info using customer Id
        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "cars/{customerId}")]
        List<CarData> GetCars(string customerId);

        // Delete/Purge the car
        [OperationContract]
        [WebInvoke(Method = "DELETE", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "cars?id={id}&cid={customerId}&purge={purge}")]
        void DeleteCar(string id, string customerId, string purge);

        #endregion

        #region RFIDs

        // Create new RFID, returns Id
        [OperationContract]
        [WebInvoke(Method = "POST", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "RFIDs")]
        long CreateRFID(RFIDData rfid);

        // Update info for the RFID using Id, only not null fields are updated
        [OperationContract]
        [WebInvoke(Method = "PUT", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "RFIDs")]
        void UpdateRFID(RFIDData rfid);

        // Get list of RFIDs with info using customer Id
        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "RFIDs/{customerId}")]
        List<RFIDData> GetRFIDs(string customerId);

        // Search for RFIDs,
        // where
        //   lastid={lastId} - last id returned in the previous query, is used when we have limit and scrolling to get the next batch of data
        //   v={value} - value of RFID, search as substring of chars, case insensitive
        //   limit={limit} - maximum returned amount of records in one query
        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "RFIDs?lastid={lastId}&v={value}&limit={limit}")]
        List<RFIDData> SearchRFIDs(string lastId, string value, string limit);

        // Delete/Purge the RFID
        [OperationContract]
        [WebInvoke(Method = "DELETE", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "RFIDs?id={id}&cid={customerId}&purge={purge}")]
        void DeleteRFID(string id, string customerId, string purge);

        #endregion

        #region Stations

        // Create new station, returns Id
        [OperationContract]
        [WebInvoke(Method = "POST", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "Stations")]
        long CreateStation(StationData station);

        // Update info for the station using Id, only not null fields are updated
        [OperationContract]
        [WebInvoke(Method = "PUT", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "Stations")]
        void UpdateStation(StationData station);

        // Get station with info using Id
        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "Stations/{id}")]
        StationData GetStation(string id);

        // Search for stations and ports,
        // where
        //   lastid={lastId} - last id returned in the previous query, is used when we have limit and scrolling to get the next batch of data
        //   t={type} can be:
        //      map - in the map rectangle, params used: lastid, ignoreIds, la, lo, la2, lo2, at, ss, pt, pl, ps, limit
        //      town - in the town, params used: lastid, c, r, tn, at, ss, pt, pl, ps, limit
        //      road - on the road, near by, params: lastid, ignoreIds, la, lo, d, at, ss, pt, pl, ps, limit
        //   ignoreids={ignoreIds} - comma seperated enumeration of station Ids, which to ignore, for example during zoom out they were cached and do not need to get them again
        //   la={latitude} - Top Latitude coordinate on the map or map position
        //   lo={latitude2} - Bottom Latitude coordinate on the map or map position
        //   la2={longitude} - Left Longitude coordinate on the map
        //   lo2={longitude2} - Right Longitude coordinate on the map
        //   d={distance} - max distance from la/lo in meters (la/lo is current map position)
        //   c={country} - Country code (code is case sesitive, empty means any)
        //   r={region} - Region name (code is case sesitive, empty means any)
        //   tn={town} - Town name (code is case seietive, empty means any)
        //   at={accessTypes} - comma seperated enumeration of station access types (empty means any), e.g.: Public,Restricted 
        //   ss={stationStatuses} - comma seperated enumeration of station statuses (empty means any), e.g.: Available,Disconnected
        //   pt={portType} - comma seperated enumeration of port types (empty means any), e.g.: CHAdeMO,Type2,Shuko 
        //   pl={portLevels} - comma seperated enumeration of port levels (empty means any), e.g.: Normal,FastCharging
        //   ps={portStatuses} - comma seperated enumeration of port statuses (empty means any), e.g.: Available,Reserved
        //   limit={limit} - maximum returned amount of records in one query
        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare,
            UriTemplate = "Stations?lastid={lastId}&ignoreids={ignoreIds}&t={type}&la={latitude}&lo={longitude}&la2={latitude2}&lo2={longitude2}&d={distance}&c={country}&r={region}&tn={town}&at={accessTypes}&ss={stationStatuses}&pt={portTypes}&pl={portLevels}&ps={portStatuses}&limit={limit}")]
        List<StationData> SearchStations(string lastId, string ignoreIds, string type, string latitude, string longitude, string latitude2, string longitude2, string distance, string country, string region, string town, string accessTypes, string stationStatuses, string portTypes, string portLevels, string portStatuses, string limit);

        // Delete/Purge the station
        [OperationContract]
        [WebInvoke(Method = "DELETE", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "Stations?id={id}&purge={purge}")]
        void DeleteStation(string id, string purge);

        #endregion

        #region Tariffs

        // Create new tariff, returns Id
        [OperationContract]
        [WebInvoke(Method = "POST", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "tariffs")]
        long CreateTariff(TariffData tariff);

        // Update info for the tariff using Id, only not null fields are updated
        [OperationContract]
        [WebInvoke(Method = "PUT", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "tariffs")]
        void UpdateTariff(TariffData tariff);

        // Get tariff with info using Id
        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "tariffs/{Id}")]
        TariffData GetTariff(string id);

        // Search for tariffs,
        // where
        //   lastid={lastId} - last id returned in the previous query, is used when we have limit and scrolling to get the next batch of data
        //   n={name} - name of tariff, search as substring of chars, case insensitive, e.g. best
        //   d={description} - description of tariff, search as substring of chars, case insensitive, e.g. best
        //   limit={limit} - maximum returned amount of records in one query
        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "tariffs?lastid={lastId}&n={name}&d={description}&limit={limit}")]
        List<TariffData> SearchTariffs(string lastId, string name, string description, string limit);

        // Delete/Purge the tariff
        [OperationContract]
        [WebInvoke(Method = "DELETE", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, UriTemplate = "tariffs?id={id}&purge={purge}")]
        void DeleteTariff(string id, string purge);

        #endregion

    }
}
