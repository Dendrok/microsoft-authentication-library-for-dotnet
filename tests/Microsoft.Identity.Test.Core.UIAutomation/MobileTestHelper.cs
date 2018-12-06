//------------------------------------------------------------------------------
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
//------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Identity.Test.LabInfrastructure;
using NUnit.Framework;
using Xamarin.UITest.Queries;

namespace Microsoft.Identity.Test.UIAutomation.infrastructure
{
    /// <summary>
    /// Contains the core test functionality that will be used by Android and iOS tests
    /// </summary>
    public class MobileTestHelper
    {
        /// <summary>
        /// Runs through the standard acquire token flow, using the login prompt behavior. The ui behavior of "login" is used by default.
        /// </summary>
        /// <param name="controller">The test framework that will execute the test interaction</param>
        public void AcquireTokenInteractiveTestHelper(
            ITestController controller,
            LabResponse labResponse,
            string promptBehavior = CoreUiTestConstants.UIBehaviorLogin)
        {
            AcquireTokenInteractiveHelper(controller, labResponse, promptBehavior);
            VerifyResult(controller);
        }

        /// <summary>
        /// Runs through the standard acquire token silent flow
        /// </summary>
        /// <param name="controller">The test framework that will execute the test interaction</param>
        public void AcquireTokenSilentTestHelper(ITestController controller, LabResponse labResponse)
        {
            //acquire token for 1st resource
            AcquireTokenInteractiveHelper(controller, labResponse, CoreUiTestConstants.UIBehaviorLogin);
            VerifyResult(controller);

            //acquire token for 2nd resource with refresh token
            SetInputData(controller, labResponse.AppId, CoreUiTestConstants.DefaultScope, CoreUiTestConstants.UIBehaviorLogin);
            controller.Tap(CoreUiTestConstants.AcquireTokenSilentID);
            VerifyResult(controller);
        }

        private void AcquireTokenInteractiveHelper(
            ITestController controller,
            LabResponse labResponse,
            string promptBehavior)
        {
            PrepareForAuthentication(controller);
            SetInputData(controller, labResponse.AppId, CoreUiTestConstants.DefaultScope, promptBehavior);
            PerformSignInFlow(controller, labResponse.User);

            // on consent, also hit the accept button
            if (promptBehavior == CoreUiTestConstants.UIBehaviorConsent)
            {
                AppWebResult consentHeader = controller.WaitForWebElementByCssId("consentHeader").FirstOrDefault();
                Assert.IsNotNull(consentHeader);
                Assert.IsTrue(consentHeader.TextContent.Contains("Permissions requested"));

                controller.Tap(CoreUiTestConstants.WebSubmitID, XamarinSelector.ByHtmlIdAttribute);
            }
        }

        private void PrepareForAuthentication(ITestController controller)
        {
            //Clear Cache
            controller.Tap(CoreUiTestConstants.CachePageID);
            controller.Tap(CoreUiTestConstants.ClearCacheID);
        }

        private void SetInputData(
            ITestController controller,
            string clientId,
            string scopes,
            string uiBehavior)
        {
            controller.Tap(CoreUiTestConstants.SettingsPageID);

            //Enter ClientID
            controller.EnterText(CoreUiTestConstants.ClientIdEntryID, clientId, XamarinSelector.ByAutomationId);
            controller.Tap(CoreUiTestConstants.SaveID);

            //Enter Scopes
            controller.Tap(CoreUiTestConstants.AcquirePageID);
            controller.EnterText(CoreUiTestConstants.ScopesEntryID, scopes, XamarinSelector.ByAutomationId);

            SetUiBehavior(controller, uiBehavior);
        }

        public void SetUiBehavior(ITestController controller, string promptBehavior)
        {
            // Enter Prompt Behavior
            controller.Tap(CoreUiTestConstants.UiBehaviorPickerID);
            controller.Tap(promptBehavior);
            controller.Tap(CoreUiTestConstants.AcquirePageID);
        }

        private void ValidateUiBehaviorString(string uiBehavior)
        {
            var okList = new[] {
                CoreUiTestConstants.UIBehaviorConsent,
                CoreUiTestConstants.UIBehaviorLogin,
                CoreUiTestConstants.UIBehaviorSelectAccount };

            bool isInList = okList.Any(item => string.Equals(item, uiBehavior, StringComparison.InvariantCulture));

            if (!isInList)
            {
                throw new InvalidOperationException("Test Setup Error: invalid uiBehavior " + uiBehavior);
            }
        }

