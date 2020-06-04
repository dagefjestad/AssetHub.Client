using System;
using System.Collections.Generic;
using System.Text;

namespace AssetHub.DeviceClient
{
    class MeteredValue
    {
        public DateTime PeriodFrom { get; set; }
        public DateTime PeriodTo { get; set; }
        public double AveragePowerConsumptionInKW { get; set; }
        public double AveragePowerGenerationInKW { get; set; }
    }
}
