using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace msos_server.Models
{
    public class Home
    {
        public IEnumerable<int> AttachTargets { get; set; }
        public IEnumerable<string> DumpFiles { get; set; }
    }
}