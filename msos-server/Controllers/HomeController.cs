using msos_server.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace msos_server.Controllers
{
    public class HomeController : BaseController
    {
        private IEnumerable<AttachTarget> GetEligibleAttachTargets()
        {
            // TODO Filter out processes of inappropriate bitness, or where permissions are insufficient
            return from process in Process.GetProcesses()
                   select new AttachTarget { ProcessId = process.Id, ProcessName = process.ProcessName };
        }

        private IEnumerable<string> GetEligibleDumpFiles()
        {
            return Directory.EnumerateFiles(@"C:\Temp", "*.dmp");
        }

        public ActionResult Index()
        {
            return View(new HomeModel
            {
                AttachTargets = GetEligibleAttachTargets(),
                DumpFiles = GetEligibleDumpFiles(),
                OpenSessionIds = GetOpenSessionIds()
            });
        }
    }
}