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

        #region Audit

        // Create audit record on object change
        public void CreateAudit(AuditData audit)
        {
            db.CreateAudit(audit);
        }

        // Get audit information by object
        public List<AuditData> SearchAudit(string tableName, long id, string columnName, long startTime, long stopTime, bool includePrevious, long maxCount)
        {
            return db.SearchAudit(tableName, id, columnName, startTime, stopTime, includePrevious, maxCount);
        }

        // Get audit information by user
        public List<AuditData> SearchAudit(long customerId, long startTime, long stopTime, long maxCount)
        {
            return db.SearchAudit(customerId, startTime, stopTime, maxCount);
        }

        #endregion

        #region Sessions

        // Create login session
        public void CreateSesson(SessionData session, int maxSessionCount)
        {
            db.CreateSesson(session, maxSessionCount);
        }

        // Get login session
        public SessionData GetSesson(string sessionToken)
        {
            return db.GetSesson(sessionToken);
        }

        // Delete session
        public void DeleteSession(string sessionToken)
        {
            db.DeleteSession(sessionToken);
        }

        // Delete all sessions of this customer
        public void DeleteSession(long customerId)
        {
            db.DeleteSession(customerId);
        }

        // Search for sessions
        public List<SessionData> SearchSessions(string lastId, long startTime, long stopTime, string accessRights, string ip, string phone, string email, string organizationName, string firstName, string middleName, string lastName, long maxCount)
        {
            return db.SearchSessions(lastId, startTime, stopTime, accessRights, ip, phone, email, organizationName, firstName, middleName, lastName, maxCount);
        }

        #endregion

        #region Customers

        // Create new customer, returns Id
        public long CreateCustomer(CustomerData customer, long who)
        {
            return db.CreateCustomer(customer, who);
        }

        // Update info for the customer using Id, only not null fields are updated
        public void UpdateCustomer(CustomerData customer, long who)
        {
            db.UpdateCustomer(customer, who);
        }

        // Get customer info using phone
        public CustomerData GetCustomer(long phone, bool showHidden)
        {
            return db.GetCustomer(phone, showHidden);
        }

        // Get customer info using email
        public CustomerData GetCustomer(string email, bool showHidden)
        {
            return db.GetCustomer(email, showHidden);
        }

        // Get customer info using Id
        public CustomerData GetCustomerById(long id, bool showHidden)
        {
            return db.GetCustomerById(id, showHidden);
        }

        // Get customer info using RFID
        public CustomerData GetCustomerByRFID(string rfid, bool showHidden)
        {
            return db.GetCustomerByRFID(rfid, showHidden);
        }

        // Search for customers
        public List<CustomerData> SearchCustomers(string lastId, string phone, string email, string organizationName, string firstName, string middleName, string lastName, long maxCount)
        {
            return db.SearchCustomers(lastId, phone, email, organizationName, firstName, middleName, lastName, maxCount);
        }

        // Delete the customer
        public void DeleteCustomer(CustomerData customer, long who)
        {
            db.DeleteCustomer(customer, who);
        }

        // Purge the customer
        public void PurgeCustomer(CustomerData customer, long who)
        {
            db.PurgeCustomer(customer, who);
        }

        #endregion

        #region Cars

        // Create new car, returns Id
        public long CreateCar(CarData car, long who)
        {
            return db.CreateCar(car, who);
        }

        // Update info for the car using Id, only not null fields are updated
        public void UpdateCar(CarData car, long who)
        {
            db.UpdateCar(car, who);
        }

        // Get list of cars with info using customer Id
        public List<CarData> GetCars(long customerId, bool showHidden)
        {
            return db.GetCars(customerId, showHidden);
        }

        // Delete the car
        public void DeleteCar(CarData car, long who)
        {
            db.DeleteCar(car, who);
        }

        // Purge the car
        public void PurgeCar(CarData car, long who)
        {
            db.PurgeCar(car, who);
        }

        #endregion

        #region RFIDs

        // Create new RFID, returns Id
        public long CreateRFID(RFIDData rfid, long who)
        {
            return db.CreateRFID(rfid, who);
        }

        // Update info for the RFID using Id, only not null fields are updated
        public void UpdateRFID(RFIDData rfid, long who)
        {
            db.UpdateRFID(rfid, who);
        }

        // Get list of RFIDs with info using customer Id
        public List<RFIDData> GetRFIDs(long customerId, bool showHidden)
        {
            return db.GetRFIDs(customerId, showHidden);
        }

        // Search for RFIDs
        public List<RFIDData> SearchRFIDs(string lastId, string value, long maxCount)
        {
            return db.SearchRFIDs(lastId, value, maxCount);
        }

        // Delete the RFID
        public void DeleteRFID(RFIDData rfid, long who)
        {
            db.DeleteRFID(rfid, who);
        }

        // Purge the RFID
        public void PurgeRFID(RFIDData rfid, long who)
        {
            db.PurgeRFID(rfid, who);
        }

        #endregion 

        #region Stations

        // Create new station, returns Id
        public long CreateStation(StationData station, long who)
        {
            return db.CreateStation(station, who);
        }

        // Update info for the station using Id, only not null fields are updated
        public void UpdateStation(StationData station, long who)
        {
            db.UpdateStation(station, who);
        }

        // Get station with info using Station Id
        public StationData GetStation(long id, bool showHidden)
        {
            return db.GetStation(id, showHidden);
        }

        // Search for stations and ports on the map
        public List<StationData> SearchStationsOnMap(string lastId, long[] ignoreIds, double minLatitude, double maxLatitude, double minLongitude, double maxLongitude, string[] accessTypes, string[] stationStatuses, string[] portTypes, string[] portLevels, string[] portStatuses, bool showHidden, long maxCount)
        {
            return db.SearchStationsOnMap(lastId, ignoreIds, minLatitude, maxLatitude, minLongitude, maxLongitude, accessTypes, stationStatuses, portTypes, portLevels, portStatuses, showHidden, maxCount);
        }

        // Search for stations and ports in the town
        public List<StationData> SearchStationsInTown(string lastId, string country, string region, string town, string[] accessTypes, string[] stationStatuses, string[] portTypes, string[] portLevels, string[] portStatuses, bool showHidden, long maxCount)
        {
            return db.SearchStationsInTown(lastId, country, region, town, accessTypes, stationStatuses, portTypes, portLevels, portStatuses, showHidden, maxCount);
        }

        // Delete the station
        public void DeleteStation(StationData station, long who)
        {
            db.DeleteStation(station, who);
        }

        // Purge the station
        public void PurgeStation(StationData station, long who)
        {
            db.PurgeStation(station, who);
        }

        #endregion 

        #region Ports

        // Create new port, returns Id
        public long CreatePort(PortData port, long who)
        {
            return db.CreatePort(port, who);
        }

        // Update info for the port using Id, only not null fields are updated
        public void UpdatePort(PortData port, long who)
        {
            db.UpdatePort(port, who);
        }

        // Get list of ports with info using Station Id
        public List<PortData> GetPorts(long stationId, bool showHidden)
        {
            return db.GetPorts(stationId, showHidden);
        }

        // Get list of ports with not empty DriverName and not deleted, it does not read tariff and some other fields
        public List<PortData> GetPortsWithDriverName(bool showMore)
        {
            return db.GetPortsWithDriverName(showMore);
        }

        // Get port info using Id
        public PortData GetPort(long id, bool showHidden)
        {
            return db.GetPort(id, showHidden);
        }

        // Delete the port
        public void DeletePort(PortData port, long who)
        {
            db.DeletePort(port, who);
        }

        // Purge the port
        public void PurgePort(PortData port, long who)
        {
            db.PurgePort(port, who);
        }

        #endregion 

        #region Tariffs

        // Create new tariff, returns Id
        public long CreateTariff(TariffData tariff, long who)
        {
            return db.CreateTariff(tariff, who);
        }

        // Update info for the tariff using Id, only not null fields are updated
        public void UpdateTariff(TariffData tariff, long who)
        {
            db.UpdateTariff(tariff, who);
        }

        // Get tariff with info using Id
        public TariffData GetTariff(long id, bool showHidden)
        {
            return db.GetTariff(id, showHidden);
        }

        // Search for tariffs
        public List<TariffData> SearchTariffs(string lastId, string name, string description, long maxCount)
        {
            return db.SearchTariffs(lastId, name, description, maxCount);
        }

        // Delete the tariff
        public void DeleteTariff(TariffData tariff, long who)
        {
            db.DeleteTariff(tariff, who);
        }

        // Purge the tariff
        public void PurgeTariff(TariffData tariff, long who)
        {
            db.PurgeTariff(tariff, who);
        }

        #endregion 

        #region ChargingSessions

        // Create the new charging session, returns Id, if it has not empty PaymentData, then it is prepaid
        public long CreateChargingSession(ChargingSessionData session, long who)
        {
            return db.CreateChargingSession(session, who);
        }

        // Update info for the charging session using Id, only not null fields are updated
        public void UpdateChargingSession(ChargingSessionData session, long who)
        {
            db.UpdateChargingSession(session, who);
        }

        // Get charging session with info using Id
        public ChargingSessionData GetChargingSession(long id, long customerId, bool showHidden)
        {
            return db.GetChargingSession(id, customerId, showHidden);
        }

        // Search for stations and ports in the town
        public List<ChargingSessionData> SearchChargingSession(string lastId, long? customerId, long? stationId, long? portId, long startTime, long stopTime, string[] statuses, bool showHidden, long maxCount)
        {
            return db.SearchChargingSession(lastId, customerId, stationId, portId, startTime, stopTime, statuses, showHidden, maxCount);
        }

        #endregion

        #region Payments

        // Create the new payment, returns Id
        public long CreatePayment(PaymentData payment, long who)
        {
            return db.CreatePayment(payment, who);
        }

        // Update info for the payment using Id, only not null fields are updated
        public void UpdatePayment(PaymentData payment, long who)
        {
            db.UpdatePayment(payment, who);
        }

        // Get payment with info using Id
        public PaymentData GetPayment(long id, bool showHidden)
        {
            return db.GetPayment(id, showHidden);
        }

        #endregion
    }
}
