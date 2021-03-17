using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanService
{
    public class ScanRequest
    {
        public int DeviceId { get; set; }
        public bool AutoFeed { get; set; }
        public bool Duplex { get; set; }
        public bool ShowUI { get; set; }
        public string PixelType { get; set; }
 
    }
}
