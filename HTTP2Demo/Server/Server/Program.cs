﻿using Microsoft.Owin.Hosting;
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
            using (WebApplication.Start<Startup>(options =>
                {
                    options.Url = "http://localhost:12345/";
                    options.Server =
                        // "Microsoft.Owin.Host.HttpListener"; // No opaque or 2.0 frames
                        // "Microsoft.Owin.Host.HttpSys"; // Opaque only
                        "SocketServer"; // 2.0 frames only
                        // "Firefly"; // Opaque?
                }))
            {
                Console.WriteLine("Started");
                Console.ReadKey();
                Console.WriteLine("Ended");
            }
        }
    }
}
