using System.Collections.Generic;
using Infocom.Chargers.BackendCommon;

namespace Infocom.Chargers.BackendDatabase
{
    public interface IDatabase
    {
        void Initialize(ConnectionData data);

        #region Sessions

        // Create login session
        void CreateSesson(SessionData sessiondata, int maxSessionCount);

        // Get login session
        SessionData GetSesson(string sessionToken);

        // Delete session
        void DeleteSession(string sessionToken);

        // Delete all sessions of this customer
        void DeleteSession(long customerId);

        // Search for sessions
        List<SessionData> SearchSessions(string lastId, long startTime, long stopTime, string accessRights, string ip, string phone, string email, string organizationName, string firstName, string middleName, string lastName, long maxCount);

        #endregion 

        #region Customers

        // Create new customer, returns Id
        long CreateCustomer(CustomerData customer, long who);

        // Update info for the customer using Id, only not null fields are updated
        void UpdateCustomer(CustomerData customer, long who);

        // Get customer info using phone
        CustomerData GetCustomer(long phone, bool showHidden);

        // Get customer info using email
        CustomerData GetCustomer(string email, bool showHidden);

        // Get customer info using Id
        CustomerData GetCustomerById(long id, bool showHidden);

        // Get customer info using RFID
        CustomerData GetCustomerByRFID(string rfid, bool showHidden);

        // Search for customers
        List<CustomerData> SearchCustomers(string lastId, string phone, string email, string organizationName, string firstName, string middleName, string lastName, long maxCount);

        // Delete the customer
        void DeleteCustomer(CustomerData customer, long who);

        // Purge the customer
        void PurgeCustomer(CustomerData customer, long who);

        #endregion 

        #region Cars

        // Create new car, returns Id
        long CreateCar(CarData car, long who);

        // Update info for the car using Id, only not null fields are updated
        void UpdateCar(CarData car, long who);

        // Get list of cars with info using customer Id
        List<CarData> GetCars(long customerId, bool showHidden);

        // Delete the car
        void DeleteCar(CarData car, long who);

        // Purge the car
        void PurgeCar(CarData car, long who);

        #endregion 

        #region RFIDs

        // Create new RFID, returns Id
        long CreateRFID(RFIDData rfid, long who);

        // Update info for the RFID using Id, only not null fields are updated
        void UpdateRFID(RFIDData rfid, long who);

        // Get list of RFIDs with info using customer Id
        List<RFIDData> GetRFIDs(long customerId, bool showHidden);

        // Search for RFIDs
        List<RFIDData> SearchRFIDs(string lastId, string value, long maxCount);

        // Delete the RFID
        void DeleteRFID(RFIDData rfid, long who);

        // Purge the RFID
        void PurgeRFID(RFIDData rfid, long who);

        #endregion 

        #region Stations

        // Create new station, returns Id
        long CreateStation(StationData station, long who);

        // Update info for the station using Id, only not null fields are updated
        void UpdateStation(StationData station, long who);

        // Get station with info using Station Id
        StationData GetStation(long id, bool showHidden);

        // Search for stations and ports on the map
        List<StationData> SearchStationsOnMap(string lastId, long[] ignoreIds, double minLatitude, double maxLatitude, double minLongitude, double maxLongitude, string[] accessTypes, string[] stationStatuses, string[] portTypes, string[] portLevels, string[] portStatuses, bool showHidden, long maxCount);

        // Search for stations and ports in the town
        List<StationData> SearchStationsInTown(string lastId, string country, string region, string town, string[] accessTypes, string[] stationStatuses, string[] portTypes, string[] portLevels, string[] portStatuses, bool showHidden, long maxCount);

        // Delete the station
        void DeleteStation(StationData station, long who);

        // Purge the station
        void PurgeStation(StationData station, long who);

        #endregion 

        #region Ports

        // Create new port, returns Id
        long CreatePort(PortData port, long who);

        // Update info for the port using Id, only not null fields are updated
        void UpdatePort(PortData port, long who);

        // Get list of ports with info using Station Id
        List<PortData> GetPorts(long stationId, bool showHidden);

        // Get list of ports with not empty DriverName and not deleted, it does not read tariff and some other fields
        List<PortData> GetPortsWithDriverName(bool showMore);

        // Get port info using Id
        PortData GetPort(long id, bool showHidden);

        // Delete the port
        void DeletePort(PortData port, long who);

        // Purge the port
        void PurgePort(PortData port, long who);

        #endregion 

        #region Tariffs

        // Create new tariff, returns Id
        long CreateTariff(TariffData tariff, long who);

        // Update info for the tariff using Id, only not null fields are updated
        void UpdateTariff(TariffData tariff, long who);

        // Get tariff with info using Id
        TariffData GetTariff(long id, bool showHidden);

        // Search for tariffs
        List<TariffData> SearchTariffs(string lastId, string name, string description, long maxCount);

        // Delete the tariff
        void DeleteTariff(TariffData tariff, long who);

        // Purge the tariff
        void PurgeTariff(TariffData tariff, long who);

        #endregion 

        #region ChargingSessions

        // Create the new charging session, returns Id, if it has not empty PaymentData, then it is prepaid
        long CreateChargingSession(ChargingSessionData session, long who);

        // Update info for the charging session using Id, only not null fields are updated
        void UpdateChargingSession(ChargingSessionData session, long who);

        // Get charging session with info using Id
        ChargingSessionData GetChargingSession(long id, long customerId, bool showHidden);

        // Search for stations and ports in the town
        List<ChargingSessionData> SearchChargingSession(string lastId, long? customerId, long? stationId, long? portId, long startTime, long stopTime, string[] statuses, bool showHidden, long maxCount);

        #endregion

        #region Payments

        // Create the new payment, returns Id
        long CreatePayment(PaymentData payment, long who);

        // Update info for the payment using Id, only not null fields are updated
        void UpdatePayment(PaymentData payment, long who);

        // Get payment with info using Id
        PaymentData GetPayment(long id, bool showHidden);

        #endregion
    }
}