using System;
using System.Collections.Generic;
using System.Text;

namespace AssetHub.DeviceClient
{
    class FlexAsset
    {
        public string scopeId { get; set; }
        public string deviceId { get; set; }
        public string primaryKey { get; set; }
        public string asignedHub { get; set; }
        public bool provisioned { get; set; }
    }
}
