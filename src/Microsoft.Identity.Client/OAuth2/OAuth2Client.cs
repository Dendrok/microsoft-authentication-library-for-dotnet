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
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.Identity.Client.Core;
using Microsoft.Identity.Client.Exceptions;
using Microsoft.Identity.Client.Http;
using Microsoft.Identity.Client.Instance;
using Microsoft.Identity.Client.TelemetryCore;
using Microsoft.Identity.Client.Utils;

namespace Microsoft.Identity.Client.OAuth2
{
    internal class OAuth2Client
    {
        private readonly Dictionary<string, string> _bodyParameters = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>(MsalIdHelper.GetMsalIdParameters());
        private readonly Dictionary<string, string> _queryParameters = new Dictionary<string, string>();
        private readonly IHttpManager _httpManager;
        private readonly ITelemetryManager _telemetryManager;

        public OAuth2Client(IHttpManager httpManager, ITelemetryManager telemetryManager)
        {
            _httpManager = httpManager ?? throw new ArgumentNullException(nameof(httpManager));
            _telemetryManager = telemetryManager ?? throw new ArgumentNullException(nameof(telemetryManager));
        }

        public void AddQueryParameter(string key, string value)
        {
            _queryParameters[key] = value;
        }

        public void AddBodyParameter(string key, string value)
        {
            _bodyParameters[key] = value;
        }

        public async Task<InstanceDiscoveryResponse> DiscoverAadInstanceAsync(Uri endPoint, RequestContext requestContext)
        {
            return await ExecuteRequestAsync<InstanceDiscoveryResponse>(endPoint, HttpMethod.Get, requestContext)
                       .ConfigureAwait(false);
        }

        public async Task<MsalTokenResponse> GetTokenAsync(Uri endPoint, RequestContext requestContext)
        {
            return await ExecuteRequestAsync<MsalTokenResponse>(endPoint, HttpMethod.Post, requestContext).ConfigureAwait(false);
        }

        internal async Task<T> ExecuteRequestAsync<T>(Uri endPoint, HttpMethod method, RequestContext requestContext)
        {
            bool addCorrelationId =
                requestContext != null && !string.IsNullOrEmpty(requestContext.Logger.CorrelationId.ToString());
            if (addCorrelationId)
            {
                _headers.Add(OAuth2Header.CorrelationId, requestContext.Logger.CorrelationId.ToString());
                _headers.Add(OAuth2Header.RequestCorrelationIdInResponse, "true");
            }

            HttpResponse response = null;
            var endpointUri = CreateFullEndpointUri(endPoint);
            var httpEvent = new HttpEvent()
            {
                HttpPath = endpointUri,
                QueryParams = endpointUri.Query
            };

            using (_telemetryManager.CreateTelemetryHelper(requestContext.TelemetryRequestId, requestContext.ClientId, httpEvent))
            {
                if (method == HttpMethod.Post)
                {
                    response = await _httpManager.SendPostAsync(endpointUri, _headers, _bodyParameters, requestContext)
                                                .ConfigureAwait(false);
                }
                else
                {
                    response = await _httpManager.SendGetAsync(endpointUri, _headers, requestContext).ConfigureAwait(false);
                }

                httpEvent.HttpResponseStatus = (int)response.StatusCode;
                httpEvent.UserAgent = response.UserAgent;
                httpEvent.HttpMethod = method.Method;

                IDictionary<string, string> headersAsDictionary = response.HeadersAsDictionary;
                if(headersAsDictionary.ContainsKey("x-ms-request-id") &&
                    headersAsDictionary["x-ms-request-id"] != null)
                {
                    httpEvent.RequestIdHeader = headersAsDictionary["x-ms-request-id"];
                }

                if(headersAsDictionary.ContainsKey("x-ms-clitelem") && 
                    headersAsDictionary["x-ms-clitelem"] != null)
                {
                    XmsCliTelemInfo xmsCliTeleminfo = new XmsCliTelemInfoParser().ParseXMsTelemHeader(headersAsDictionary["x-ms-clitelem"], requestContext);
                    if (xmsCliTeleminfo != null)
                    {
                        httpEvent.TokenAge = xmsCliTeleminfo.TokenAge;
                        httpEvent.SpeInfo = xmsCliTeleminfo.SpeInfo;
                        httpEvent.ServerErrorCode = xmsCliTeleminfo.ServerErrorCode;
                        httpEvent.ServerSubErrorCode = xmsCliTeleminfo.ServerSubErrorCode;
                    }
                }

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    try
                    {
                        httpEvent.OauthErrorCode = JsonHelper.DeserializeFromJson<MsalTokenResponse>(response.Body).Error;
                    }
                    catch (SerializationException) // in the rare case we get an error response we cannot deserialize
                    {
                        throw MsalExceptionFactory.GetServiceException(
                            CoreErrorCodes.NonParsableOAuthError,
                            CoreErrorMessages.NonParsableOAuthError,
                            response);
                    }
                }
            }

