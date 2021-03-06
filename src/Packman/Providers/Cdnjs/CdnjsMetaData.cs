﻿using System.Collections.Generic;
using Newtonsoft.Json;

namespace Packman
{
    public class CdnjsMetaData : IPackageMetaData
    {
        public string Name { get; set; }
        public string Description { get; set; }
        [JsonProperty("version")]
        public string LastVersion { get; set; }
        public string Homepage { get; set; }
        public string GitHub { get; set; }
        [JsonIgnore]
        public string Author { get; set; }
        [JsonProperty("filename")]
        public string MainFile { get; set; }
        public IEnumerable<PackageAsset> Assets { get; set; }
    }
}
