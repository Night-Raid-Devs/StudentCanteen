using System.Runtime.Serialization;

namespace Infocom.Chargers.BackendCommon
{
    // Access Rights, used by CutomerData
    public enum AccessRightEnum
    {
        User,             // Ordinary user/customer
        RemoteStation,    // Remote charging station that connects to us
        StationOperator,  // Person who has User rights + can manage Charging Stations / Ports + Update Stations/Ports + Search any charging sessions
        Admin             // Administrator
    }

    // CustomerTypes, used by CutomerData
    public enum CustomerTypeEnum
    {
        Private,
        Organization
    }

    // Languages, used by CutomerData, ISO 639-1 Language Codes, https://www.w3schools.com/tags/ref_language_codes.asp
    public enum LanguageEnum
    {
        de,     // German
        en,     // English
        ru,     // Russian
        ua      // Ukranian
    }

    // PriceType, used by TariffData
    public enum PriceTypeEnum
    {
        PerHour,        // Price per hour of usage
        PerMinute,      // Price per minute of usage
        PerKWh,         // Price per 1 KWh of usage
    }

    // Currencies, used by TariffData, currency codes from ISO-4217 https://en.wikipedia.org/wiki/ISO_4217
    public enum CurrencyEnum
    {
        EUR,        // Euro
        RUB,        // Russian ruble
        UAH,        // Ukrainian hryvnia
        USD         // United States dollar
    }

    // Port Types, used by PortData
    public enum PortTypeEnum
    {
        CCS,        // SAE Combo CCS
        CHAdeMO,    // CHAdeMO
        J1772,      // EV Plug (J1772)
        Nema1450,   // Nema 14-50
        Nema515,    // Nema 515
        Nema520,    // Nema 520
        Shuko,      // Shuko (220V, 16A)
        TeslaSuper, // Tesla Supercharger
        Type2,      // Mennekes (Type 2)
        Type3       // EV Plug (Type 3)
    }

    // Levels of charging, used by PortData
    public enum LevelEnum
    {
        Normal,         // Normal charging (Level 1)
        FastCharging,   // FastCharging (Level 2)
        SuperCharging,  // SuperCharging (Level 3)
    }

    // Power Supplies, used by PortData
    public enum PowerSupplyEnum
    {
        AC1,   // Single Phase (AC)
        AC3,   // Three Phase (AC)
        DC     // Direct Current (DC)
    }

    // Statuses of port, used by PortData
    public enum PortStatusEnum
    {
        // Good statuses
        Available,  // Port is working and available for charge
        Occupied,   // Port is working, but is occupied by someone and is not available right now
        Reserved,   // Port is working, but is reserved by someone and is not available right now

        // Bad Statuses
        NoPower,    // Port is disconnected from the power
        Construction,      // Under construction, not built yet, but it is planned to build it
        Maintenance,       // Closed for service maintenance
        EmergencyStop,     // Emergency stop is pressed
        Disabled,   // Disabled and is Out of Service
        Failed,     // Common failure
        Unknown     // Unknown status or No connection to the charging box
    }

    // Port management commands, used by the function ManagePort
    public enum PortCommandEnum
    {
        Restart,    // Restart the port (charging box)
        Enable,     // Enable the port and make it available for charging
        Disable,    // Disable the port and make it unavailable for charging
        Lock,       // Lock the port
        Unlock      // Unlock the port
    }

    // Access Types, used by StationData
    public enum AccessTypeEnum
    {
        Public,         // Anyone can access it
        Restricted,     // Restricted access, e.g. private or for members
        Unknown         // Unknown access type
    }

    // Statuses of station, used by StationData
    public enum StationStatusEnum
    {
        // Good statuses
        Available,      // Station is working and could have some ports available for charge

        // Bad Statuses
        Disconnected,   // Station is not working
        Construction,   // Station is Under construction, not built yet, but it is planned to build it
        Unknown         // Unknown status
    }

    // Statuses of charging sessions, used by ChargingSession
    public enum ChargingSessionStatusEnum
    {
        SessionStart,           // Port is reserved for payment, Waiting for payment hold
        PaymentHolded,          // Payment is holded, unblocking charging
        ChargingUnblocked,      // Unblocked charging port and ready to start charging
        ChargingStarted,        // Charging started, car is charging
        ChargingInterrupted,    // Charging is interrupted
        ChargingStopped,        // Charging sesson is done, calculating PaymentAmount
        PaymentProcess,         // Money are in the process of withdrawing from holded amount
        SessionDone             // Money are taken for charging
    }

    // Driver statuses of operations, used by ChargingSession
    public enum ChargingDriverStatusEnum
    {
        Wait,           // Operation is executed by driver at this moment, user should wait
        Charging,       // Driver is in the process of charging

        // Good statuses
        Success,        // Successful result

        // Bad Statuses
        Failed,         // Common failure
        Timeout,        // Timeout of operation execution in driver
        Occupied,       // Port is occupied
        Rejected,       // Rejected to execute operation
        NotAvailable,   // Port is not available for operations
        NoDriver,       // No driver is found for this port
        NoCarCable,     // Cable to the car was not connected or was unplugged
        EmergencyStop,  // Emergency stop is pressed
        Exception,      // Driver returned exception error
        NoPower,        // Port is disconnected from the power
        Restart,        // Charging box/Port is restarted
        Maintenance,    // Service maintenance
        CableLockFailed, // It is not possible to lock charging cable in the charging box
        NoConnection,   // There is no connection to the charging port
        NoPortOrder     // No PortOrder is specified in the port configuration
    }

    // Reason why charging session was stopped, used by StopReason
    public enum StopReasonEnum
    {
        AboveMaxChargeTime,         // Charging time is above Max allowed
        AboveMaxEnergyConsumed,     // Energy consumed is above Max allowed
        NoChargingCallsFromDriver,  // Driver does not call back the Charging function, may be it suspended or charging box has been disconnected
        NoEnergyConsumption,        // There is no or low enery consumption, may be the car is full or charging cable is disconnected
        StoppedByCustomer,          // Is stopped by customer, e.g. from Mobile
        StoppedByDriver,            // Is stopped by driver, read ChargingDriverStatusEnum for more info
        ServerRestart,              // The server has been restarted
        PaymentTimeout,             // It takes too long time to pay
        UnknownPaymentService       // Not supported payment service
    }

    // Payment services, used by Payment
    public enum PaymentServiceEnum
    {
        LiqPay,                 // via LiqPay
        SMS,                    // Prepaid via SMS
        Cash,                   // Paid by cash
        Credit                  // Will be credit, e.g. using RFID cards
    }

    // Payment statuses, used by Payment
    public enum PaymentStatusEnum
    {
        NeedToHold, // need to hold money
        NeedToPay,  // need to pay (withdraw money)
        NeedToReverse,   // need to reverse money back to the customer
        Paid,       // success, wait_compensation in LiqPay
        Holded,     // hold_wait in LiqPay
        Reversed,   // reversed, wait_reserve in LiqPay
        Error,      // failure, sender_verify, receiver_verify in LiqPay
        Test,       // sandbox in LiqPay
        Verify,     // otp_verify, 3ds_verify, cvv_verify in LiqPay
        Wait        // error (means payment is not created yet), processing, prepared, wait_secure, wait_accept, wait_sender in LiqPay
    }

    [DataContract]
    public struct RestApiErrorMessage
    {
        [DataMember]
        public string Error;

        public RestApiErrorMessage(string message)
        {
            this.Error = message;
        }
    }
}