            return CreateResponse<T>(response, requestContext, addCorrelationId);
        }

        public static T CreateResponse<T>(HttpResponse response, RequestContext requestContext, bool addCorrelationId)
        {
            if (response.StatusCode != HttpStatusCode.OK)
            {
                CreateErrorResponse(response, requestContext);
            }

            if (addCorrelationId)
            {
                VerifyCorrelationIdHeaderInResponse(response.HeadersAsDictionary, requestContext);
            }

            return JsonHelper.DeserializeFromJson<T>(response.Body);
        }

        public static void CreateErrorResponse(HttpResponse response, RequestContext requestContext)
        {
            bool shouldLogAsError = true;

            Exception serviceEx;

            try
            {
                var msalTokenResponse = JsonHelper.DeserializeFromJson<MsalTokenResponse>(response.Body);

                if (CoreErrorCodes.InvalidGrantError.Equals(msalTokenResponse.Error, StringComparison.OrdinalIgnoreCase))
                {
                    throw MsalExceptionFactory.GetUiRequiredException(
                        CoreErrorCodes.InvalidGrantError,
                        msalTokenResponse.ErrorDescription,
                        null,
                        ExceptionDetail.FromHttpResponse(response));
                }

                serviceEx = MsalExceptionFactory.GetServiceException(
                    msalTokenResponse.Error,
                    msalTokenResponse.ErrorDescription,
                    response);

                // For device code flow, AuthorizationPending can occur a lot while waiting
                // for the user to auth via browser and this causes a lot of error noise in the logs.
                // So suppress this particular case to an Info so we still see the data but don't 
                // log it as an error since it's expected behavior while waiting for the user.
                if (string.Compare(msalTokenResponse.Error, OAuth2Error.AuthorizationPending,
                        StringComparison.OrdinalIgnoreCase) == 0)
                {
                    shouldLogAsError = false;
                }

            }
            catch (SerializationException ex)
            {
                serviceEx = MsalExceptionFactory.GetClientException(CoreErrorCodes.UnknownError, response.Body, ex);
            }

            if (shouldLogAsError)
            {
                requestContext.Logger.ErrorPii(serviceEx);
            }
            else
            {
                requestContext.Logger.InfoPii(serviceEx);
            }

            throw serviceEx;
        }

        private Uri CreateFullEndpointUri(Uri endPoint)
        {
            var endpointUri = new UriBuilder(endPoint);
            string extraQp = _queryParameters.ToQueryParameter();
            endpointUri.AppendQueryParameters(extraQp);

            return endpointUri.Uri;
        }

        private static void VerifyCorrelationIdHeaderInResponse(
            IDictionary<string, string> headers,
            RequestContext requestContext)
        {
            foreach (string responseHeaderKey in headers.Keys)
            {
                string trimmedKey = responseHeaderKey.Trim();
                if (string.Compare(trimmedKey, OAuth2Header.CorrelationId, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    string correlationIdHeader = headers[trimmedKey].Trim();
                    if (string.Compare(
                            correlationIdHeader,
                            requestContext.Logger.CorrelationId.ToString(),
                            StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        requestContext.Logger.WarningPii(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Returned correlation id '{0}' does not match the sent correlation id '{1}'",
                                correlationIdHeader,
                                requestContext.Logger.CorrelationId),
                            "Returned correlation id does not match the sent correlation id");
                    }

                    break;
                }
            }
        }
    }
}