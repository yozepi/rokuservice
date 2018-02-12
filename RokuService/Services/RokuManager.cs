using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using yozepi.Roku;

namespace RokuService.Services
{
    public class RokuManager
    {
        readonly IRokuDiscovery RokuDiscovery;
        readonly ILogger<RokuManager> Logger;

        static Dictionary<string, RokuDefinition> _rokus;

        static object _lockObj = new object();

        public RokuManager(IRokuDiscovery rokuDiscovery, ILogger<RokuManager> logger)
        {
            this.RokuDiscovery = rokuDiscovery;
            this.Logger = logger;
        }

        public async Task<IList<RokuDefinition>> ListRokusAsync(bool forceDiscovery)
        {
            using (Logger.BeginScope("ListRokusAsync"))
            {
                Logger.LogInformation("forceDiscovery: {forceDiscovery}", forceDiscovery);
                VerifyRokuListExists();

                if (forceDiscovery)
                {
                    var rokus = await RokuDiscovery.DiscoverAsync();
                    Logger.LogInformation("{count} Roku(s) discovered on network", rokus.Count);
                    lock (_lockObj)
                    {
                        foreach (var roku in rokus)
                        {
                            var ip = IPAddress.Parse(roku.Url.Host);
                            MergeRoku(roku, null);
                        }
                        SerializeRokus();
                    }
                }
                return _rokus.Values.ToList();
            }
        }

        public async Task<IRokuRemote> GetRokuAsync(string rokuId)
        {
            using (Logger.BeginScope("GetRokuAsync"))
            {
                Logger.LogInformation("rokuId: {id}", rokuId);
                VerifyRokuListExists();

                IRokuRemote result = null;

                if (_rokus.ContainsKey(rokuId))
                {
                    var def = _rokus[rokuId];
                    if (def.Roku == null)
                    {
                        def.Roku = await DiscoverRoku(IPAddress.Parse(def.IPAddress));
                    }
                    result = def.Roku;
                }
                else
                {
                    Logger.LogWarning("A roku with ID# {id} is not defined.", rokuId);
                }

                return result;
            }
        }

        public async Task<RokuDefinition> AddRokuAsync(IPAddress ip, string name = null)
        {
            using (Logger.BeginScope("AddRokuAsync"))
            {
                Logger.LogInformation("ip: {ip}, name: {name}", ip, name);
                var roku = await DiscoverRoku(ip);
                if (roku == null)
                    return null;

                RokuDefinition result = null;

                lock (_lockObj)
                {
                    result = MergeRoku(roku, name);
                    SerializeRokus();
                }

                return result;
            }
        }


        private RokuDefinition MergeRoku(IRokuRemote roku, string name)
        {
            RokuDefinition result;

            var rokuIp = roku.Url.Host;

            if (_rokus.ContainsKey(roku.Info.Id))
            {
                //Updating an existing entry.
                Logger.LogInformation("Merging existing Roku #{id} into list.");
                result = _rokus[roku.Info.Id];
                //Override the name and ip.
                result.Name = !string.IsNullOrWhiteSpace(name?.Trim()) ? name.Trim()
                    : result.Name;

                result.IPAddress = rokuIp;
            }
            else
            {
                //Adding a new entry.
                Logger.LogInformation("Adding Roku #{id} to list.");

                //First use the name parameter, then use the Roku's name, if neither then no name.
                var rokuName = !string.IsNullOrWhiteSpace(name?.Trim()) ? name.Trim()
                     : !string.IsNullOrEmpty(roku.Info.UserDeviceName) ? roku.Info.UserDeviceName
                     : null;

                result = new RokuDefinition
                {
                    Id = roku.Info.Id,
                    Name = rokuName,
                    IPAddress = rokuIp,
                    Roku = roku
                };
                _rokus[result.Id] = result;
            }
            Logger.LogTrace("Name: {name}, IP: {ip}", result.Name, result.IPAddress);

            return result;
        }

        private async Task<IRokuRemote> DiscoverRoku(IPAddress ip)
        {
            Logger.LogInformation("Discovering roku at IP {ip}", ip);
            var result =  await RokuDiscovery.DiscoverAsync(ip);
            if (result == null)
            {
                Logger.LogWarning("Roku could not be discovered at {ip}.", ip);
            }
            return result;
        }

        private void VerifyRokuListExists()
        {

            lock (_lockObj)
            {
                if (_rokus == null)
                {

                    string filename = System.IO.Path.Combine(Environment.CurrentDirectory, "rokus.xml");
                    var serializer = new DataContractSerializer(typeof(Dictionary<string, RokuDefinition>));

                    if (!File.Exists(filename))
                    {
                        _rokus = new Dictionary<string, RokuDefinition>();
                    }
                    else
                    {
                        using (var s = File.OpenRead(filename))
                        {
                            _rokus = serializer.ReadObject(s) as Dictionary<string, RokuDefinition>;
                        }
                        Logger.LogInformation("Loaded {count} roku(s) from file{filename}", _rokus.Count, filename);
                        foreach (var value in _rokus.Values)
                        {
                            Logger.LogTrace("Roku #{id}, IP {ip}", value.Id, value.IPAddress);
                        }
                    }
                }
            }
        }

        private void SerializeRokus()
        {
            string filename = System.IO.Path.Combine(Environment.CurrentDirectory, "rokus.xml");
            var serializer = new DataContractSerializer(typeof(Dictionary<string, RokuDefinition>));
            Logger.LogInformation("Saving {count} roku(s) to file{filename}", _rokus.Count, filename);
            using (var s = File.Create(filename))
            {
                serializer.WriteObject(s, _rokus);
            }
            foreach (var value in _rokus.Values)
            {
                Logger.LogTrace("Roku #{id}, IP {ip}", value.Id, value.IPAddress);
            }
        }
    }
}
