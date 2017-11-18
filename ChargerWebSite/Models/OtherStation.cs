using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Infocom.Chargers.BackendCommon;

namespace ChargeWebSite.Models
{
    public class OtherStation
    {
        public Position Position { get; set; }

        public string PlaceId { get; set; }

        public string Name { get; set; }

        public string Phone { get; set; }

        public string Address { get; set; }

        public string Url { get; set; }

        public string MapUrl { get; set; }

        public string Web { get; set; }
   
        public bool IsVisible { get; set; } = true;

        public static string GetStationDataStatus(StationData station)
        {
            if (station.Ports.Count == 0)
            {
                return "Недоступна";
            }

            bool occupied = true;
            bool stop = true;
            foreach (PortData port in station.Ports)
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

            if (station.Status == "Unknown")
            {
                return "Статус неизвестен";
            }

            return "Недоступна";
        }

        public override bool Equals(object obj)
        {
            if (obj?.GetType() != typeof(OtherStation))
            {
                return false;
            }

            OtherStation station2 = (OtherStation)obj;
            Position pos1 = this.Position;
            Position pos2 = station2.Position;
            bool result = Math.Round(pos1.Latitude, 6) == Math.Round(pos2.Latitude, 6) &&
                Math.Round(pos1.Longitude, 6) == Math.Round(pos2.Longitude, 6);
            return result;
        }

        public override int GetHashCode()
        {
            Position position = this.Position;
            return Math.Round(position.Latitude, 6).GetHashCode() + Math.Round(position.Longitude, 6).GetHashCode();
        }
    }
}