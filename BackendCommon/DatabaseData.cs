using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

[module: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:FileMayOnlyContainASingleClass", Justification = "Need to use structs as classes")]

namespace Infocom.Chargers.BackendCommon
{
    public struct AuditData
    {
        public string TableName;        // Table Name
        public long Id;                 // Object Id in the table
        public string ColumnName;       // Column Name in the table
        public long CreationDateEpochtime;      // When the change of the object happened (Epochtime)
        public string Value;            // New value of the object
        public long CustomerId;         // Unique Id of customer who did this change
        public int Attempts;            // Attempts to write this record

        public AuditData(string tableName, long id, string columnName, long creationDateEpochtime, string value, long customerId)
        {
            this.TableName = tableName;
            this.Id = id;
            this.ColumnName = columnName;
            this.CreationDateEpochtime = creationDateEpochtime;
            this.Value = value;
            this.CustomerId = customerId;
            this.Attempts = 0;
        }
    }

    public struct ConnectionData
    {
        public string Host;             // Database DNS or IP of the server
        public string Port;             // Port number
        public string Database;         // Name of database
        public string User;             // User name to connect
        public string Password;         // User's password
        public int ConnectionPoolSize;  // How many connecitons to hold in connection pool
        public int Timeout;             // Timeout to connect/execute query in seconds
        public int MaxAttempts;         // Max tries if it is connection problem
        public int Delay;               // Delay on reconnection/try in seconds
    }

    public struct StartChargingSessionData
    {
        public long ChargingSessionId;     // Unique Id of the charging session (is used as order_id in LiqPay)
        public string PaymentString;       // String value for post data which should be sent to the payment service
    }

    // Extention of DateTime with epochtime conversion
    public static class Epochtime
    {
        public static long ToEpochtime(this DateTime dateTime)
        {
            return (dateTime.ToUniversalTime().Ticks / 10000000) - 62135596800;
        }

        public static long? ToEpochtime(this DateTime? dateTime)
        {
            return dateTime.HasValue ? (long?)ToEpochtime(dateTime.Value) : null;
        }

        public static DateTime ToDateTime(long epoch)
        {
            DateTime t = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(epoch);
            return t.ToLocalTime();
        }

        public static DateTime ToDateTimeUTC(long epoch)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(epoch);
        }

        public static DateTime? ToDateTime(long? epoch)
        {
            return epoch.HasValue ? (DateTime?)ToDateTime(epoch.Value) : null;
        }

        public static DateTime? ToDateTimeUTC(long? epoch)
        {
            return epoch.HasValue ? (DateTime?)ToDateTimeUTC(epoch.Value) : null;
        }
    }

    [DataContract, TableAttr(Name = "Session")]
    public class SessionData
    {
        // Unique Id of session
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsId = true)]
        public long Id { get; set; }

        // Unique token of session
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsUpdateKey = true, MaxLength = 32)]
        public string SessionToken { get; set; }

        // Unique Id of customer
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsUpdatable = false)]
        public long CustomerId { get; set; }

        // Customer data from Customer table, read only
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public CustomerData Customer { get; set; } = new CustomerData();

        // User, Admin or Station, see AccessRightEnum
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 16, IsUpdatable = false)]
        public string AccessRights { get; set; }

        // IP address of session creation
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsUpdatable = false)]
        public string IP { get; set; }

        // When session was created (Epochtime)
        [DataMember(Name = "CreationDate", IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsUpdatable = false)]
        public long? CreationDateEpochtime { get; set; }

        public DateTime? CreationDate
        {
            get { return Epochtime.ToDateTime(this.CreationDateEpochtime); }
        }

        // When session expires (Epochtime)
        [DataMember(Name = "ExpirationDate", IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsUpdatable = false)]
        public long? ExpirationDateEpochtime { get; set; }

        public DateTime? ExpirationDate
        {
            get { return Epochtime.ToDateTime(this.ExpirationDateEpochtime); }
        }

        // When session was deleted (Epochtime)
        [DataMember(Name = "DeletionDate", IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public long? DeletionDateEpochtime { get; set; }

        public DateTime? DeletionDate
        {
            get { return Epochtime.ToDateTime(this.DeletionDateEpochtime); }
        }

        // USER_AGENT string in HTTP header when session was created
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 128, IsUpdatable = false)]
        public string UserAgent { get; set; }

        // If this record is deleted or not
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public bool? Deleted { get; set; }
    }

    [DataContract, TableAttr(Name = "Customer")]
    public class CustomerData
    {
        // Unique Id of customer
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsId = true, IsUpdateKey = true)]
        public long Id { get; set; }

        // Without '+' and with country code. Example: 380681234567
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsAuditable = true)]
        public long? Phone { get; set; }

        // Will be saved with low case 
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 64, IsAuditable = true)]
        public string Email { get; set; }

        // SHA256 is used for hashing
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 64, IsAuditable = true)]
        public string Password { get; set; }

        // If it is organization
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 128, IsAuditable = true)]
        public string OrganizationName { get; set; }

        // First name if it is private person
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32, IsAuditable = true)]
        public string FirstName { get; set; }

        // Middle name
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32, IsAuditable = true)]
        public string MiddleName { get; set; }

        // Last name
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32, IsAuditable = true)]
        public string LastName { get; set; }

        // 1 - Male, 0 - Female, null - unknown
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsAuditable = true)]
        public bool? Sex { get; set; }

        // Date of birth (Epochtime)
        [DataMember(Name = "BirthDate", IsRequired = false, EmitDefaultValue = false), ColumnAttr(Name = "BirthDate", IsAuditable = true)]
        public long? BirthDateEpochtime { get; set; }

        public DateTime? BirthDate
        {
            get { return Epochtime.ToDateTimeUTC(this.BirthDateEpochtime); }
            set { this.BirthDateEpochtime = value.HasValue ? (long?)new DateTime(value.Value.Year, value.Value.Month, value.Value.Day, 0, 0, 0, DateTimeKind.Utc).ToEpochtime() : null; }
        }

        // Country name
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32, IsAuditable = true)]
        public string Country { get; set; }

        // Region name
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32, IsAuditable = true)]
        public string Region { get; set; }

        // Town name
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32, IsAuditable = true)]
        public string Town { get; set; }

        // Post Index
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 16, IsAuditable = true)]
        public string PostIndex { get; set; }

        // Street name, house number
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 128, IsAuditable = true)]
        public string Address { get; set; }

        // Enumeration of language codes in ISO 639-1, e.g. ru, uk, en, de, see public enum Infocom.Chargers.BackendAppServer.Languages
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 2, IsAuditable = true)]
        public string Language { get; set; }

        // Enumeration of customer types, e.g. Private or Organization, see Infocom.Chargers.BackendAppServer.CustomerTypes
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 16, IsAuditable = true)]
        public string CustomerType { get; set; }

        // Date of creation (Epochtime)
        [DataMember(Name = "CreationDate", IsRequired = false, EmitDefaultValue = false), ColumnAttr(Name = "CreationDate", IsUpdatable = false)]
        public long? CreationDateEpochtime { get; set; }

        public DateTime? CreationDate
        {
            get { return Epochtime.ToDateTime(this.CreationDateEpochtime); }
        }

        // Date of last update (Epochtime)
        [DataMember(Name = "UpdateDate", IsRequired = false, EmitDefaultValue = false), ColumnAttr(Name = "UpdateDate")]
        public long? UpdateDateEpochtime { get; set; }

        public DateTime? UpdateDate
        {
            get { return Epochtime.ToDateTime(this.UpdateDateEpochtime); }
        }

        // Enumeration of access rights, e.g. User or Admin, see Infocom.Chargers.BackendAppServer.AccessRights
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 16, IsAuditable = true)]
        public string AccessRights { get; set; }

        // Some secret question from the list or custom configured
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 64, IsAuditable = true)]
        public string SecretQuestion { get; set; }

        // Answer from the customer to the secret question
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32, IsAuditable = true)]
        public string SecretAnswer { get; set; }

        // If it is mobile
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 20, IsAuditable = true)]
        public string IMEA { get; set; }

        // List of cars, readonly
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public List<CarData> Cars { get; set; } = new List<CarData>();

        // List of RFIDs, readonly
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public List<RFIDData> RFIDs { get; set; } = new List<RFIDData>();

        // Comments are used and shown only for admins
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 128)]
        public string Comments { get; set; }

        // If this record is deleted or not
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsAuditable = true)]
        public bool? Deleted { get; set; }
    }

    [DataContract, TableAttr(Name = "Car")]
    public class CarData
    {
        // Unique Id of car
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsId = true, IsUpdateKey = true)]
        public long Id { get; set; }

        // Id of customer (customer can have several cars)
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsUpdateKey = true)]
        public long? CustomerId { get; set; }

        // Car brand like Volvo, Lada
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32, IsAuditable = true)]
        public string Brand { get; set; }

        // Car model XC90, 21010
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32, IsAuditable = true)]
        public string Model { get; set; }

        // Year when the car was produced
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsAuditable = true)]
        public long? Year { get; set; }

        // Registration number of the car
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 10, IsAuditable = true)]
        public string RegNumber { get; set; }

        // VIN of the car
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 17, IsAuditable = true)]
        public string VIN { get; set; }

        // Date of last update (Epochtime)
        [DataMember(Name = "UpdateDate", IsRequired = false, EmitDefaultValue = false), ColumnAttr(Name = "UpdateDate")]
        public long? UpdateDateEpochtime { get; set; }

        public DateTime? UpdateDate
        {
            get { return Epochtime.ToDateTime(this.UpdateDateEpochtime); }
        }

        // Comments are used and shown only for admins
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 128)]
        public string Comments { get; set; }

        // If this record is deleted or not
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsAuditable = true)]
        public bool? Deleted { get; set; }
    }

    [DataContract, TableAttr(Name = "RFID")]
    public class RFIDData
    {
        // Unique Id of RFID card
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsId = true, IsUpdateKey = true)]
        public long Id { get; set; }

        // Id of customer (customer can have several RFID cards)
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsUpdateKey = true)]
        public long? CustomerId { get; set; }

        // Value of RFID card, max 20 chars, hex values in upper case
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 20, IsUpdatable = false)]
        public string Value { get; set; }

        // If the RFID card is blocked (user can block and unblock)
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsAuditable = true)]
        public bool? Blocked { get; set; }

        // Date of creation (Epochtime)
        [DataMember(Name = "CreationDate", IsRequired = false, EmitDefaultValue = false), ColumnAttr(Name = "CreationDate", IsUpdatable = false)]
        public long? CreationDateEpochtime { get; set; }

        public DateTime? CreationDate
        {
            get { return Epochtime.ToDateTime(this.CreationDateEpochtime); }
        }

        // Comments are used and shown only for admins
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 128)]
        public string Comments { get; set; }

        // If this record is deleted or not
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsAuditable = true)]
        public bool? Deleted { get; set; }
    }

    [DataContract, TableAttr(Name = "Station")]
    public class StationData
    {
        // Unique Id of the charging station
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsId = true, IsUpdateKey = true)]
        public long Id { get; set; }

        // Name of the charging station
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 64, IsAuditable = true)]
        public string Name { get; set; }

        // Description of the charging station
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 255, IsAuditable = true)]
        public string Description { get; set; }

        // GPS Coordinates, Latitude
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsAuditable = true)]
        public double? Latitude { get; set; }

        // GPS Coordinates, Longitude
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsAuditable = true)]
        public double? Longitude { get; set; }

        // Information message, e.g. Temporary closed till some date
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 255, IsAuditable = true)]
        public string InfoMessage { get; set; }

        // Network Name, owner, e.g. Tesla, Infocom, Revolta
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 128, IsAuditable = true)]
        public string NetworkName { get; set; }

        // Without '+' and with country code. Example: 380681234567
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsAuditable = true)]
        public long? Phone { get; set; }

        // Country name
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32, IsAuditable = true)]
        public string Country { get; set; }

        // Region name
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32, IsAuditable = true)]
        public string Region { get; set; }

        // Town name
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32, IsAuditable = true)]
        public string Town { get; set; }

        // Post Index
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 16, IsAuditable = true)]
        public string PostIndex { get; set; }

        // Street name, house number
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 128, IsAuditable = true)]
        public string Address { get; set; }

        // Web page of this network or charging station, URL
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 128, IsAuditable = true)]
        public string Web { get; set; }

        // Open Hours when it works
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 64, IsAuditable = true)]
        public string OpenHours { get; set; }

        // Enumeration of who can access this station, see Infocom.Chargers.BackendAppServer.AccessTypeEnum
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 16, IsAuditable = true)]
        public string AccessType { get; set; }

        // What is accepted for payment (used for info), e.g.: Cash, Visa, RFID
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 64, IsAuditable = true)]
        public string PaymentType { get; set; }

        // Enumeration of charging station statuses, see Infocom.Chargers.BackendAppServer.StationStatusEnum
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32, IsAuditable = true)]
        public string Status { get; set; }

        // Port list with info in this station
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public List<PortData> Ports { get; set; } = new List<PortData>();

        // Date of creation (Epochtime)
        [DataMember(Name = "CreationDate", IsRequired = false, EmitDefaultValue = false), ColumnAttr(Name = "CreationDate", IsUpdatable = false)]
        public long? CreationDateEpochtime { get; set; }

        public DateTime? CreationDate
        {
            get { return Epochtime.ToDateTime(this.CreationDateEpochtime); }
        }

        // Date of last update (Epochtime)
        [DataMember(Name = "UpdateDate", IsRequired = false, EmitDefaultValue = false), ColumnAttr(Name = "UpdateDate")]
        public long? UpdateDateEpochtime { get; set; }

        public DateTime? UpdateDate
        {
            get { return Epochtime.ToDateTime(this.UpdateDateEpochtime); }
        }

        // Comments are used and shown only for admins
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 128)]
        public string Comments { get; set; }

        // If this record is deleted or not
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsAuditable = true)]
        public bool? Deleted { get; set; }
    }

    [DataContract, TableAttr(Name = "Port")]
    public class PortData
    {
        // Unique Id of the charging station
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsId = true, IsUpdateKey = true)]
        public long Id { get; set; }

        // Id of station (station can have several ports)
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsUpdateKey = true)]
        public long? StationId { get; set; }

        // Port brand, e.g. Siemens
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32, IsAuditable = true)]
        public string Brand { get; set; }

        // Port model, e.g. A354HJ
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32, IsAuditable = true)]
        public string Model { get; set; }

        // Serial Number, e.g. 23423543453443
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32, IsAuditable = true)]
        public string SerialNumber { get; set; }

        // Name of the port
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 64, IsAuditable = true)]
        public string Name { get; set; }

        // Enumeration of port types, e.g. Mennekes, Shuko (220V), see Infocom.Chargers.BackendAppServer.PortTypes
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32, IsAuditable = true)]
        public string PortType { get; set; }

        // Enumeration of charging levels, see Infocom.Chargers.BackendAppServer.Levels
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 16, IsAuditable = true)]
        public string Level { get; set; }

        // Voltage, e.g. 400V
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsAuditable = true)]
        public long? Voltage { get; set; }

        // Current in Ampers, e.g. 32A
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsAuditable = true)]
        public long? Current { get; set; }

        // MaxPower in Kilo Watts, e.g. 3.5, 40 kW
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsAuditable = true)]
        public double? MaxPower { get; set; }

        // Enumeration of power supplies, e.g. AC 1 phase, AC 3 phase, DC, see Infocom.Chargers.BackendAppServer.PowerSupplyEnum
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 16, IsAuditable = true)]
        public string PowerSupply { get; set; }

        // Tariff Id from Tariff table
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsAuditable = true)]
        public long? TariffId { get; set; }

        // Tariff data from Tariff table, read only
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public TariffData Tariff { get; set; } = new TariffData();

        // Enumeration of port statusese, see Infocom.Chargers.BackendAppServer.PortStatusEnum
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32, IsAuditable = true)]
        public string Status { get; set; }

        // Date of creation (Epochtime)
        [DataMember(Name = "CreationDate", IsRequired = false, EmitDefaultValue = false), ColumnAttr(Name = "CreationDate", IsUpdatable = false)]
        public long? CreationDateEpochtime { get; set; }

        public DateTime? CreationDate
        {
            get { return Epochtime.ToDateTime(this.CreationDateEpochtime); }
        }

        // Date of last update (Epochtime)
        [DataMember(Name = "UpdateDate", IsRequired = false, EmitDefaultValue = false), ColumnAttr(Name = "UpdateDate")]
        public long? UpdateDateEpochtime { get; set; }

        public DateTime? UpdateDate
        {
            get { return Epochtime.ToDateTime(this.UpdateDateEpochtime); }
        }

        // Driver Name used in BackendCharge module to connect to this port, if it is empty, then we do not surve this port
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 16)]
        public string DriverName { get; set; }

        // Connection String used in driver of BackendCharge module to connect to this port, ports with the same connection string means that they belong to the same hardware
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 128)]
        public string ConnectionString { get; set; }

        // Port Order inside of the same hardware to identify the exact port
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public long? PortOrder { get; set; }

        // Comments are used and shown only for admins
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 128)]
        public string Comments { get; set; }

        // If this record is deleted or not
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsAuditable = true)]
        public bool? Deleted { get; set; }
    }

    [DataContract, TableAttr(Name = "Tariff")]
    public class TariffData
    {
        // Unique Id of the tariff
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsId = true, IsUpdateKey = true)]
        public long Id { get; set; }

        // Unique Tariff Group Name, for exmaple Free of charge
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 64, IsAuditable = true)]
        public string Name { get; set; }

        // Description of tariff, e.g. grivna per minute, grivna per kW, it is what is shown to the customer in Tariff field
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 255, IsAuditable = true)]
        public string Description { get; set; }

        // Payment Reqired, 0 = free of charge, 1 = payment is required
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsAuditable = true)]
        public bool? PaymentReqired { get; set; }

        // Enumeration of Price types, e.g. price per hour, price per minute, price per KW, see Infocom.Chargers.BackendAppServer.PriceTypes
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 16, IsAuditable = true)]
        public string PriceType { get; set; }

        // Price in the currency per PriceType, e.g. 1.5 
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsAuditable = true)]
        public double? Price { get; set; }

        // Enumeration of currency codes from ISO-4217, see Infocom.Chargers.BackendAppServer.Currencies
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 3, IsAuditable = true)]
        public string Currency { get; set; }

        // ... Additional info
        // Detailed info from Tariff table (future implementation)
        // different prices for different time / days of week

        // Comments are used and shown only for admins
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 128)]
        public string Comments { get; set; }

        // If this record is deleted or not
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsAuditable = true)]
        public bool? Deleted { get; set; }
    }

    [DataContract, TableAttr(Name = "ChargingSession")]
    public class ChargingSessionData
    {
        // Unique Id of the charging session (is used as order_id in LiqPay)
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsId = true, IsUpdateKey = true)]
        public long Id { get; set; }

        // Id of customer
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsUpdateKey = true)]
        public long? CustomerId { get; set; }

        // Customer data from Customer table, read only
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public CustomerData Customer { get; set; } = new CustomerData();

        // Id of car (can be empty)
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsUpdatable = false)]
        public long? CarId { get; set; }

        // Car data from Car table, read only
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public CarData Car { get; set; } = new CarData();

        // Id of station
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsUpdatable = false)]
        public long? StationId { get; set; }

        // Station data from Station table, read only
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public StationData Station { get; set; } = new StationData();

        // Id of port
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsUpdatable = false)]
        public long? PortId { get; set; }

        // Port data from Port table, read only
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public PortData Port { get; set; } = new PortData();

        // Date when this charging session was started/created
        [DataMember(Name = "SessionStartDate", IsRequired = false, EmitDefaultValue = false), ColumnAttr(Name = "SessionStartDate", IsUpdatable = false)]
        public long? SessionStartDateEpochtime { get; set; }

        public DateTime? SessionStartDate
        {
            get { return Epochtime.ToDateTime(this.SessionStartDateEpochtime); }
        }

        // Date when this charging session was finished
        [DataMember(Name = "SessionStopDate", IsRequired = false, EmitDefaultValue = false), ColumnAttr(Name = "SessionStopDate")]
        public long? SessionStopDateEpochtime { get; set; }

        public DateTime? SessionStopDate
        {
            get { return Epochtime.ToDateTime(this.SessionStopDateEpochtime); }
        }

        // Date when the first energy was activated for this session and charging of the car started
        [DataMember(Name = "ChargeStartDate", IsRequired = false, EmitDefaultValue = false), ColumnAttr(Name = "ChargeStartDate")]
        public long? ChargeStartDateEpochtime { get; set; }

        public DateTime? ChargeStartDate
        {
            get { return Epochtime.ToDateTime(this.ChargeStartDateEpochtime); }
        }

        // Date when the last energy was given to the car and charging of the car stopped
        [DataMember(Name = "ChargeStopDate", IsRequired = false, EmitDefaultValue = false), ColumnAttr(Name = "ChargeStopDate")]
        public long? ChargeStopDateEpochtime { get; set; }

        public DateTime? ChargeStopDate
        {
            get { return Epochtime.ToDateTime(this.ChargeStopDateEpochtime); }
        }

        // Date when was the last value of Votage, Current received
        [DataMember(Name = "LastValueDate", IsRequired = false, EmitDefaultValue = false), ColumnAttr(Name = "LastValueDate")]
        public long? LastValueDateEpochtime { get; set; }

        public DateTime? LastValueDate
        {
            get { return Epochtime.ToDateTime(this.LastValueDateEpochtime); }
        }

        // Energy Meter start value when charging started in kWh
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public double? EnergyMeterStart { get; set; }

        // Energy Meter stop value when charging stopped in kWh
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public double? EnergyMeterStop { get; set; }

        // The charge time in seconds
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public long? ChargeTime { get; set; }

        // Maximum charge time in seconds which is specified by user or we allow
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public long? MaxChargeTime { get; set; }

        // The consumed energy in kWh, e.g 1.5 kWh
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public double? EnergyConsumed { get; set; }

        // Maximum energy in kWh which is specified by user or we allow
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public double? MaxEnergyConsumed { get; set; }

        // Voltage now, e.g. 400V
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public double? Voltage { get; set; }

        // Current now in Ampers, e.g. 32A
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public double? Current { get; set; }

        // Power now in Kilo Watts, e.g. 3.5, 40 kW
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public double? Power { get; set; }

        // The amount of money to pay, e.g  123.45
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public double? PaymentAmount { get; set; }

        // Enumeration of Price types, e.g. price per hour, price per minute, price per KW, see Infocom.Chargers.BackendAppServer.PriceTypes
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 16, IsAuditable = true)]
        public string PriceType { get; set; }

        // Price in the currency per PriceType, e.g. 1.5 
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsAuditable = true)]
        public double? Price { get; set; }

        // Enumeration of currency codes from ISO-4217, is taken from Tariff table using TariffId, see Infocom.Chargers.BackendAppServer.Currencies
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 3, IsUpdatable = false)]
        public string Currency { get; set; }

        // Enumeration of charging session statuses, see Infocom.Chargers.BackendAppServer.ChargingSessionStatusEnum
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32, IsAuditable = true)]
        public string Status { get; set; }

        // Enumeration of charging driver statuses of operation execution, see Infocom.Chargers.BackendAppServer.ChargingDriverStatusEnum
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32, IsAuditable = true)]
        public string DriverStatus { get; set; }

        // Enumeration of reasons why charging session was stopped, see Infocom.Chargers.BackendAppServer.StopReasonEnum
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32, IsAuditable = true)]
        public string StopReason { get; set; }

        // Id of payment if is prepaid (can be null if it is not paid yet (e.g. it is credit using RFID card), if it is paid by bank card, 
        // then it is not null)
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public long? PaymentId { get; set; }

        // Payment data from Payment table, read only
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public PaymentData Payment { get; set; } = new PaymentData();
    }

    [DataContract, TableAttr(Name = "Payment")]
    public class PaymentData
    {
        // Unique Id of the payment
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsId = true, IsUpdateKey = true)]
        public long Id { get; set; }

        // Id of customer
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsUpdateKey = true)]
        public long? CustomerId { get; set; }

        // The amount of money which was holded/blocked in bank card before the charging session started, e.g 123.45
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public double? AmountHold { get; set; }

        // The amount of money which was paid, e.g 123.45
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public double? AmountPaid { get; set; }

        // Bank's commissioning, e.g 3.45
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public double? Commission { get; set; }

        // The amount of money which was received from payment excluding commissioning, e.g 120.00
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public double? AmountReceived { get; set; }

        // Enumeration of currency codes from ISO-4217, is taken from Tariff table using TariffId, see Infocom.Chargers.BackendAppServer.Currencies
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 3, IsUpdatable = false)]
        public string Currency { get; set; }

        // Date when this payment was created
        [DataMember(Name = "CreationDate", IsRequired = false, EmitDefaultValue = false), ColumnAttr(Name = "CreationDate", IsUpdatable = false)]
        public long? CreationDateEpochtime { get; set; }

        public DateTime? CreationDate
        {
            get { return Epochtime.ToDateTime(this.CreationDateEpochtime); }
        }

        // Date when the amount of money which was holded/blocked in bank card
        [DataMember(Name = "HoldDate", IsRequired = false, EmitDefaultValue = false), ColumnAttr(Name = "HoldDate")]
        public long? HoldDateEpochtime { get; set; }

        public DateTime? HoldDate
        {
            get { return Epochtime.ToDateTime(this.HoldDateEpochtime); }
        }

        // Date when the payment was paid
        [DataMember(Name = "PaidDate", IsRequired = false, EmitDefaultValue = false), ColumnAttr(Name = "PaidDate")]
        public long? PaidDateEpochtime { get; set; }

        public DateTime? PaidDate
        {
            get { return Epochtime.ToDateTime(this.PaidDateEpochtime); }
        }

        // Enumeration of payment services used for this payment, e.g. LiqPay, see Infocom.Chargers.BackendAppServer.PaymentServiceEnum
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 16, IsUpdatable = false)]
        public string PaymentService { get; set; }

        // External Payment Id from payment service, e.g. payment_id in LiqPay
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public long? ExternalPaymentId { get; set; }

        // Enumeration of payment statuses, see Infocom.Chargers.BackendAppServer.PaymentStatusEnum
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 16, IsAuditable = true)]
        public string Status { get; set; }

        // Error description
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32, IsAuditable = true)]
        public string ErrorDescription { get; set; }

        // IP address
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 15)]
        public string IP { get; set; }
    }

    public class TableAttr : Attribute
    {
        public string Name { get; set; }
    }

    public class ColumnAttr : Attribute
    {
        public string Name { get; set; }                // Name of column in Database

        public int MaxLength { get; set; } = 0;         // Max length for string in database

        public bool IsId { get; set; } = false;         // It is Id column

        public bool IsUpdatable { get; set; } = true;   // This column is possible to update

        public bool IsUpdateKey { get; set; } = false;  // It is key in WHERE statement to update record

        public bool IsAuditable { get; set; } = false;  // We save history of changes for this column
    }
}