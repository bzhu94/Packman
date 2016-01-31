﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json;

namespace Packman
{
    class JsDelivrPackage
    {
        const string _metaPackageUrlFormat = "http://api.jsdelivr.com/v1/jsdelivr/libraries?name={0}";
        const string _downloadUrlFormat = "https://cdn.jsdelivr.net/{0}/{1}/{2}";

        public string Name { get; set; }
        public IEnumerable<string> Versions { get; set; }

        public async Task<InstallablePackage> ToInstallablePackageAsync(string version, string providerName)
        {
            string rootCacheDir = Environment.ExpandEnvironmentVariables(Defaults.CachePath);
            string dir = Path.Combine(rootCacheDir, providerName, Name, version);
            var metadata = await GetPackageMetaData(providerName);

            if (metadata == null)
            {
                return null;
            }

            var asset = metadata.Assets.FirstOrDefault(a => a.Version.Equals(version, StringComparison.OrdinalIgnoreCase));

            if (!Directory.Exists(dir))
            {
                var list = new List<Task>();

                foreach (string fileName in asset.Files)
                {
                    string url = string.Format(_downloadUrlFormat, Name, asset.Version, fileName);
                    var localFile = new FileInfo(Path.Combine(dir, fileName));

                    localFile.Directory.Create();

                    using (WebClient client = new WebClient())
                    {
                        try
                        {
                            var task = client.DownloadFileTaskAsync(url, localFile.FullName);
                            list.Add(task);
                        }
                        catch
                        {
                        }
                    }
                }

                try
                {
                    await Task.WhenAll(list);
                }
                catch
                {
                    return null;
                }
            }

            var package = new InstallablePackage
            {
                Name = Name,
                Version = asset.Version,
                Files = asset.Files,
                MainFile = asset.MainFile ?? metadata.MainFile,
                AllFiles = asset.Files
            };

            return package;
        }

        public async Task<IPackageMetaData> GetPackageMetaData(string providerName)
        {
            string rootCacheDir = Environment.ExpandEnvironmentVariables(Defaults.CachePath);
            string metaPath = Path.Combine(rootCacheDir, providerName, Name, "metadata.json");

            try
            {
                if (!File.Exists(metaPath))
                {
                    string url = string.Format(_metaPackageUrlFormat, Name);
                    Directory.CreateDirectory(Path.GetDirectoryName(metaPath));

                    using (WebClient client = new WebClient())
                    {
                        await client.DownloadFileTaskAsync(url, metaPath);
                    }
                }

                using (StreamReader reader = new StreamReader(metaPath))
                {
                    string json = await reader.ReadToEndAsync();

                    if (!string.IsNullOrEmpty(json))
                    {
                        return JsonConvert.DeserializeObject<List<JsDelivrMetaData>>(json).FirstOrDefault();
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        static IPackageAsset GetAssetFromDisk(string version, FileInfo metaFile)
        {
            if (!metaFile.Exists)
            {
                return null;
            }

            try
            {
                using (StreamReader reader = metaFile.OpenText())
                {
                    string json = reader.ReadToEnd();
                    var data = JsonConvert.DeserializeObject<List<JsDelivrMetaData>>(json).FirstOrDefault();
                    return data.Assets.FirstOrDefault(a => a.Version.Equals(version, StringComparison.OrdinalIgnoreCase));
                }
            }
            catch (IOException)
            {
                return null;
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
