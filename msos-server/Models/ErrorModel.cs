using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace msos_server.Models
{
    public class ErrorModel
    {
        public string Error { get; private set; }

        public ErrorModel(string error)
        {
            Error = error;
        }
    }
}