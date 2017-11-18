using System;
using System.Collections.Generic;
using System.Text;

[module: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:FileMayOnlyContainASingleClass", Justification = "Need to use structs as classes")]

namespace Infocom.Chargers.BackendCommon
{
    public class ReservePortData
    {
        public ReservePortData(long portId, int reservationTime)
        {
            this.PortId = portId;
            this.ReservationTime = reservationTime;
        }

        public long PortId { get; set; }

        public int ReservationTime { get; set; }
    }

    public class UnblockPortData
    {
        public UnblockPortData(long portId)
        {
            this.PortId = portId;
        }

        public long PortId { get; set; }
    }

    public class ManagePortData
    {
        public ManagePortData(long portId, PortCommandEnum command)
        {
            this.PortId = portId;
            this.Command = command.ToString();
        }

        public long PortId { get; set; }

        public string Command { get; set; }
    }
}
