﻿// ------------------------------------------------------------------------------
// 
// Copyright (c) Microsoft Corporation.
// All rights reserved.
// 
// This code is licensed under the MIT License.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files(the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions :
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// 
// ------------------------------------------------------------------------------

using Microsoft.Identity.Client.Config;
using Microsoft.Identity.Client.Http;
using Microsoft.Identity.Client.Instance;
using Microsoft.Identity.Client.TelemetryCore;
using Microsoft.Identity.Client.WsTrust;

namespace Microsoft.Identity.Client.Core
{
    internal class ServiceBundle : IServiceBundle
    {
        internal ServiceBundle(
            ApplicationConfiguration config,
            //IValidatedAuthoritiesCache validatedAuthoritiesCache = null,
            //IAadInstanceDiscovery aadInstanceDiscovery = null,
            //IWsTrustWebRequestManager wsTrustWebRequestManager = null,
            bool shouldClearCaches = false)
        {
            Config = config;
            HttpManager = config.HttpManager ?? new HttpManager(config.HttpClientFactory);
            TelemetryManager = new TelemetryManager(config.TelemetryReceiver);
            ValidatedAuthoritiesCache = new ValidatedAuthoritiesCache(shouldClearCaches);
            AadInstanceDiscovery = new AadInstanceDiscovery(HttpManager, TelemetryManager, shouldClearCaches);
            WsTrustWebRequestManager = new WsTrustWebRequestManager(HttpManager);
            PlatformProxy = PlatformProxyFactory.GetPlatformProxy();
        }

        /// <inheritdoc />
        public IHttpManager HttpManager { get; }

        /// <inheritdoc />
        public ITelemetryManager TelemetryManager { get; }

        /// <inheritdoc />
        public IValidatedAuthoritiesCache ValidatedAuthoritiesCache { get; }

        /// <inheritdoc />
        public IAadInstanceDiscovery AadInstanceDiscovery { get; }

        /// <inheritdoc />
        public IWsTrustWebRequestManager WsTrustWebRequestManager { get; }

        /// <inheritdoc />
        public IPlatformProxy PlatformProxy { get; }

        /// <inheritdoc />
        public IApplicationConfiguration Config { get; }

        public static ServiceBundle CreateDefault(ApplicationConfiguration config)
        {
            return new ServiceBundle(config);
        }

        public static ServiceBundle CreateWithClearCaches(ApplicationConfiguration config)
        {
            return new ServiceBundle(config, shouldClearCaches: true);
        }
    }
}