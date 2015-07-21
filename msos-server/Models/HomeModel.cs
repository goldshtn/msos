using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace msos_server.Models
{
    public class AttachTarget
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; }
    }

    public class HomeModel
    {
        public IEnumerable<AttachTarget> AttachTargets { get; set; }
        public IEnumerable<string> DumpFiles { get; set; }
        public IEnumerable<string> OpenSessionIds { get; set; }
    }
}