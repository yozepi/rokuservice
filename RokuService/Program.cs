using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using yozepi.Roku;

namespace RokuService
{

    public class Program
    {

        public static int Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();
            try
            {
                Log.Information("Starting web host");
                var ips = GetLocalIPAddresses();
                BuildWebHost(ips, args).Run();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
                return 1;
            }
        }

        static IWebHost BuildWebHost(string[] ips, string[] args)
        {
            var urls = new List<string> { "http://localhost:5000" };
            urls.AddRange(ips.Select(ip => $"http://{ip}:5000"));
            return WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseSerilog()
                .UseUrls(urls.ToArray())
                .Build();
        }

        static string[] GetLocalIPAddresses()
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                throw new Exception("Not connected to a network!");
            }
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ips = new List<string>();
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    ips.Add(ip.ToString());
                }
            }
            if (ips.Count == 0)
            {
                throw new Exception("No network adapters with an IPv4 address in the system!");
            }
            return ips.ToArray();
        }
    }
}
