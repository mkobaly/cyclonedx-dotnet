// This file is part of CycloneDX Tool for .NET
//
// Licensed under the Apache License, Version 2.0 (the “License”);
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an “AS IS” BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) OWASP Foundation. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using NuGet.Packaging;
using License = CycloneDX.Models.License;

namespace CycloneDX.Services
{
    public interface ILicenseLookupService
    {
        int Priority { get; }
        Task<License> GetLicenseAsync(NuspecReader nuspecReader);
        string DisplayName { get; }
    }

    #region "Types for deserialization"
    public class SourceLocation
    {
        public string type { get; set; }
        public string provider { get; set; }
        public string @namespace { get; set; }
        public string name { get; set; }
        public string revision { get; set; }
        public string url { get; set; }
    }

    public class Urls
    {
        public string registry { get; set; }
        public string version { get; set; }
        public string download { get; set; }
    }

    public class Hashes
    {
        public string sha1 { get; set; }
        public string sha256 { get; set; }
    }

    public class ToolScore
    {
        public int total { get; set; }
        public int date { get; set; }
        public int source { get; set; }
        public int declared { get; set; }
        public int discovered { get; set; }
        public int consistency { get; set; }
        public int spdx { get; set; }
        public int texts { get; set; }
    }

    public class Score
    {
        public int total { get; set; }
        public int date { get; set; }
        public int source { get; set; }
        public int declared { get; set; }
        public int discovered { get; set; }
        public int consistency { get; set; }
        public int spdx { get; set; }
        public int texts { get; set; }
    }

    public class Described
    {
        public string releaseDate { get; set; }
        public SourceLocation sourceLocation { get; set; }
        public Urls urls { get; set; }
        public Hashes hashes { get; set; }
        public int files { get; set; }
        public List<string> tools { get; set; }
        public ToolScore toolScore { get; set; }
        public Score score { get; set; }
    }

    public class Attribution
    {
        public int unknown { get; set; }
        public List<string> parties { get; set; }
    }

    public class Discovered
    {
        public int unknown { get; set; }
    }

    public class Core
    {
        public Attribution attribution { get; set; }
        public Discovered discovered { get; set; }
        public int files { get; set; }
    }

    public class Facets
    {
        public Core core { get; set; }
    }

    public class Licensed
    {
        public string declared { get; set; }
        public ToolScore toolScore { get; set; }
        public Facets facets { get; set; }
        public Score score { get; set; }

        public License GetLicense()
        {
            if (!string.IsNullOrWhiteSpace(declared) && !Utils.UnknownLicenseIds.Contains(declared.ToLower()))
            {
                return new License
                {
                    Id = declared,
                    Name = declared
                };
            }
            return null;
        }
    }

    public class Coordinates
    {
        public string type { get; set; }
        public string provider { get; set; }
        public string name { get; set; }
        public string revision { get; set; }
    }

    public class Meta
    {
        public string schemaVersion { get; set; }
        public DateTime updated { get; set; }
    }

    public class Scores
    {
        public int effective { get; set; }
        public int tool { get; set; }
    }

    public class Root
    {
        public Described described { get; set; }
        public Licensed licensed { get; set; }

        //public List<File> files { get; set; }
        public Coordinates coordinates { get; set; }

        public Meta _meta { get; set; }
        public Scores scores { get; set; }
    }
    #endregion

    public class ClearlyDefinedService : ILicenseLookupService
    {
        private readonly string _baseUrl = "https://api.clearlydefined.io/definitions/nuget/nuget/-/";
        private readonly HttpClient _httpClient;
        
        public ClearlyDefinedService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public string DisplayName => "Clearly Defined (https://clearlydefined.io)";
        public int Priority => 5;

        /// <summary>
        /// Tries to get a license from Clearly Defined.
        /// </summary>
        /// <param name="licenseUrl">URL for the license file. Supporting both github.com and raw.githubusercontent.com URLs.</param>
        /// <returns></returns>
        public async Task<License> GetLicenseAsync(NuspecReader nuspecReader)
        {
            if (nuspecReader == null) return null;
            var id = nuspecReader.GetId();
            var version = nuspecReader.GetVersion().OriginalVersion;

            if (string.IsNullOrWhiteSpace(id)) return null;
            if (string.IsNullOrWhiteSpace(version)) return null;

            Console.WriteLine($"Clearly Defined, retrieving license for: {id} Version: {version}");

            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}{id}/{version}");
            request.Headers.Accept.ParseAdd("application/json");

            Root root = null;
            // Send HTTP request and handle its response
            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                // License found, extract data
                root = JsonSerializer.Deserialize<Root>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.Error.WriteLine($"Clearly Defined, unable to find package {id} Version: {version}");
            }
            else
            {
                // License not found or any other error with GitHub APIs.
                Console.WriteLine($"Unknown error finding package {id} for version {version}. Status code {response.StatusCode} and message {response.ReasonPhrase}.");
                return null;
            }

            if (root != null)
            {
                return root.licensed.GetLicense();
            }
            return null;
        }
    }
}
