using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace BackendCommon
{
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

        // User, Admin or Station, see AccessRightEnum
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 16, IsUpdatable = false)]
        public string AccessRights { get; set; }

        // When session expires (Epochtime)
        [DataMember(Name = "ExpirationDate", IsRequired = false, EmitDefaultValue = false), ColumnAttr(Name = "ExpirationDate", IsUpdatable = false)]
        public long? ExpirationDateEpochtime { get; set; }

        public DateTime? ExpirationDate
        {
            get { return Epochtime.ToDateTime(this.ExpirationDateEpochtime); }
        }

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

        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 64)]
        public string Login { get; set; }

        // SHA256 is used for hashing
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 64)]
        public string Password { get; set; }

        // Enumeration of access rights, e.g. User or Admin, see BackendAppServer.AccessRights
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 16)]
        public string AccessRights { get; set; }

        // First name if it is private person
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32)]
        public string FirstName { get; set; }

        // Last name
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 32)]
        public string LastName { get; set; }

        // List of orders, readonly
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public List<OrderData> Orders { get; set; } = new List<OrderData>();

        // If this record is deleted or not
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public bool? Deleted { get; set; }
    }

    [DataContract, TableAttr(Name = "Dish")]
    public class DishData
    {
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsId = true, IsUpdateKey = true)]
        public long Id { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 64)]
        public string Name { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(MaxLength = 16)]
        public string DishType { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public double? Price { get; set; }

        [DataMember(Name = "ValidDate", IsRequired = false, EmitDefaultValue = false), ColumnAttr(Name = "ValidDate")]
        public long? ValidDateEpochtime { get; set; }

        public DateTime? ValidDate
        {
            get { return Epochtime.ToDateTime(this.ValidDateEpochtime); }
        }

        // List of orders, readonly
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public List<OrderData> Orders { get; set; } = new List<OrderData>();

        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public bool? Deleted { get; set; }
    }

    [DataContract, TableAttr(Name = "Orders")]
    public class OrderData
    {
        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr(IsId = true, IsUpdateKey = true)]
        public long Id { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public long? CustomerId { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public long? DishId { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public double? Count { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false), ColumnAttr()]
        public bool? Deleted { get; set; }
    }

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
    }
}