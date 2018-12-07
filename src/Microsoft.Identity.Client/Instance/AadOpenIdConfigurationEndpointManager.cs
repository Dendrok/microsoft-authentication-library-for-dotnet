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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Identity.Client.Config;
using Microsoft.Identity.Client.Core;

namespace Microsoft.Identity.Client.Instance
{
    internal class AadOpenIdConfigurationEndpointManager : IOpenIdConfigurationEndpointManager
    {
        private readonly IServiceBundle _serviceBundle;

        public AadOpenIdConfigurationEndpointManager(IServiceBundle serviceBundle)
        {
            _serviceBundle = serviceBundle;
        }

        /// <inheritdoc />
        public async Task<string> GetOpenIdConfigurationEndpointAsync(
            AuthorityInfo authorityInfo,
            string userPrincipalName,
            RequestContext requestContext)
        {
            var authorityUri = new Uri(authorityInfo.CanonicalAuthority);

            if (authorityInfo.ValidateAuthority && !AadAuthority.IsInTrustedHostList(authorityUri.Host))
            {
                var discoveryResponse = await _serviceBundle.AadInstanceDiscovery.DoInstanceDiscoveryAndCacheAsync(
                                            authorityUri,
                                            true,
                                            requestContext).ConfigureAwait(false);

                return discoveryResponse.TenantDiscoveryEndpoint;
            }

            return authorityInfo.CanonicalAuthority + "v2.0/.well-known/openid-configuration";
        }
    }
}