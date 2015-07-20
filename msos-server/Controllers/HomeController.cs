using msos_server.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace msos_server.Controllers
{
    public class HomeController : Controller
    {
        private IEnumerable<int> GetEligibleAttachTargets()
        {
            yield return 48;
        }

        private IEnumerable<string> GetEligibleDumpFiles()
        {
            yield return @"C:\Temp\VSDebugging.dmp";
        }

        public ActionResult Index()
        {
            return View(new Home
            {
                AttachTargets = GetEligibleAttachTargets(),
                DumpFiles = GetEligibleDumpFiles()
            });
        }
    }
}