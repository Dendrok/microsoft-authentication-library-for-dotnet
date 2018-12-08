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
using System.Threading.Tasks;
using Microsoft.Identity.Client.Config;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.Exceptions;

namespace Microsoft.Identity.Client.Instance
{
    internal abstract class Authority
    {
        internal static readonly HashSet<string> TenantlessTenantNames = new HashSet<string>(
            new[]
            {
                "common",
                "organizations",
                "consumers"
            });

        protected Authority(IServiceBundle serviceBundle, AuthorityInfo authorityInfo)
        {
            ServiceBundle = serviceBundle;
            AuthorityInfo = authorityInfo;

            // TODO: it's odd to have multiple sources of truth here, but some authority objects overwrite the
            // canonical authority.  So should this be read/write here or should we enable it in AuthorityInfo?
            CanonicalAuthority = authorityInfo.CanonicalAuthority;
        }

        public AuthorityInfo AuthorityInfo { get; }

        public AuthorityType AuthorityType => AuthorityInfo.AuthorityType;

        public string CanonicalAuthority
        {
            get => AuthorityInfo.CanonicalAuthority;
            set => AuthorityInfo.CanonicalAuthority = value;
        }

        public string Host => AuthorityInfo.Host;

        protected IServiceBundle ServiceBundle { get; }

        public static Authority CreateAuthorityWithOverride(IServiceBundle serviceBundle, AuthorityInfo authorityInfo)
        {
            switch (serviceBundle.Config.DefaultAuthorityInfo.AuthorityType)
            {
            case AuthorityType.Adfs:
                throw MsalExceptionFactory.GetClientException(
                    CoreErrorCodes.InvalidAuthorityType,
                    "ADFS is not a supported authority");

            case AuthorityType.B2C:
                return new B2CAuthority(serviceBundle, authorityInfo);

            case AuthorityType.Aad:
                return new AadAuthority(serviceBundle, authorityInfo);

            default:
                throw MsalExceptionFactory.GetClientException(
                    CoreErrorCodes.InvalidAuthorityType,
                    "Unsupported authority type");
            }
        }

        public static Authority CreateAuthority(IServiceBundle serviceBundle, string authority, bool validateAuthority)
        {
            return CreateAuthorityWithOverride(
                serviceBundle,
                AuthorityInfo.FromAuthorityUri(authority, validateAuthority, false));
        }

        public static Authority CreateAuthority(IServiceBundle serviceBundle)
        {
            return CreateAuthorityWithOverride(serviceBundle, serviceBundle.Config.DefaultAuthorityInfo);
        }

        internal virtual async Task UpdateCanonicalAuthorityAsync(
            RequestContext requestContext)
        {
            await Task.FromResult(0).ConfigureAwait(false);
        }

        internal static string GetFirstPathSegment(string authority)
        {
            return new Uri(authority).Segments[1].TrimEnd('/');
        }

        internal static AuthorityType GetAuthorityType(string authority)
        {
            string firstPathSegment = GetFirstPathSegment(authority);

            if (string.Equals(firstPathSegment, "adfs", StringComparison.OrdinalIgnoreCase))
            {
                return AuthorityType.Adfs;
            }
            else if (string.Equals(firstPathSegment, B2CAuthority.Prefix, StringComparison.OrdinalIgnoreCase))
            {
                return AuthorityType.B2C;
            }
            else
            {
                return AuthorityType.Aad;
            }
        }

        internal abstract string GetTenantId();
        internal abstract void UpdateTenantId(string tenantId);

        internal static string CreateAuthorityUriWithHost(string authority, string host)
        {
            var uriBuilder = new UriBuilder(authority)
            {
                Host = host
            };

            return uriBuilder.Uri.AbsoluteUri;
        }
    }
}