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
    public class LibrariesIOVersion
    {
        public string number { get; set; }
        public DateTime published_at { get; set; }
        public string spdx_expression { get; set; }
        public string original_license { get; set; }
        public object researched_at { get; set; }
        public List<string> repository_sources { get; set; }
    }

    public class LibrariesIOResponse
    {
        public int dependent_repos_count { get; set; }
        public int dependents_count { get; set; }
        public object deprecation_reason { get; set; }
        public string description { get; set; }
        public int forks { get; set; }
        public string homepage { get; set; }
        public List<string> keywords { get; set; }
        public string language { get; set; }
        public string latest_download_url { get; set; }
        public string latest_release_number { get; set; }
        //public DateTime latest_release_published_at { get; set; }
        public string latest_stable_release_number { get; set; }
        //public DateTime latest_stable_release_published_at { get; set; }
        public bool license_normalized { get; set; }
        public string licenses { get; set; }
        public string name { get; set; }
        public List<string> normalized_licenses { get; set; }
        public string package_manager_url { get; set; }
        public string platform { get; set; }
        public int rank { get; set; }
        public string repository_license { get; set; }
        public string repository_url { get; set; }
        public int stars { get; set; }
        public object status { get; set; }
        public List<LibrariesIOVersion> versions { get; set; }

        public License GetLicense()
        {
            if(!string.IsNullOrWhiteSpace(licenses) && !Utils.UnknownLicenseIds.Contains(licenses.ToLower()))
            {
                return new License
                {
                    Id = licenses,
                    Name = licenses
                };
            }
            if (!string.IsNullOrWhiteSpace(repository_license) && !Utils.UnknownLicenseIds.Contains(repository_license.ToLower()))
            {
                return new License
                {
                    Id = repository_license,
                    Name = repository_license
                };
            }
            return null;

        }
    }

    public class LibrariesIOService : ILicenseLookupService
    {
        private readonly string _baseUrl = "https://libraries.io/api/nuget";
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        
        public LibrariesIOService(HttpClient httpClient, string apiKey)
        {
            _httpClient = httpClient;
            _apiKey = apiKey;
        }

        public string DisplayName => "Libraries.io (https://libraries.io/api/nuget)";
        public int Priority => 4;

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

            Console.WriteLine($"Libraries.io, retrieving license for: {id} Version: {version}");

            //ex: https://libraries.io/api/nuget/Microsoft.Extensions.Logging.Log4Net.AspNetCore?api_key=fed9c408e9a813550ee2bd8e34124761
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/{id}?api_key={_apiKey}");
            request.Headers.Accept.ParseAdd("application/json");

            //waiting at least 1 second so not to hit the 60 request/sec rate limit
            await Task.Delay(1010).ConfigureAwait(false);

            LibrariesIOResponse jsonResponse = null;
            // Send HTTP request and handle its response
            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                // License found, extract data
                try
                {
                    jsonResponse = JsonSerializer.Deserialize<LibrariesIOResponse>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                }
                catch (Exception)
                {
                    //ignoring since must be bad json so don't use
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine($"Libraries.io, unable to find package {id} Version: {version}");
            }
            else
            {
                // License not found or any other error with GitHub APIs.
                Console.WriteLine($"Unknown error finding package {id} for version {version}. Status code {response.StatusCode} and message {response.ReasonPhrase}.");
                return null;
            }

            if (jsonResponse != null && jsonResponse.versions.Any(x => x.number == version))
            {
                return jsonResponse.GetLicense();
            }
            return null;
        }
    }
}