        /// <summary>
        /// Runs through the B2C acquire token flow with local account
        /// </summary>
        public void B2CLocalAccountAcquireTokenInteractiveTestHelper(ITestController controller, LabResponse labResponse, bool isB2CLoginAuthority)
        {
            PerformB2CSignInFlow(controller, labResponse.User, B2CIdentityProvider.Local, isB2CLoginAuthority);
        }

        /// <summary>
        /// Runs through the B2C acquire token flow with Facebook Provider
        /// </summary>
        public void B2CFacebookProviderAcquireTokenInteractiveTestHelper(ITestController controller, LabResponse labResponse, bool isB2CLoginAuthority)
        {
            PerformB2CSignInFlow(controller, labResponse.User, B2CIdentityProvider.Facebook, isB2CLoginAuthority);
        }

        /// <summary>
        /// Runs through the B2C acquire token flow with Facebook Provider
        /// and Edit Policy authority
        /// </summary>
        public void B2CFacebookProviderEditPolicyAcquireTokenInteractiveTestHelper(ITestController controller)
        {
            PerformB2CSignInEditProfileFlow(controller, B2CIdentityProvider.Facebook);
        }

        /// <summary>
        /// Runs through the B2C acquire token flow with Google Provider
        /// </summary>
        public void B2CGoogleProviderAcquireTokenInteractiveTestHelper(ITestController controller, LabResponse labResponse, bool isB2CLoginAuthority)
        {
            PerformB2CSignInFlow(controller, labResponse.User, B2CIdentityProvider.Google, isB2CLoginAuthority);
        }

        private void SetB2CAuthority(ITestController controller, bool isB2CLoginAuthority)
        {
            PrepareForAuthentication(controller);
            controller.Tap(CoreUiTestConstants.SettingsPageID);

            if (isB2CLoginAuthority)
            {
                SetB2CInputDataForB2CloginAuthority(controller);
            }
            else
            {
                SetB2CInputData(controller);
            }
        }

        /// <summary>
        /// Runs through the B2C acquire token silent flow with local account
        /// </summary>
        /// <param name="controller">The test framework that will execute the test interaction</param>
        public void B2CLocalAccountAcquireTokenSilentTest(ITestController controller, LabResponse labResponse, bool isB2CLoginAuthority)
        {
            //acquire token for 1st resource   
            B2CLocalAccountAcquireTokenInteractiveTestHelper(controller, labResponse, isB2CLoginAuthority);

            B2CSilentFlowHelper(controller);
        }

        /// <summary>
        /// Runs through the B2C acquire token silent flow with Facebook identity provider
        /// </summary>
        /// <param name="controller">The test framework that will execute the test interaction</param>
        public void B2CFacebookProviderAcquireTokenSilentTest(ITestController controller, LabResponse labResponse, bool isB2CLoginAuthority)
        {
            //acquire token for 1st resource   
            B2CFacebookProviderAcquireTokenInteractiveTestHelper(controller, labResponse, isB2CLoginAuthority);

            B2CSilentFlowHelper(controller);
        }

        /// <summary>
        /// Runs through the B2C acquire token silent flow with Google identity provider
        /// </summary>
        /// <param name="controller">The test framework that will execute the test interaction</param>
        public void B2CGoogleProviderAcquireTokenSilentTest(ITestController controller, LabResponse labResponse, bool isB2CLoginAuthority)
        {
            //acquire token for 1st resource   
            B2CGoogleProviderAcquireTokenInteractiveTestHelper(controller, labResponse, isB2CLoginAuthority);

            B2CSilentFlowHelper(controller);
        }

        private void B2CSilentFlowHelper(ITestController controller)
        {
            //verify results of AT call
            VerifyResult(controller);

            //select user
            controller.Tap(CoreUiTestConstants.SelectUser);
            //b2c does not return userinfo in token response
            controller.Tap(CoreUiTestConstants.UserMissingFromResponse);
            //acquire token silent with selected user
            controller.Tap(CoreUiTestConstants.AcquireTokenSilentID);
            VerifyResult(controller);
        }

        private void SetB2CInputData(ITestController controller)
        {
            // Select login.microsoftonline.com for authority
            SetAuthority(controller, CoreUiTestConstants.MicrosoftOnlineAuthority);
        }

        private void SetB2CInputDataForB2CloginAuthority(ITestController controller)
        {
            // Select b2clogin.com for authority
            SetAuthority(controller, CoreUiTestConstants.B2CLoginAuthority);
        }

