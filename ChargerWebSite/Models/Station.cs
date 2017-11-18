using Infocom.Chargers.BackendCommon;

namespace ChargeWebSite.Models
{
    public class Station : StationData
    {
        public string FullAddress
        {
            get
            {
                return this.Country + " " + this.Region + " " + this.Town + " " + this.Address;
            }
        }

        public bool GetStationStatus_Boolean
        {
            get
            {
                if (this.Ports.Count == 0)
                {
                    return false; ////Недоступна
                }

                foreach (PortData port in this.Ports)
                {
                    if (port.Status == "Available")
                    {
                        return true; ////Доступна
                    }
                }

                return false;
            }
        }

        public string GetPhone
        {
            get
            {
                return this.Phone == null ? "Не указано" : this.Phone.ToString();
            }
        }

        public static string GetPortStatus(PortData port)
        {
            switch (port.Status)
            {
                case "Available":
                    return "Доступен";
                case "Occupied":
                    return "Занят";
                case "Reserved":
                    return "Зарезервирован";
                case "Stop":
                    return "Аварийный стоп";
                case "Disabled":
                    return "Недоступен";
                default:
                    return "Недоступен";
            }
        }

        public int GetStationStatus_Int()
        {
            if (this.Ports.Count == 0)
            {
                return 1; ////Недоступна
            }

            bool occupied = true;
            bool stop = true;
            foreach (PortData port in this.Ports)
            {
                if (port.Status == "Available")
                {
                    return 2; ////Доступна
                }

                if (port.Status != "Occupied" || port.Status != "Reserved")
                {
                    occupied = false;
                }

                if (port.Status != "Stop")
                {
                    stop = false;
                }
            }

            if (occupied)
            {
                return 3; ////Занята/резерв
            }

            if (stop)
            {
                return 4; ////Аварийный стоп
            }

            if (this.Status == "Unknown")
            {
                return 5; ////Статус неизвестен
            }

            return 1; ////Недоступна
        }

        public string GetStationStatus_String()
        {
            if (this.Ports.Count == 0)
            {
                return "Недоступна";
            }
 
            bool occupied = true;
            bool stop = true;
            foreach (PortData port in this.Ports)
            {
                if (port.Status == "Available")
                {
                    return "Доступна";
                }

                if (port.Status != "Occupied" || port.Status != "Reserved")
                {
                    occupied = false;
                }

                if (port.Status != "Stop")
                {
                    stop = false;
                }
            }

            if (occupied)
            {
                return "Занята/резерв";
            }

            if (stop)
            {
                return "Аварийный стоп";
            }

            if (this.Status == "Unknown")
            {
                return "Статус неизвестен";
            }

            return "Недоступна";
        }

        public byte GetStationStatusColor()
        {
            int stationStatus = this.GetStationStatus_Int();
            switch (stationStatus)
            {
                case 2:
                    return 0; //// Доступна цвет:#66bd39
                case 3:
                    return 1; ////Занята/резерв цвет:blue
                case 4:
                    return 2; ////Аварийный стоп цвет:#edbf07
                case 1:
                    return 3; ////gray
                default:
                    return 3;
            }
        }

        public string GetStatusImage()
        {
            int stationStatus = this.GetStationStatus_Int();
            switch (stationStatus)
            {
                case 2:
                    return "~/Content/images/station_available.png";
                case 3:
                    return "~/Content/images/station_reserved.png";
                case 4:
                    return "~/Content/images/station_stop.png";
                case 1:
                    return "~/Content/images/station_not_available.png";
                case 5:
                    return "~/Content/images/station_unknown.png";
                default:
                    return "~/Content/images/station_unknown.png";
            }
        }

        public string[] GetMarkerImage()
        {
            int stationStatus = this.GetStationStatus_Int();
            switch (stationStatus)
            {
                case 2:
                    return new string[] { "/Content/images/green_marker.png", "/Content/images/selected_green_marker.png" };
                //// selected == true ? "/Content/images/selected_green_marker.png" : "/Content/images/green_marker.png";
                case 3:
                    return new string[] { "/Content/images/blue_marker.png", "/Content/images/selected_blue_marker.png" };
                        ////selected == true ? "/Content/images/selected_blue_marker.png" : "/Content/images/blue_marker.png";
                default:
                    return new string[] { "/Content/images/gray_marker.png", "/Content/images/selected_gray_marker.png" };
                        ////selected == true ? "/Content/images/selected_gray_marker.png" : "/Content/images/gray_marker.png";
            }
        }       

        public void CopyParams(StationData stationData)
        {
            this.AccessType = stationData.AccessType;
            this.Comments = stationData.Comments;
            this.Id = stationData.Id;
            this.Latitude = stationData.Latitude;
            this.Longitude = stationData.Longitude;
            this.Phone = stationData.Phone;
            this.Country = stationData.Country;
            this.InfoMessage = stationData.InfoMessage;
            this.NetworkName = stationData.NetworkName;
            this.OpenHours = stationData.OpenHours;
            this.PaymentType = stationData.PaymentType;
            this.Ports = stationData.Ports;
            this.PostIndex = stationData.PostIndex;
            this.Region = stationData.Region;
            this.Status = stationData.Status;
            this.Town = stationData.Town;
            this.Address = stationData.Address;
            this.Name = stationData.Name;
            this.Web = stationData.Web;
        }
    }
}