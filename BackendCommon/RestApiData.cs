using System.Runtime.Serialization;

namespace BackendCommon
{
    // Access Rights, used by CutomerData
    public enum AccessRightEnum
    {
        User,             // Ordinary user/customer
        Admin             // Administrator
    }

    public enum DishType
    {
        First,
        Second,
        Salad,
        Drink
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