        public void SetB2CInputDataForEditProfileAuthority(ITestController controller)
        {
            controller.Tap(CoreUiTestConstants.SettingsPageID);
            // Select Edit Profile for Authority
            SetAuthority(controller, CoreUiTestConstants.B2CEditProfileAuthority);
        }

        public void SetAuthority(ITestController controller, string authority)
        {
            // Select authority
            controller.Tap(CoreUiTestConstants.AuthorityPickerID);
            controller.Tap(authority);
        }

        public void PerformB2CLocalAccountSignInFlow(ITestController controller, LabUser user, UserInformationFieldIds userInformationFieldIds)
        {
            controller.EnterText(CoreUiTestConstants.WebUPNB2CLocalInputID, 20, user.Upn, XamarinSelector.ByHtmlIdAttribute);

            controller.EnterText(userInformationFieldIds.PasswordInputId, LabUserHelper.GetUserPassword(user), XamarinSelector.ByHtmlIdAttribute);

            controller.Tap(userInformationFieldIds.SignInButtonId, XamarinSelector.ByHtmlIdAttribute);
        }

        public void PerformB2CFacebookProviderSignInFlow(ITestController controller, LabUser user, UserInformationFieldIds userInformationFieldIds)
        {
            controller.Tap(CoreUiTestConstants.FacebookAccountID, XamarinSelector.ByHtmlIdAttribute);

            controller.EnterText(CoreUiTestConstants.WebUPNB2CFacebookInputID, 20, user.Upn, XamarinSelector.ByHtmlIdAttribute);

            controller.EnterText(userInformationFieldIds.PasswordInputId, LabUserHelper.GetUserPassword(user), XamarinSelector.ByHtmlIdAttribute);

            controller.Tap(userInformationFieldIds.SignInButtonId, XamarinSelector.ByHtmlIdAttribute);
        }

        public void PerformB2CGoogleProviderSignInFlow(ITestController controller, LabUser user, UserInformationFieldIds userInformationFieldIds)
        {
            controller.Tap(CoreUiTestConstants.GoogleAccountID, XamarinSelector.ByHtmlIdAttribute);

            controller.EnterText(CoreUiTestConstants.WebUPNB2CGoogleInputID, 20, user.Upn, XamarinSelector.ByHtmlIdAttribute);

            controller.Tap(CoreUiTestConstants.B2CGoogleNextID, XamarinSelector.ByHtmlIdAttribute);

            controller.EnterText(userInformationFieldIds.PasswordInputId, LabUserHelper.GetUserPassword(user), XamarinSelector.ByHtmlIdAttribute);

            controller.Tap(userInformationFieldIds.SignInButtonId, XamarinSelector.ByHtmlIdAttribute);
        }

        public void PerformB2CSignInFlow(ITestController controller, LabUser user, B2CIdentityProvider b2CIdentityProvider, bool isB2CLoginAuthority)
        {
            SetB2CAuthority(controller, true);

            UserInformationFieldIds userInformationFieldIds = DetermineUserInformationFieldIds(user);

            controller.Tap(CoreUiTestConstants.AcquirePageID);

            //Acquire token flow
            controller.Tap(CoreUiTestConstants.AcquireTokenID);

            switch (b2CIdentityProvider)
            {
                case B2CIdentityProvider.Local:
                    PerformB2CLocalAccountSignInFlow(controller, user, userInformationFieldIds);
                    break;
                case B2CIdentityProvider.Google:
                    PerformB2CGoogleProviderSignInFlow(controller, user, userInformationFieldIds);
                    break;

                case B2CIdentityProvider.Facebook:
                    PerformB2CFacebookProviderSignInFlow(controller, user, userInformationFieldIds);
                    break;
                default:
                    throw new InvalidOperationException("B2CIdentityProvider unknown");
            }
            VerifyResult(controller);
        }

        public void PerformB2CSignInEditProfileFlow(ITestController controller, B2CIdentityProvider b2CIdentityProvider)
        {
            SetB2CInputDataForEditProfileAuthority(controller);

            controller.Tap(CoreUiTestConstants.AcquirePageID);

            SetUiBehavior(controller, CoreUiTestConstants.UIBehaviorNoPrompt);

            //Acquire token flow
            controller.Tap(CoreUiTestConstants.AcquireTokenID);

            controller.Tap(CoreUiTestConstants.B2CEditProfileContinueID, XamarinSelector.ByHtmlIdAttribute);

            VerifyResult(controller);
        }

