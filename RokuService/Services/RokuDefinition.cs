using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using yozepi.Roku;

namespace RokuService.Services
{
    [DataContract]
    public class RokuDefinition
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string IPAddress { get; set; }
        [DataMember] public string Name { get; set; }

        public IRokuRemote Roku { get; set; }
    }
}
