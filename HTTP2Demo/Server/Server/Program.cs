using Microsoft.Owin.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            using (
                WebApplication.Start<Startup>(url: "http://localhost:12345/",
                server:
                // "Microsoft.Owin.Host.HttpListener"
                // "Microsoft.Owin.Host.HttpSys"
                "Firefly"
                ))
            {
                Console.WriteLine("Started");
                Console.ReadKey();
                Console.WriteLine("Ended");
            }
        }
    }
}
