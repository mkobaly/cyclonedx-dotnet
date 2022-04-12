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
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading.Tasks;
using CycloneDX.Models;
using License = CycloneDX.Models.v1_3.License;
using System.IO;
using NuGet.Packaging;
using System.Collections.Concurrent;

namespace CycloneDX.Services
{
    /// <summary>
    /// ILicenseCacheRepository provides a simple abstraction for caching
    /// licnese results for components. This saves hitting 3rd party sites
    /// like google, libraries.io and potential rate limits
    /// </summary>
    public interface ILicenseCacheRepository
    {
        Task<string> Read(string id, string version);
        Task Write(string id, string version, string licenseId);
    }

    /// <summary>
    /// LicenseFileCacheRepository is a file based cache
    /// </summary>
    public class LicenseFileCacheRepository : ILicenseCacheRepository
    {
        private readonly string _rootPath;
        public LicenseFileCacheRepository(string rootPath)
        {
            _rootPath = Path.Combine(rootPath, "cyclonedx_cache");
            if (!Directory.Exists(_rootPath))
            {
                Directory.CreateDirectory(_rootPath);
            }
        }

        public async Task Write(string id, string version, string licenseId)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (string.IsNullOrEmpty(version)) return;
            if (string.IsNullOrEmpty(licenseId)) return;
            var file = Path.Combine(_rootPath, id.ToLowerInvariant());
            var content = $"{version}||{licenseId}";
            if (File.Exists(file))
            {
                var lines = await File.ReadAllLinesAsync(file).ConfigureAwait(false);
                foreach (var line in lines)
                {
                    var parts = line.Split("||");
                    if (parts[0] == version)
                    {
                        return;
                    }
                }
            }
            await File.AppendAllTextAsync(file, content).ConfigureAwait(false);
        }

        public async Task<string> Read(string id, string version)
        {
            var file = Path.Combine(_rootPath, id.ToLower());
            if (File.Exists(file))
            {
                var lines = await File.ReadAllLinesAsync(file).ConfigureAwait(false);
                foreach (var line in lines)
                {
                    var parts = line.Split("||");
                    //* special case where as enduser you can now dictate what license you wanto to use for a given library
                    // This is good for internal libraries or ones that are not standard. You an set it once and the going forward
                    // it will always use that value.
                    if (parts[0] == version || parts[0] == "*")  
                    {
                        return parts[1];
                    }
                }
            }
            return null;
        }
    }

    /// <summary>
    /// LicenseMemoryCacheRepository is a memory based cache.
    /// </summary>
    public class LicenseMemoryCacheRepository : ILicenseCacheRepository
    {
        private readonly ConcurrentDictionary<string, string> _cache = new ConcurrentDictionary<string, string>();
        
        public Task Write(string id, string version, string licenseId)
        {
            var key = $"{id}/{version}";
            _cache.TryAdd(key, licenseId);
            return Task.CompletedTask;
        }

        public Task<string> Read(string id, string version)
        {
            var key = $"{id}/{version}";
            if (_cache.TryGetValue(key, out string licenseId))
            {
                return Task.FromResult(licenseId);
            }
            return Task.FromResult<string>(null);
        }
    }
}