        public void PromptBehaviorTestHelperWithConsent(ITestController controller, LabResponse labResponse)
        {
            // 1. Acquire token with uiBehavior set to consent
            AcquireTokenInteractiveTestHelper(
                controller,
                labResponse,
                CoreUiTestConstants.UIBehaviorConsent);

            // 2. Switch ui behavior to "select account"
            SetUiBehavior(controller, CoreUiTestConstants.UIBehaviorSelectAccount);

            // 3. Hit Acquire Token directly since we are not changing any other setting
            controller.Tap(CoreUiTestConstants.AcquireTokenID);

            // 4. The web UI should display all users, so click on the current user
            controller.Tap(labResponse.User.Upn, XamarinSelector.ByHtmlValue);

            // 5. Validate token again
            VerifyResult(controller);
        }

        public void PerformSignInFlow(ITestController controller, LabUser user)
        {
            UserInformationFieldIds userInformationFieldIds = DetermineUserInformationFieldIds(user);

            //Acquire token flow
            try
            {
                controller.Tap(CoreUiTestConstants.AcquireTokenID);
            }
            catch (Exception ex)
            {
                //i0116 = UPN text field on AAD sign in endpoint
                controller.EnterText(CoreUiTestConstants.WebUPNInputID, 20, user.Upn, XamarinSelector.ByHtmlIdAttribute);
                //idSIButton9 = Sign in button
                controller.Tap(CoreUiTestConstants.WebSubmitID, XamarinSelector.ByHtmlIdAttribute);
                //i0118 = password text field
                controller.EnterText(userInformationFieldIds.PasswordInputId, LabUserHelper.GetUserPassword(user), XamarinSelector.ByHtmlIdAttribute);
                controller.Tap(userInformationFieldIds.SignInButtonId, XamarinSelector.ByHtmlIdAttribute);
                return;
            }
            //i0116 = UPN text field on AAD sign in endpoint
            controller.EnterText(CoreUiTestConstants.WebUPNInputID, 20, user.Upn, XamarinSelector.ByHtmlIdAttribute);
            //idSIButton9 = Sign in button
            controller.Tap(CoreUiTestConstants.WebSubmitID, XamarinSelector.ByHtmlIdAttribute);
            //i0118 = password text field
            controller.EnterText(userInformationFieldIds.PasswordInputId, LabUserHelper.GetUserPassword(user), XamarinSelector.ByHtmlIdAttribute);
            controller.Tap(userInformationFieldIds.SignInButtonId, XamarinSelector.ByHtmlIdAttribute);
        }

        public static void PerformSignInFlowWithoutUI(ITestController controller)
        {
            //Acquire token flow
            controller.Tap(CoreUiTestConstants.AcquireTokenID);
        }

        public static UserInformationFieldIds DetermineUserInformationFieldIds(LabUser user)
        {
            UserInformationFieldIds userInformationFieldIds = new UserInformationFieldIds();
            userInformationFieldIds.DetermineFieldIds(user);
            return userInformationFieldIds;
        }

        public void VerifyResult(ITestController controller)
        {
            RetryVerificationHelper(() =>
            {
                //Test results are put into a label that is checked for messages
                var result = controller.GetText(CoreUiTestConstants.TestResultID);
                if (result.Contains(CoreUiTestConstants.TestResultSuccessfulMessage))
                {
                    return;
                }
                else if (result.Contains(CoreUiTestConstants.TestResultFailureMessage))
                {
                    throw new ResultVerificationFailureException(VerificationError.ResultIndicatesFailure);
                }
                else
                {
                    throw new ResultVerificationFailureException(VerificationError.ResultNotFound);
                }
            });
        }

        private static void RetryVerificationHelper(Action verification)
        {
            //There may be a delay in the amount of time it takes for an authentication request to complete.
            //Thus this method will check the result once a second for 20 seconds.
            var attempts = 0;
            do
            {
                try
                {
                    attempts++;
                    verification();
                    break;
                }
                catch (ResultVerificationFailureException ex)
                {
                    if (attempts == CoreUiTestConstants.MaximumResultCheckRetryAttempts)
                    {
                        Assert.Fail("Could not Verify test result");
                    }

                    switch (ex.Error)
                    {
                        case VerificationError.ResultIndicatesFailure:
                            Assert.Fail("Test result indicates failure");
                            break;
                        case VerificationError.ResultNotFound:
                            Task.Delay(CoreUiTestConstants.ResultCheckPolliInterval).Wait();
                            break;
                        default:
                            throw;
                    }
                }
            } while (true);
        }
    }
}
