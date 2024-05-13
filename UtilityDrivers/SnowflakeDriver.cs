// Copyright (c) 2021-2022 Snowflake Inc. All rights reserved.

// Licensed under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at

//   http://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.

using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web;
using Newtonsoft.Json.Linq;

namespace Snowflake.Powershell
{
    public class SnowflakeDriver
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static Logger loggerConsole = LogManager.GetLogger("Snowflake.Powershell.Console");
        private static Logger loggerDiagnosticTest = LogManager.GetLogger("Snowflake.Powershell.DiagnosticTest");
        private static Logger loggerExtensiveDiagnosticTest = LogManager.GetLogger("Snowflake.Powershell.ExtensiveDiagnosticTest");

        #region Snowsight Client Metadata

        public static Tuple<string, CookieContainer, HttpStatusCode> GetBootstrapCookie(string AppUrl,
            CookieContainer cookies)
        {
            return apiGET(
                AppUrl,
                "bootstrap",
                cookies: cookies
            );
        }

        /// <summary>
        /// Validates that the account identifier is correct, this can be what is supported by
        /// Snowflake, see docs.snowflake.com/en/user-guide/admin-account-identifier.
        /// </summary>
        /// <param name="mainAppUrl">Usually https://app.snowflake.com.</param>
        /// <param name="acco">Cookie co</param>
        /// <returns>String of the response, cookie container with latest cookies, and the status code.</returns>
        public static Tuple<string, CookieContainer, HttpStatusCode> GetAccountAppEndpoints(string mainAppUrl, string accountIdentifier, CookieContainer cookies)
        {
            return apiGET(
                mainAppUrl,
                String.Format("v0/validate-snowflake-url?url={0}", accountIdentifier),
                cookies: cookies
            );
        }

        /// <summary>
        /// When a user logs in via the Snowsight UI at https://app.snowflake.com, Snowflake redirects them to the `start-oauth/snowflake` endpoint:
        ///
        /// `https://apps-api.c1.us-east-999.aws.app.snowflake.com/start-oauth/snowflake?accountUrl=https%3A%2F%2Faccount12345us-east-999.snowflakecomputing.com&&state=%7B%22csrf%22%3A%22abcdefab%22%2C%22url%22%3A%22https%3A%2F%2Faccount12345.us-east-999.snowflakecomputing.com%22%2C%22windowId%22%3A%2200000000-0000-0000-0000-000000000000%22%2C%22browserUrl%22%3A%22https%3A%2F%2Fapp.snowflake.com%2F%22%7D`
        ///
        /// > Note that the `&&state` is not a typo, it is what Snowflake sends, so we send the same.
        ///
        /// This string is URL-encoded; its decoded form appears as follows:
        ///
        /// `https://apps-api.c1.us-east-999.aws.app.snowflake.com/start-oauth/snowflake?accountUrl=https://account12345us-east-999.snowflakecomputing.com&&state={"csrf":"abcdefab","url":"https://account12345.us-east-999.snowflakecomputing.com","windowId":"00000000-0000-0000-0000-000000000000","browserUrl":"https://app.snowflake.com/"}`
        ///
        /// Snowflake expects the following keys in the state object:
        ///
        /// 1. csrf - The csrf token from the earlier step
        /// 2. url - The URL of the user's Snowflake instance (https://account12345.us-east-999.snowflakecomputing.com)
        /// 3. windowId - This parameter is not needed, this parameter is the unique window ID of the user's web browser session, used to mitigate forgery risks.
        /// 4. browserUrl - https://app.snowflake.com
        /// </summary>
        /// <param name="mainAppUrl">Usually https://app.snowflake.com.</param>
        /// <param name="appServerUrl">Usually https://apps-api.c1.REGION.aws.app.snowflake.com</param>
        /// <param name="accountUrl">Usually https://kt61312.ap-southeast-2.snowflakecomputing.com</param>
        /// <param name="csrf">csrf from bootstrap</param>
        /// <param name="cookies">Cookie container to use for requests</param>
        /// <returns>String of the response, cookie container with latest cookies, and the status code.</returns>
        public static Tuple<string, CookieContainer, HttpStatusCode> OAuth_Start_GetSnowSightClientIDInDeployment(string mainAppUrl, string appServerUrl, string accountUrl, string csrf, CookieContainer cookies)
        {
            // ensure mainAppURL ends with a /
            if (mainAppUrl.EndsWith("/") == false)
            {
                mainAppUrl = mainAppUrl + "/";
            }

            string stateParam = String.Format("{{\"csrf\":\"{0}\",\"url\":\"{1}\",\"windowId\":\"{2}\",\"browserUrl\":\"{3}\"}}", csrf, accountUrl, Guid.NewGuid(), mainAppUrl);

            return apiGET(
                appServerUrl,
                String.Format("start-oauth/snowflake?accountUrl={0}&&state={1}", HttpUtility.UrlEncode(accountUrl), HttpUtility.UrlEncode(stateParam)),
                cookies: cookies
            );
        }

        #endregion

        #region Snowsight Authentication

        public static string OAuth_Authenticate_GetMasterTokenFromCredentials(AppUserContext appUserContext, string password)
        {
            string state = string.Format("\"{{\\\"csrf\\\":\\\"{0}\\\",\\\"url\\\":\\\"{1}\\\",\\\"browserUrl\\\":\\\"https://app.snowflake.com/\\\",\\\"originator\\\":\\\"{2}\\\",\\\"oauthNonce\\\":\\\"{3}\\\"}}", appUserContext.CSRFToken, appUserContext.AccountUrl, appUserContext.AuthOriginator, appUserContext.AuthOAuthNonce);
            string requestBody =
$@"{{
  ""data"": {{
    ""ACCOUNT_NAME"": ""{appUserContext.AccountName.ToUpper()}"",
    ""LOGIN_NAME"": ""{appUserContext.UserName}"",
    ""clientId"": ""{appUserContext.ClientID}"",
    ""redirectUri"": ""{appUserContext.AuthRedirectUri}"",
    ""responseType"": ""code"",
    ""state"": ""{{\""csrf\"":\""{appUserContext.CSRFToken}\"",\""url\"":\""{appUserContext.AccountUrl}\"",\""windowId\"":\""{appUserContext.WindowId}\"",\""browserUrl\"":\""https://app.snowflake.com/\"",\""originator\"":\""{appUserContext.AuthOriginator}\"",\""oauthNonce\"":\""{appUserContext.AuthOAuthNonce}\""}}"",
    ""scope"": ""refresh_token"",
    ""codeChallenge"": ""{appUserContext.AuthCodeChallenge}"",
    ""codeChallengeMethod"": ""S256"",
    ""CLIENT_APP_ID"": ""Snowflake UI"",
    ""CLIENT_APP_VERSION"": 20240404084918,
    ""PASSWORD"": ""{password}""
  }}
}}";
            return apiPOST(
                appUserContext.AccountUrl,
                "session/authenticate-request",
                "application/json",
                requestBody,
                "application/json",
                cookies: appUserContext.Cookies, csrfTokenValue: null, snowflakeContext: String.Empty, referer: String.Empty, classicUIAuthToken: String.Empty);
        }

        public static string OAuth_Authorize_GetOAuthRedirectFromOAuthToken(AppUserContext appUserContext)
        {
            string requestBody = $@"{{
              ""masterToken"": ""{appUserContext.AuthTokenMaster}"",
              ""clientId"": ""{appUserContext.ClientID}"",
              ""redirectUri"": ""{appUserContext.AuthRedirectUri}"",
              ""responseType"": ""code"",
              ""state"": ""{{\""csrf\"":\""{appUserContext.CSRFToken}\"",\""url\"":\""{appUserContext.AccountUrl}\"",\""windowId\"":\""{appUserContext.WindowId}\"",\""browserUrl\"":\""https://app.snowflake.com/\"",\""originator\"":\""{appUserContext.AuthOriginator}\"",\""oauthNonce\"":\""{appUserContext.AuthOAuthNonce}\""}}"",
              ""scope"": ""refresh_token"",
              ""codeChallenge"": ""{appUserContext.AuthCodeChallenge}"",
              ""codeChallengeMethod"": ""S256""
            }}";

            return apiPOST(
                appUserContext.AccountUrl,
                "oauth/authorization-request",
                "application/json",
                requestBody,
                "application/json",
                cookies: appUserContext.Cookies, csrfTokenValue: appUserContext.CSRFToken, snowflakeContext: String.Empty, referer: String.Empty, classicUIAuthToken: String.Empty
                );
        }

        public static Tuple<string, CookieContainer, HttpStatusCode> OAuth_Complete_GetAuthenticationTokenFromOAuthRedirectToken(AppUserContext appUserContext, string redirectUrl)
        {
            return apiGET(
                appUserContext.AppServerUrl,
                redirectUrl,
                cookies: appUserContext.Cookies,
                referer: "https://mobilize.snowflakecomputing.com/"
                //host: "apps-api.c1.us-west-2.aws.app.snowflake.com"
            );
        }


        #endregion

        #region Classic UI Authentication

        public static string GetMasterTokenAndSessionTokenFromCredentials(string accountUrl, string accountName, string userName, string password, CookieContainer cookies)
        {
            string requestJSONTemplate =
@"{{
    ""data"": {{
        ""ACCOUNT_NAME"": ""{0}"",
        ""LOGIN_NAME"": ""{1}"",
        ""PASSWORD"": ""{2}""
    }}
}}";
            string requestBody = String.Format(requestJSONTemplate,
                accountName,
                userName,
                password);

            return apiPOST(
                accountUrl,
                "session/v1/login-request",
                "application/json",
                requestBody,
                "application/json",
                cookies: cookies
            );
        }

        public static string GetMasterTokenAndSessionTokenFromSSOToken(string accountUrl, string accountName, string userName, string token, string proofKey, CookieContainer cookies)
        {
            string requestJSONTemplate =
@"{{
    ""data"": {{
        ""ACCOUNT_NAME"": ""{0}"",
        ""LOGIN_NAME"": ""{1}"",
        ""AUTHENTICATOR"": ""EXTERNALBROWSER"",
        ""TOKEN"": ""{2}"",
        ""PROOF_KEY"": ""{3}""
    }}
}}";
            string requestBody = String.Format(requestJSONTemplate,
                accountName,
                userName,
                token,
                proofKey);

            return apiPOST(
                accountUrl,
                "session/v1/login-request",
                "application/json",
                requestBody,
                "application/json",
                cookies: cookies, snowflakeContext: String.Empty, referer: String.Empty, classicUIAuthToken: String.Empty);
        }

        public static string GetSSOLoginLinkForAccountAndUser(string accountUrl, string accountName, string userName, int returnRedirectPortNumber)
        {
            string requestJSONTemplate =
@"{{
    ""data"": {{
        ""ACCOUNT_NAME"": ""{0}"",
        ""LOGIN_NAME"": ""{1}"",
        ""AUTHENTICATOR"": ""EXTERNALBROWSER"",
        ""BROWSER_MODE_REDIRECT_PORT"": {2}
    }}
}}";
            string requestBody = String.Format(requestJSONTemplate,
                accountName,
                userName,
                returnRedirectPortNumber);

            return apiPOST(
                accountUrl,
                "session/authenticator-request",
                "application/json",
                requestBody,
                "application/json", null);
        }

        #endregion

        #region Snowsight Org Metadata

        public static Tuple<string, CookieContainer, HttpStatusCode> GetOrganizationAndUserContext(AppUserContext authContext)
        {
            return apiGET(
                authContext.AppServerUrl,
                "bootstrap",
                snowflakeContext: authContext.ContextUserNameUrl,
                referer: String.Format("{0}/", authContext.MainAppUrl),
                cookies: authContext.Cookies
            );
        }

        #endregion

        #region Snowsight Worksheets

        public static string GetWorksheets(AppUserContext authContext)
        {
            string optionsParam = "{\"sort\":{\"col\":\"viewed\",\"dir\":\"desc\"},\"limit\":500,\"owner\":null,\"types\":[\"query\"],\"showNeverViewed\":\"if-invited\"}";

            string requestBody = String.Format("options={0}&location=worksheets", HttpUtility.UrlEncode(optionsParam));

            return apiPOST(
                authContext.AppServerUrl,
                String.Format("v0/organizations/{0}/entities/list", authContext.OrganizationID),
                "application/json",
                requestBody,
                "application/x-www-form-urlencoded", 
                cookies: authContext.Cookies,
                csrfTokenValue: authContext.CSRFToken, 
                snowflakeContext: authContext.ContextUserNameUrl, 
                referer: String.Format("{0}/", authContext.MainAppUrl), 
                String.Empty);
        }

        public static string GetWorksheet(
            AppUserContext authContext,
            string worksheetID)
        {
            return apiGET(
                authContext.AppServerUrl,
                String.Format("v0/queries/{0}", worksheetID),
                "application/json",
                snowflakeContext: authContext.ContextUserNameUrl,
                referer: String.Format("{0}/", authContext.MainAppUrl),
                cookies: authContext.Cookies).Item1;
        }

        public static string CreateWorksheet(
            AppUserContext authContext,
            string worksheetName)
        {
            string requestBody = String.Format("action=create&orgId={0}&name={1}", authContext.OrganizationID, HttpUtility.UrlEncode(worksheetName));

            return apiPOST(
                authContext.AppServerUrl,
                "v0/queries",
                "application/json",
                requestBody,
                "application/x-www-form-urlencoded", 
                cookies: authContext.Cookies, 
                csrfTokenValue: authContext.CSRFToken,
                snowflakeContext: authContext.ContextUserNameUrl, 
                referer: String.Format("{0}/", authContext.MainAppUrl), 
                String.Empty);
        }

        public static string CreateWorksheetForFilter(
            AppUserContext authContext,
            string worksheetName, string role, string warehouse, string database, string schema)
        {
            string contextParam = String.Format("{{\"role\":\"{0}\",\"warehouse\":\"{1}\",\"database\":\"{2}\",\"schema\":\"{3}\"}}", role, warehouse, database, schema);

            string requestBody = String.Format("action=create&orgId={0}&name={1}&context={2}&paramQuery=1", authContext.OrganizationID, HttpUtility.UrlEncode(worksheetName), HttpUtility.UrlEncode(contextParam));

            return apiPOST(
                authContext.AppServerUrl,
                "v0/queries",
                "application/json",
                requestBody,
                "application/x-www-form-urlencoded", 
                cookies: authContext.Cookies, 
                csrfTokenValue: authContext.CSRFToken,
                snowflakeContext: authContext.ContextUserNameUrl, 
                referer: String.Format("{0}/", authContext.MainAppUrl), 
                String.Empty);
        }

        public static string CreateWorksheet(
            AppUserContext authContext,
            string worksheetName, string folderID)
        {
            string requestBody = String.Format("action=create&orgId={0}&name={1}&folderId={2}", authContext.OrganizationID, HttpUtility.UrlEncode(worksheetName), folderID);

            return apiPOST(
                authContext.AppServerUrl,
                "v0/queries",
                "application/json",
                requestBody,
                "application/x-www-form-urlencoded", 
                cookies: authContext.Cookies, 
                csrfTokenValue: authContext.CSRFToken,
                snowflakeContext: authContext.ContextUserNameUrl, 
                referer: String.Format("{0}/", authContext.MainAppUrl), 
                String.Empty);
        }

        public static string UpdateWorksheet(
            AppUserContext authContext,
            string worksheetID, string queryText, string role, string warehouse, string database, string schema)
        {
            string executionContextParam = String.Format("{{\"role\":\"{0}\",\"warehouse\":\"{1}\",\"database\":\"{2}\",\"schema\":\"{3}\"}}", role, warehouse, database, schema);

            string requestBody = String.Format("action=saveDraft&id={0}&projectId={0}&executionContext={1}&query={2}", worksheetID, HttpUtility.UrlEncode(executionContextParam), HttpUtility.UrlEncode(queryText));

            return apiPOST(
                authContext.AppServerUrl,
                "v0/queries",
                "application/json",
                requestBody,
                "application/x-www-form-urlencoded", 
                cookies: authContext.Cookies, 
                csrfTokenValue: authContext.CSRFToken,
                snowflakeContext: authContext.ContextUserNameUrl, 
                referer: String.Format("{0}/", authContext.MainAppUrl), 
                String.Empty);
        }

        public static string DeleteWorksheet(
            AppUserContext authContext,
            string worksheetID)
        {
            return apiDELETE(
                authContext.AppServerUrl,
                String.Format("v0/queries/{0}", worksheetID),
                "application/json",
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                String.Empty,
                authContext.Cookies
            );
        }

        public static string ExecuteWorksheet(
            AppUserContext authContext,
            string worksheetID, string queryText, string paramRefs, string role, string warehouse, string database, string schema)
        {
            string executionContextParam = String.Format("{{\"role\":\"{0}\",\"warehouse\":\"{1}\",\"database\":\"{2}\",\"schema\":\"{3}\"}}", role, warehouse, database, schema);

            string requestBody = String.Format("action=execute&projectId={0}&executionContext={1}&query={2}&paramRefs={3}", worksheetID, HttpUtility.UrlEncode(executionContextParam), HttpUtility.UrlEncode(queryText), HttpUtility.UrlEncode(paramRefs));

            return apiPOST(
                authContext.AppServerUrl,
                "v0/queries",
                "application/json",
                requestBody,
                "application/x-www-form-urlencoded",
                cookies: authContext.Cookies, 
                csrfTokenValue: authContext.CSRFToken,
                snowflakeContext: authContext.ContextUserNameUrl, 
                referer: String.Format("{0}/", authContext.MainAppUrl), 
                String.Empty);
        }

        #endregion

        #region Snowsight Dashboards

        public static string GetDashboards(
            AppUserContext authContext)
        {
            string optionsParam = "{\"sort\":{\"col\":\"viewed\",\"dir\":\"desc\"},\"limit\":500,\"owner\":null,\"types\":[\"dashboard\"],\"showNeverViewed\":\"if-invited\"}";

            string requestBody = String.Format("options={0}&location=worksheets", HttpUtility.UrlEncode(optionsParam));

            return apiPOST(
                authContext.AppServerUrl,
                String.Format("v0/organizations/{0}/entities/list", authContext.OrganizationID),
                "application/json",
                requestBody,
                "application/x-www-form-urlencoded",
                cookies: authContext.Cookies, 
                csrfTokenValue: authContext.CSRFToken,
                snowflakeContext: authContext.ContextUserNameUrl, 
                referer: String.Format("{0}/", authContext.MainAppUrl), 
                String.Empty);
        }

        public static string GetDashboard(
            AppUserContext authContext,
            string dashboardID)
        {
            return apiGET(
                authContext.AppServerUrl,
                String.Format("v0/folders/{0}", dashboardID),
                "application/json",
                snowflakeContext: authContext.ContextUserNameUrl,
                referer: String.Format("{0}/", authContext.MainAppUrl),
                cookies: authContext.Cookies).Item1;
        }

        public static string CreateDashboard(
            AppUserContext authContext,
            string dashboardName, string roleName, string warehouseName)
        {
            string requestBody = String.Format("orgId={0}&name={1}&role={2}&warehouse={3}&type=dashboard&visibility=organization", authContext.OrganizationID, HttpUtility.UrlEncode(dashboardName), roleName, warehouseName);

            return apiPOST(
                authContext.AppServerUrl,
                "v0/folders",
                "application/json",
                requestBody,
                "application/x-www-form-urlencoded",
                cookies: authContext.Cookies, 
                csrfTokenValue: authContext.CSRFToken,
                snowflakeContext: authContext.ContextUserNameUrl, 
                referer: String.Format("{0}/", authContext.MainAppUrl), 
                String.Empty);
        }

        public static string UpdateDashboardNewRowWithWorksheet(
            AppUserContext authContext,
            string dashboardID, string worksheetID, string displayMode, int rowIndex, int rowHeight)
        {
            // Table
            // [{
            //         "action": "insertRow",
            //         "params": {
            //             "pid": "2jlHIoKMPpx",
            //             "rowIdx": 0,
            //             "row": {
            //                 "height": 2,
            //                 "cells": [{
            //                         "pid": "2jlHIoKMPpx",
            //                         "displayMode": "table",
            //                         "type": "query"
            //                     }
            //                 ]
            //             },
            //             "cell": {
            //                 "pid": "2jlHIoKMPpx",
            //                 "displayMode": "table",
            //                 "type": "query"
            //             }
            //         }
            //     }
            // ]
            // 
            // Chart:
            // [{
            //         "action": "insertRow",
            //         "params": {
            //             "pid": "4VBAfU3r0IJ",
            //             "rowIdx": 4,
            //             "row": {
            //                 "height": 2,
            //                 "cells": [{
            //                         "pid": "4VBAfU3r0IJ",
            //                         "displayMode": "chart",
            //                         "type": "query"
            //                     }
            //                 ]
            //             },
            //             "cell": {
            //                 "pid": "4VBAfU3r0IJ",
            //                 "displayMode": "chart",
            //                 "type": "query"
            //             }
            //         }
            //     }
            // ]            
            string transformParamTemplate = @"[{{""action"": ""insertRow"", ""params"": {{""pid"": ""{0}"", ""rowIdx"": {2}, ""row"": {{""height"": {3}, ""cells"": [{{""pid"": ""{0}"", ""displayMode"": ""{1}"", ""type"": ""query""}}]}}, ""cell"": {{""pid"": ""{0}"", ""displayMode"": ""{1}"", ""type"": ""query""}}}}}}]";

            string transformParam = String.Format(transformParamTemplate, worksheetID, displayMode, rowIndex, rowHeight);
            string requestBody = String.Format("action=transformDashboard&transforms={0}", HttpUtility.UrlEncode(transformParam));

            return apiPOST(
                authContext.AppServerUrl,
                String.Format("v0/folders/{0}", dashboardID),
                "application/json",
                requestBody,
                requestTypeHeader: "application/x-www-form-urlencoded",
                cookies: authContext.Cookies, 
                csrfTokenValue: authContext.CSRFToken,
                snowflakeContext: authContext.ContextUserNameUrl, 
                referer: String.Format("{0}/", authContext.MainAppUrl), 
                String.Empty);
        }

        public static string UpdateDashboardInsertNewCellWithWorksheet(
            AppUserContext authContext,
            string dashboardID, string worksheetID, string displayMode, int rowIndex, int rowHeight, int cellIndex)
        {
            // Table:
            // [{
            //         "action": "insertCell",
            //         "params": {
            //             "pid": "3xTXA7vaxk6",
            //             "rowIdx": 0,
            //             "cellIdx": 1,
            //             "row": {
            //                 "height": 2,
            //                 "cells": [{
            //                         "pid": "3xTXA7vaxk6",
            //                         "displayMode": "table",
            //                         "type": "query"
            //                     }
            //                 ]
            //             },
            //             "cell": {
            //                 "pid": "3xTXA7vaxk6",
            //                 "displayMode": "table",
            //                 "type": "query"
            //             }
            //         }
            //     }
            // ]
            // 
            // Chart:
            // [{
            //         "action": "insertCell",
            //         "params": {
            //             "pid": "4VBAfU3r0IJ",
            //             "rowIdx": 3,
            //             "cellIdx": 1,
            //             "row": {
            //                 "height": 2,
            //                 "cells": [{
            //                         "pid": "4VBAfU3r0IJ",
            //                         "displayMode": "chart",
            //                         "type": "query"
            //                     }
            //                 ]
            //             },
            //             "cell": {
            //                 "pid": "4VBAfU3r0IJ",
            //                 "displayMode": "chart",
            //                 "type": "query"
            //             }
            //         }
            //     }
            // ]
            string transformParamTemplate = @"[{{""action"": ""insertCell"", ""params"": {{""pid"": ""{0}"", ""rowIdx"": {2}, ""cellIdx"": {4}, ""row"": {{""height"": {3}, ""cells"": [{{""pid"": ""{0}"", ""displayMode"": ""{1}"", ""type"": ""query""}}]}}, ""cell"": {{""pid"": ""{0}"", ""displayMode"": ""{1}"", ""type"": ""query""}}}}}}]";

            string transformParam = String.Format(transformParamTemplate, worksheetID, displayMode, rowIndex, rowHeight, cellIndex);
            string requestBody = String.Format("action=transformDashboard&transforms={0}", HttpUtility.UrlEncode(transformParam));

            return apiPOST(
                authContext.AppServerUrl,
                String.Format("v0/folders/{0}", dashboardID),
                "application/json",
                requestBody,
                "application/x-www-form-urlencoded",
                cookies: authContext.Cookies, 
                csrfTokenValue: authContext.CSRFToken,
                snowflakeContext: authContext.ContextUserNameUrl, 
                referer: String.Format("{0}/", authContext.MainAppUrl), 
                String.Empty);
        }

        public static string DeleteDashboard(
            AppUserContext authContext,
            string dashboardID)
        {
            return apiDELETE(
                authContext.AppServerUrl,
                String.Format("v0/folders/{0}", dashboardID),
                "application/json",
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                String.Empty,
                authContext.Cookies
            );
        }

        public static string ExecuteDashboard(
            AppUserContext authContext,
            string dashboardID)
        {
            string requestBody = "action=refresh&drafts={}";

            return apiPOST(
                authContext.AppServerUrl,
                String.Format("v0/folders/{0}", dashboardID),
                "application/json",
                requestBody,
                "application/x-www-form-urlencoded",                 
                cookies: authContext.Cookies, 
                csrfTokenValue: authContext.CSRFToken,
                snowflakeContext: authContext.ContextUserNameUrl, 
                referer: String.Format("{0}/", authContext.MainAppUrl), 
                String.Empty);
        }


        #endregion

        #region Snowsight Charts

        public static string GetChart(
            AppUserContext authContext,
            string worksheetID, string chartID)
        {
            return apiGET(
                authContext.AppServerUrl,
                $"v0/queries/{worksheetID}/charts/{chartID}",
                "application/json",
                snowflakeContext: authContext.ContextUserNameUrl,
                referer: String.Format("{0}/", authContext.MainAppUrl),
                cookies: authContext.Cookies).Item1;
        }

        public static string CreateChartFromWorksheet(
            AppUserContext authContext,
            string worksheetID, string chartConfiguration)
        {
            // {
            //     "type": "line",
            //     "lineStyle": {
            //         "fill": true,
            //         "trimYAxis": false
            //     },
            //     "source": "sources/query",
            //     "primary": [{
            //             "key": "CREATED",
            //             "domain": ["auto", "auto"],
            //             "numTicks": "auto",
            //             "bucket": "date"
            //         }
            //     ],
            //     "secondary": {
            //         "cols": [{
            //                 "key": "ROW_COUNT",
            //                 "aggregation": "sum"
            //             }
            //         ],
            //         "domain": ["auto", "auto"],
            //         "numTicks": "auto"
            //     },
            //     "showLegend": true,
            //     "version": 1
            // }
            string requestBody = String.Format("chart={0}", HttpUtility.UrlEncode(chartConfiguration));

            return apiPOST(
                authContext.AppServerUrl,
                String.Format("v0/queries/{0}/charts", worksheetID),
                "application/json",
                requestBody,
                "application/x-www-form-urlencoded", 
                cookies: authContext.Cookies, 
                csrfTokenValue: authContext.CSRFToken,
                snowflakeContext: authContext.ContextUserNameUrl, 
                referer: String.Format("{0}/", authContext.MainAppUrl), 
                String.Empty);
        }

        #endregion

        #region Snowsight Folders

        public static string GetFolders(
            AppUserContext authContext)
        {
            string optionsParam = "{\"sort\":{\"col\":\"viewed\",\"dir\":\"desc\"},\"limit\":500,\"owner\":null,\"types\":[\"folder\"],\"showNeverViewed\":\"if-invited\"}";

            string requestBody = String.Format("options={0}&location=worksheets", HttpUtility.UrlEncode(optionsParam));

            return apiPOST(
                authContext.AppServerUrl,
                String.Format("v0/organizations/{0}/entities/list", authContext.OrganizationID),
                "application/json",
                requestBody,
                "application/x-www-form-urlencoded",                 
                cookies: authContext.Cookies, 
                csrfTokenValue: authContext.CSRFToken,
                snowflakeContext: authContext.ContextUserNameUrl, 
                referer: String.Format("{0}/", authContext.MainAppUrl), 
                String.Empty);
        }

        #endregion

        #region Snowsight Queries

        public static string GetQueryDetails(
            AppUserContext authContext,
            string queryID, string roleToUse)
        {
            return apiGET(
                authContext.AppServerUrl,
                String.Format("v0/session/request/monitoring/queries/{0}?max=1001", queryID),
                "application/json",
                snowflakeContext: authContext.ContextUserNameUrl,
                referer: String.Format("{0}/", authContext.MainAppUrl),
                roleToUse: roleToUse,
                cookies: authContext.Cookies).Item1;
        }

        public static string GetQueryProfile(
            AppUserContext authContext,
            string queryID, string roleToUse)
        {
            return apiGET(
                authContext.AppServerUrl,
                String.Format("v0/session/request/monitoring/query-plan-data/{0}", queryID),
                "application/json",
                snowflakeContext: authContext.ContextUserNameUrl,
                referer: String.Format("{0}/", authContext.MainAppUrl),
                roleToUse: roleToUse,
                cookies: authContext.Cookies).Item1;
        }

        public static string GetQueryProfile(
            AppUserContext authContext,
            string queryID, string roleToUse, int retryNumber)
        {
            return apiGET(
                authContext.AppServerUrl,
                String.Format("v0/session/request/monitoring/query-plan-data/{0}?jobRetryAttemptRank={1}", queryID, retryNumber),
                "application/json",
                snowflakeContext: authContext.ContextUserNameUrl,
                referer: String.Format("{0}/", authContext.MainAppUrl),
                roleToUse: roleToUse,
                cookies: authContext.Cookies).Item1;
        }

        #endregion

        #region Snowsight Filters

        public static string CreateOrUpdateFilter(
            AppUserContext authContext,
            string filterKeyword, string filterConfiguration)
        {
            string requestBody = String.Format("paramConfig={0}", HttpUtility.UrlEncode(filterConfiguration));

            return apiPOST(
                authContext.AppServerUrl,
                String.Format("v0/organizations/{0}/param/{1}", authContext.OrganizationID, filterKeyword),
                "application/json",
                requestBody,
                "application/x-www-form-urlencoded", 
                cookies: authContext.Cookies, 
                csrfTokenValue: authContext.CSRFToken,
                snowflakeContext: authContext.ContextUserNameUrl, 
                referer: String.Format("{0}/", authContext.MainAppUrl), 
                String.Empty);
        }

        public static string DeleteFilter(
            AppUserContext authContext,
            string filterKeyword)
        {
            return apiDELETE(
                authContext.AppServerUrl,
                String.Format("v0/organizations/{0}/param/{1}", authContext.OrganizationID, filterKeyword),
                "application/json",
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                String.Empty,
                authContext.Cookies
            );
        }

        #endregion

        #region Retrieval GET and POST API

        /// Returns Tuple<string, CookieContainer, HttpStatusCode>
        ///               ^^^^^^                                    results of the page
        ///                       ^^^^^^^^^^^^                      list of cookies
        ///                                     ^^^^^^^^^^^^^^^     HTTP Result Code
        private static Tuple<string, CookieContainer, HttpStatusCode> apiGET(string baseUrl, string restAPIUrl,
            string acceptHeader = "*/*", string snowflakeContext = null, string referer = null,
            string classicUIAuthToken = null, string roleToUse = null, CookieContainer cookies = null, string host = null)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                HttpClientHandler httpClientHandler = new HttpClientHandler();
                httpClientHandler.UseCookies = true;
                httpClientHandler.CookieContainer = cookies ?? new CookieContainer();
                httpClientHandler.AllowAutoRedirect = true;
                // If customer certificates are not in trusted store, let's not fail
                httpClientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                using (HttpClient httpClient = new HttpClient(httpClientHandler))
                {
                    httpClient.Timeout = new TimeSpan(0, 1, 0);
                    Uri baseUri = new Uri(baseUrl);
                    httpClient.BaseAddress = baseUri;

                    httpClient.DefaultRequestHeaders.Add("User-Agent", String.Format("Snowflake Snowsight Extensions {0}", Assembly.GetExecutingAssembly().GetName().Version));

                    if (referer?.Length > 0)
                    {
                        httpClient.DefaultRequestHeaders.Referrer = new Uri(referer);
                    }
                    if (host?.Length > 0)
                    {
                        httpClient.DefaultRequestHeaders.Add("Host", host);
                    }
                    if (snowflakeContext?.Length > 0)
                    {
                        httpClient.DefaultRequestHeaders.Add("X-Snowflake-Context", snowflakeContext);
                    }
                    if (classicUIAuthToken?.Length > 0)
                    {
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", String.Format("Snowflake Token=\"{0}\"", classicUIAuthToken));
                    }
                    if (roleToUse?.Length > 0)
                    {
                        httpClient.DefaultRequestHeaders.Add("x-snowflake-role", roleToUse);
                    }

                    MediaTypeWithQualityHeaderValue accept = new MediaTypeWithQualityHeaderValue(acceptHeader);
                    if (httpClient.DefaultRequestHeaders.Accept.Contains(accept) == false)
                    {
                        httpClient.DefaultRequestHeaders.Accept.Add(accept);
                    }
                    HttpResponseMessage response = httpClient.GetAsync(restAPIUrl).Result;
                    stopWatch.Stop();

                    // extract all cookies from cookieContainer, into the list
                    ApiGetLogDiagnostic(response, restAPIUrl, cookies, httpClientHandler.CookieContainer, stopWatch.ElapsedMilliseconds);

                    if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Found)
                    {
                        string resultString = response.Content.ReadAsStringAsync().Result;

                        logger.Info("GET {0}/{1} returned {2} ({3})\nRequest Headers:\n{4}Cookies:\n{5}\nResponse Length {6}:\n{7}", baseUrl, restAPIUrl, (int)response.StatusCode, response.ReasonPhrase, httpClient.DefaultRequestHeaders, String.Join('\n', cookies?.ToString()), resultString.Length, resultString);

                        // Workaround for the issue that the API now returns the client_id in the URL, and the apiGET
                        // function doesn't return the URL. So we need to return the final redirected URL as the result for
                        // `start-oauth/snowflake`.
                        if (restAPIUrl.Contains("start-oauth/snowflake"))
                        {
                            resultString = response.RequestMessage.RequestUri.ToString();
                        }


                        return new Tuple<string, CookieContainer, HttpStatusCode>(resultString, httpClientHandler.CookieContainer, response.StatusCode);
                    }
                    else
                    {
                        string resultString = response.Content.ReadAsStringAsync().Result;
                        if (resultString.Length > 0)
                        {
                            logger.Info("GET {0}/{1} returned {2} ({3})\nRequest Headers:\n{4}Cookies:\n{5}\nResponse Length {6}:\n{7}", baseUrl, restAPIUrl, (int)response.StatusCode, response.ReasonPhrase, httpClient.DefaultRequestHeaders, httpClientHandler.CookieContainer.GetCookies(baseUri), resultString.Length, resultString);
                        }
                        else
                        {
                            logger.Error("GET {0}/{1} returned {2} ({3})\nRequest Headers:\n{4}Cookies:\n{5}", baseUrl, restAPIUrl, (int)response.StatusCode, response.ReasonPhrase, httpClient.DefaultRequestHeaders, httpClientHandler.CookieContainer.GetCookies(baseUri));
                        }

                        if (response.StatusCode == HttpStatusCode.Unauthorized ||
                            response.StatusCode == HttpStatusCode.Forbidden)
                        {
                            loggerConsole.Error("GET {0}/{1} returned {2} ({3})", baseUrl, restAPIUrl, (int)response.StatusCode, response.ReasonPhrase);
                        }

                        return new Tuple<string, CookieContainer, HttpStatusCode>(String.Empty, httpClientHandler.CookieContainer, response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("GET {0}/{1} threw {2} ({3})", baseUrl, restAPIUrl, ex.Message, ex.Source);
                logger.Error(ex);

                loggerConsole.Error("GET {0}/{1} threw {2} ({3})", baseUrl, restAPIUrl, ex.Message, ex.Source);
                return new Tuple<string, CookieContainer, HttpStatusCode>(String.Empty, new CookieContainer(), HttpStatusCode.InternalServerError);
            }
            finally
            {
                stopWatch.Stop();
                logger.Info("GET {0}/{1} took {2:c} ({3} ms)", baseUrl, restAPIUrl, stopWatch.Elapsed.ToString("c"), stopWatch.ElapsedMilliseconds);
            }
        }

        private static string apiPOST(string baseUrl, string restAPIUrl, string acceptHeader, string requestBody,
            string requestTypeHeader, CookieContainer cookies, string csrfTokenValue = null, string snowflakeContext = null, string referer = null,
            string classicUIAuthToken = null)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                HttpClientHandler httpClientHandler = new HttpClientHandler();
                httpClientHandler.UseCookies = true;
                httpClientHandler.CookieContainer = cookies;
                httpClientHandler.AllowAutoRedirect = false;
                // If customer certificates are not in trusted store, let's not fail
                httpClientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                using (HttpClient httpClient = new HttpClient(httpClientHandler))
                {
                    httpClient.Timeout = new TimeSpan(0, 1, 0);
                    Uri baseUri = new Uri(baseUrl);
                    httpClient.BaseAddress = baseUri;

                    httpClient.DefaultRequestHeaders.Add("User-Agent", String.Format("Snowflake Snowsight Extensions {0}", Assembly.GetExecutingAssembly().GetName().Version));

                    if (referer?.Length > 0)
                    {
                        httpClient.DefaultRequestHeaders.Referrer = new Uri(referer);
                    }
                    if (snowflakeContext?.Length > 0)
                    {
                        httpClient.DefaultRequestHeaders.Add("X-Snowflake-Context", snowflakeContext);
                    }
                    if (classicUIAuthToken?.Length > 0)
                    {
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", String.Format("Snowflake Token=\"{0}\"", classicUIAuthToken));
                    }
                    // Hacky workaround to add `X-CSRF-Token` for authenticated requests, we need to check for the
                    // user- cookie.
                    //var userCookie = cookies.GetCookies(baseUri).Cast<Cookie>().FirstOrDefault(c => c.Name.StartsWith("user"));
                    if (csrfTokenValue != null)
                    {
                        httpClient.DefaultRequestHeaders.Add("X-CSRF-Token", csrfTokenValue);
                    }
                    else
                    {
                        var csrfCookie = cookies.GetCookies(baseUri).Cast<Cookie>().FirstOrDefault(c => c.Name.StartsWith("csrf"));
                        if (csrfCookie != null)
                        {
                            httpClient.DefaultRequestHeaders.Add("X-CSRF-Token", csrfCookie.Value);
                        }
                    }

                    MediaTypeWithQualityHeaderValue accept = new MediaTypeWithQualityHeaderValue(acceptHeader);
                    if (httpClient.DefaultRequestHeaders.Accept.Contains(accept) == false)
                    {
                        httpClient.DefaultRequestHeaders.Accept.Add(accept);
                    }

                    StringContent content = new StringContent(requestBody);
                    content.Headers.ContentType = new MediaTypeWithQualityHeaderValue(requestTypeHeader);

                    // As exception for the login
                    // Remove sensitive data
                    if (restAPIUrl.StartsWith("session/authenticate-request") ||
                        restAPIUrl.StartsWith("session/v1/login-request"))
                    {
                        var pattern = "\"PASSWORD\": \"(.*)\"";
                        requestBody = Regex.Replace(requestBody, pattern, "\"PASSWORD\":\"****\"", RegexOptions.IgnoreCase);
                    }

                    HttpResponseMessage response = httpClient.PostAsync(restAPIUrl, content).Result;
                    stopWatch.Stop();

                    ApiPostLogDiagnostic(response, restAPIUrl, cookies, httpClientHandler.CookieContainer, stopWatch.ElapsedMilliseconds);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultString = response.Content.ReadAsStringAsync().Result;
                        if (resultString == null) resultString = String.Empty;

                        logger.Info("POST {0}/{1} returned {2} ({3})\nRequest Headers:\n{4}Cookies:\n{5}\nRequest:\n{6}\nResponse Length {7}:\n{8}", baseUrl, restAPIUrl, (int)response.StatusCode, response.ReasonPhrase, httpClient.DefaultRequestHeaders, httpClientHandler.CookieContainer, requestBody, resultString.Length, resultString);

                        return resultString;
                    }
                    else
                    {
                        string resultString = response.Content.ReadAsStringAsync().Result;
                        if (resultString == null) resultString = String.Empty;
                        if (resultString.Length > 0)
                        {
                            logger.Error("POST {0}/{1} returned {2} ({3})\nRequest Headers:\n{4}Cookies:\n{5}\nRequest:\n{6}\nResponse Length {7}:\n{8}", baseUrl, restAPIUrl, (int)response.StatusCode, response.ReasonPhrase, httpClient.DefaultRequestHeaders, httpClientHandler.CookieContainer, requestBody, resultString.Length, resultString);
                        }
                        else
                        {
                            logger.Error("POST {0}/{1} returned {2} ({3})\nRequest Headers:\n{4}Cookies:\n{5}\nRequest:\n{6}", baseUrl, restAPIUrl, (int)response.StatusCode, response.ReasonPhrase, httpClient.DefaultRequestHeaders, httpClientHandler.CookieContainer, requestBody);
                        }

                        if (response.StatusCode == HttpStatusCode.Unauthorized ||
                            response.StatusCode == HttpStatusCode.Forbidden)
                        {
                            loggerConsole.Warn("POST {0}/{1} returned {2} ({3}), Request:\n{4}", baseUrl, restAPIUrl, (int)response.StatusCode, response.ReasonPhrase, requestBody);
                        }

                        return String.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("POST {0}/{1} threw {2} ({3})", baseUrl, restAPIUrl, ex.Message, ex.Source);
                logger.Error(ex);

                loggerConsole.Error("POST {0}/{1} threw {2} ({3})", baseUrl, restAPIUrl, ex.Message, ex.Source);
                return String.Empty;
            }
            finally
            {
                stopWatch.Stop();
                logger.Info("POST {0}/{1} took {2:c} ({3} ms)", baseUrl, restAPIUrl, stopWatch.Elapsed.ToString("c"), stopWatch.ElapsedMilliseconds);
            }
        }

        private static string apiDELETE(string baseUrl, string restAPIUrl, string acceptHeader, string snowflakeContext = null, string referer = null, string classicUIAuthToken = null, CookieContainer cookies = null)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                HttpClientHandler httpClientHandler = new HttpClientHandler();
                httpClientHandler.UseCookies = true;
                httpClientHandler.CookieContainer = cookies ?? new CookieContainer();
                httpClientHandler.AllowAutoRedirect = false;

                using (HttpClient httpClient = new HttpClient(httpClientHandler))
                {
                    httpClient.Timeout = new TimeSpan(0, 1, 0);
                    Uri baseUri = new Uri(baseUrl);
                    httpClient.BaseAddress = baseUri;

                    httpClient.DefaultRequestHeaders.Add("User-Agent", String.Format("Snowflake Snowsight Extensions {0}", Assembly.GetExecutingAssembly().GetName().Version));

                    if (referer?.Length > 0)
                    {
                        httpClient.DefaultRequestHeaders.Referrer = new Uri(referer);
                    }
                    if (snowflakeContext?.Length > 0)
                    {
                        httpClient.DefaultRequestHeaders.Add("X-Snowflake-Context", snowflakeContext);
                    }
                    if (classicUIAuthToken?.Length > 0)
                    {
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", String.Format("Snowflake Token=\"{0}\"", classicUIAuthToken));
                    }

                    MediaTypeWithQualityHeaderValue accept = new MediaTypeWithQualityHeaderValue(acceptHeader);
                    if (httpClient.DefaultRequestHeaders.Accept.Contains(accept) == false)
                    {
                        httpClient.DefaultRequestHeaders.Accept.Add(accept);
                    }

                    HttpResponseMessage response = httpClient.DeleteAsync(restAPIUrl).Result;

                    IEnumerable<string> cookiesList = new List<string>();
                    if (response.Headers.Contains("Set-Cookie") == true)
                    {
                        cookiesList = response.Headers.GetValues("Set-Cookie");
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        string resultString = response.Content.ReadAsStringAsync().Result;
                        if (resultString == null) resultString = String.Empty;

                        logger.Info("DELETE {0}/{1} returned {2} ({3})\nRequest Headers:\n{4}Cookies:\n{5}\nResponse Length {6}:\n{7}", baseUrl, restAPIUrl, (int)response.StatusCode, response.ReasonPhrase, httpClient.DefaultRequestHeaders, String.Join('\n', cookiesList), resultString.Length, resultString);

                        return resultString;
                    }
                    else
                    {
                        string resultString = response.Content.ReadAsStringAsync().Result;
                        if (resultString == null) resultString = String.Empty;
                        if (resultString.Length > 0)
                        {
                            logger.Error("DELETE {0}/{1} returned {2} ({3})\nRequest Headers:\n{4}Cookies:\n{5}\nResponse Length {6}:\n{7}", baseUrl, restAPIUrl, (int)response.StatusCode, response.ReasonPhrase, httpClient.DefaultRequestHeaders, String.Join('\n', cookiesList), resultString.Length, resultString);
                        }
                        else
                        {
                            logger.Error("DELETE {0}/{1} returned {2} ({3})", baseUrl, restAPIUrl, (int)response.StatusCode, response.ReasonPhrase);
                        }

                        if (response.StatusCode == HttpStatusCode.Unauthorized ||
                            response.StatusCode == HttpStatusCode.Forbidden)
                        {
                            loggerConsole.Warn("DELETE {0}/{1} returned {2} ({3})", baseUrl, restAPIUrl, (int)response.StatusCode, response.ReasonPhrase);
                        }

                        return String.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("DELETE {0}/{1} threw {2} ({3})", baseUrl, restAPIUrl, ex.Message, ex.Source);
                logger.Error(ex);

                loggerConsole.Error("DELETE {0}/{1} threw {2} ({3})", baseUrl, restAPIUrl, ex.Message, ex.Source);
                return String.Empty;
            }
            finally
            {
                stopWatch.Stop();
                logger.Info("DELETE {0}/{1} took {2:c} ({3} ms)", baseUrl, restAPIUrl, stopWatch.Elapsed.ToString("c"), stopWatch.ElapsedMilliseconds);
            }
        }

        #endregion

        #region Internal helper methods

        private static void ApiGetLogDiagnostic(HttpResponseMessage response, string restApiUrl,
            CookieContainer requestCookies, CookieContainer responseCookies, long stopWatchElapsedMilliseconds)
        {
            // Right now this function is pretty basic, to allow us to diagnose login issues in the short-term.
            // This will be improved upon in future, such using HTTP Handlers to log the request/response, which
            // means we're not reliant on the HttpClient to log the request/response.
            //
            // This function is designed to sanitise the output on a per-endpoint basis,
            // and falling back we warn that the endpoint is not handled, with the full URL
            // logged to the main logger instead.
            //
            // In each block we try and parse JSON (if the response *should* be JSON), use try/catch, as we receive
            // a response that isn't JSON, which we'll then log.

            // Split the responses to make it a bit easier to read
            loggerDiagnosticTest.Info("--------------------");

            // loggerDiagnosticTest.Info("Request URL: {0}", restApiUrl);
            string finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? "";
            // loggerDiagnosticTest.Info("Final URL: {0}", finalUrl);

            // @todo In .NET 7, they introduced better pattern matching for switch statements, which would
            // allow us to match on the what the URL begins with.
            // As this project currently targets 6.0, we work around this for now by using if statements.
            // GET URLs currently targeted (beginning with) are:
            // 1. /v0/validate-snowflake-url - (200) Validates the Snowflake URL as a first step in the login process
            // 2. /start-oauth/snowflake - (302) Begins the login process, which should be a 302 to
            // https://apps-api.c1.REGION.aws.app.snowflake.com/sessionmanager/login/oauth2login/oauth2
            // has the S8_SESSION cookie.
            // 3. /sessionmanager/login/oauth2/authorization - (302) Redirected from #2, which then redirects to
            // https://ACCOUNT.REGION.snowflakecomputing.com/oauth/authorize , which is the users Snowflake
            // instance, instead of the apps-api.c1.REGION.aws.app.snowflake.com endpoint. Has the `oauth-nonce`
            // cookie. Note: We generally won't hit this endpoint, as the HTTP Client in .NET will follow the redirect
            // automatically, and doesn't keep track of the URLs. Having this endpoint show in a log will usually be a
            // good indicator that something went wrong during the login process. 
            // 4. /oauth/authorize - (200) Final step i nt he start-oauth flow for the user (2 -> 3 -> 4),
            // we don't need anything here.
            // 5. /complete-oauth/snowflake - (200) - The URL returned from oauth/authorization-request, which
            // returns the user-xxx cookie, which is needed for authenticated requests. Page is a HTML response,
            // which also has a JSON blob in `var params` with params which are found in bootstrap. As bootstrap
            // is needed to be hit anyway, we don't need to parse this.
            // 6. /bootstrap - (200) - Returns a JSON blob with information about the User, Org, etc. Can be either
            // unauthenticated or authenticated, with authenticated returning more information. Returns the csrf-xxx
            // cookie which is used for additional requests.
            if (restApiUrl.StartsWith("v0/validate-snowflake-url"))
            {
                loggerDiagnosticTest.Info("GET /v0/validate-snowflake-url");
                loggerDiagnosticTest.Info("Response: {0} ({1}) in {2}ms", (int)response.StatusCode,
                    response.ReasonPhrase, stopWatchElapsedMilliseconds);
                string account = "N/A";
                string appServerUrl = "N/A";
                string region = "N/A";
                string url = "N/A";
                string valid = "N/A";
                try
                {
                    // Example valid response:
                    // {
                    //     "account": "ez40571",
                    //     "appServerUrl": "https://apps-api.c1.REGION.aws.app.snowflake.com",
                    //     "region": "REGION",
                    //     "regionGroup": "PUBLIC",
                    //     "snowflakeRegion": "AWS_REGION",
                    //     "url": "https://REGION.snowflakecomputing.com",
                    //     "valid": true
                    // }
                    // Example invalid response (eg account/region is incorrect):
                    // {"valid":false}
                    string resultString = response.Content.ReadAsStringAsync().Result;
                    JObject jsonResponse = JObject.Parse(resultString);
                    valid = JSONHelper.getBoolValueFromJToken(jsonResponse, "valid") == false ? "INVALID" : "VALID";
                    // return as "EXISTS" if it exists, otherwise "N/A"
                    // getStringValueFromJToken returns String.Empty if the value doesn't exist/invalid
                    account = JSONHelper.getStringValueFromJToken(jsonResponse, "account") == String.Empty
                        ? "N/A"
                        : "EXISTS";
                    appServerUrl = JSONHelper.getStringValueFromJToken(jsonResponse, "appServerUrl") == String.Empty
                        ? "N/A"
                        : "EXISTS";
                    region = JSONHelper.getStringValueFromJToken(jsonResponse, "region") == String.Empty
                        ? "N/A"
                        : "EXISTS";
                    url = JSONHelper.getStringValueFromJToken(jsonResponse, "url") == String.Empty ? "N/A" : "EXISTS";
                }
                catch (Exception ex)
                {
                    // @todo We could use ex.Message here, but it might contain sensitive information.
                    loggerDiagnosticTest.Error("Failed to parse JSON response");
                    logger.Error(ex);
                }

                loggerDiagnosticTest.Info(
                    "Response Body: valid: {0} | account: {1} | appServerUrl: {2} | region: {3} | url: {4}", valid,
                    account, appServerUrl, region, url);
            }
            else if (restApiUrl.StartsWith("start-oauth/snowflake"))
            {
                loggerDiagnosticTest.Info("GET /start-oauth/snowflake");
                loggerDiagnosticTest.Info("Response: {0} ({1}) in {2}ms", (int)response.StatusCode,
                    response.ReasonPhrase, stopWatchElapsedMilliseconds);

                // We only care if if the redirect URL contains oauth/authorize, as that is the current final URL
                bool correctRedirectUrl = finalUrl.Contains("oauth/authorize");

                loggerDiagnosticTest.Info("Redirected to oauth/authorize: {0}", correctRedirectUrl);
            }
            else if (restApiUrl.StartsWith("complete-oauth/snowflake"))
            {
                loggerDiagnosticTest.Info("GET /complete-oauth/snowflake");
                loggerDiagnosticTest.Info("Response: {0} ({1}) in {2}ms", (int)response.StatusCode,
                    response.ReasonPhrase, stopWatchElapsedMilliseconds);
                string resultString = response.Content.ReadAsStringAsync().Result;
                // does the page contents contain "var params"?
                bool containsVarParams = resultString.Contains("var params");

                // What was the response type, if not found fallback to N/A
                string responseType = response.Content.Headers.ContentType?.MediaType ?? "N/A";
                // Response Body: HTML, params var: EXISTS
                loggerDiagnosticTest.Info("Response Body: {0}, params var: {1}", responseType, containsVarParams);
            }
            else if (restApiUrl.StartsWith("bootstrap"))
            {
                loggerDiagnosticTest.Info("GET /bootstrap");
                loggerDiagnosticTest.Info("Response: {0} ({1}) in {2}ms", (int)response.StatusCode,
                    response.ReasonPhrase, stopWatchElapsedMilliseconds);
                // Response Body: BuildVersion: 240125-9-f8441ccb37, user.id: EXISTS
                string resultString = response.Content.ReadAsStringAsync().Result;
                // BuildVersion is from the JSON
                string buildVersion = "N/A";
                string userId = "N/A";
                try
                {
                    JObject jsonResponse = JObject.Parse(resultString);
                    buildVersion = JSONHelper.getStringValueFromJToken(jsonResponse, "BuildVersion") == String.Empty
                        ? "N/A"
                        : "EXISTS";
                    userId = JSONHelper.getStringValueFromJToken(jsonResponse["User"], "id") == String.Empty
                        ? "N/A"
                        : "EXISTS";
                }
                catch (Exception ex)
                {
                    // @todo We could use ex.Message here, but it might contain sensitive information.
                    loggerExtensiveDiagnosticTest.Error("Failed to parse JSON response | error: {0}", ex.Message);
                    logger.Error(ex);
                }
                loggerDiagnosticTest.Info("Response Body: BuildVersion: {0} | User.id: {1}", buildVersion, userId);
            }
            else
            {
                // else we ended up somewhere we didn't expect.. We don't want to expose this in the logfile for
                // DiagnosticTest, as this might be a sensitive URL or contain sensitive information.
                logger.Warn("DiagnosticTest - GET {0} didn't match any known URL", restApiUrl);
                loggerDiagnosticTest.Info("Couldn't match on GET, unknown URL");
            }

            // Convert the cookies into a string, and remove sensitive information.
            // Note that for redirects, these are cookies from all redirects, not just the final URL.
            // This is because the HttpClient in .NET will follow the redirects automatically, and doesn't
            // keep track of the URLs/cookies per URL. This is fine for our purposes, as we're just trying to
            // diagnose login issues.
            Uri restApiUri = new Uri(finalUrl);
            // Uri restApiUrlBaseDomain = new Uri(restApiUri.Host);

            // get all cookies that have the base domain, which is kind of wonky as the redirect
            // process in the pst redirects to multiple domains..
            CookieCollection responseCookiesFiltered = responseCookies.GetCookies(restApiUri);
            CookieCollection requestCookiesFiltered = requestCookies.GetCookies(restApiUri);

            LogSanitiseCookies("Request", requestCookiesFiltered);
            LogSanitiseCookies("Response", responseCookiesFiltered);
        }

        private static void ApiPostLogDiagnostic(HttpResponseMessage response, string restApiUrl, CookieContainer requestCookies, CookieContainer responseCookies, long stopWatchElapsedMilliseconds)
        {
            loggerDiagnosticTest.Info("--------------------");

            if (restApiUrl.StartsWith("session/v1/login-request"))
            {
                // Response: 200 in 1000ms
                // Response Body: success: TRUE, code: NULL, serverVersion: 8.4.1, masterToken: EXISTS, token: EXISTS, sessionId: EXISTS, displayUserName: EXISTS, schemaName: EXISTS, warehouseName: EXISTS, roleName: EXISTS, databaseName: EXISTS
                loggerDiagnosticTest.Info("POST /session/v1/login-request");
                loggerDiagnosticTest.Info("Response: {0} ({1}) in {2}ms", (int)response.StatusCode, response.ReasonPhrase, stopWatchElapsedMilliseconds);
                string success = "N/A";
                string code = "N/A";
                string serverVersion = "N/A";
                string masterToken = "N/A";
                string token = "N/A";
                string sessionId = "N/A";
                string displayUserName = "N/A";
                string schemaName = "N/A";
                string warehouseName = "N/A";
                string roleName = "N/A";
                string databaseName = "N/A";
                try
                {
                    string resultString = response.Content.ReadAsStringAsync().Result;
                    JObject jsonResponse = JObject.Parse(resultString);
                    success = JSONHelper.getBoolValueFromJToken(jsonResponse, "success") == false ? "FALSE" : "TRUE";
                    code = JSONHelper.getStringValueFromJToken(jsonResponse, "code") == String.Empty ? "N/A" : "EXISTS";

                    // The data object is only returned on successful login.
                    if (jsonResponse.TryGetValue("data", out JToken data))
                    {
                        serverVersion = JSONHelper.getStringValueFromJToken(data, "serverVersion") == String.Empty ? "N/A" : "EXISTS";
                        masterToken = JSONHelper.getStringValueFromJToken(data, "masterToken") == String.Empty ? "N/A" : "EXISTS";
                        token = JSONHelper.getStringValueFromJToken(data, "token") == String.Empty ? "N/A" : "EXISTS";
                        sessionId = JSONHelper.getStringValueFromJToken(data, "sessionId") == String.Empty ? "N/A" : "EXISTS";
                        displayUserName = JSONHelper.getStringValueFromJToken(data, "displayUserName") == String.Empty ? "N/A" : "EXISTS";

                        // These values are inside sessionInfo, which should exist if data exists
                        schemaName = JSONHelper.getStringValueFromJToken(data["sessionInfo"], "schemaName") == String.Empty ? "N/A" : "EXISTS";
                        warehouseName = JSONHelper.getStringValueFromJToken(data["sessionInfo"], "warehouseName") == String.Empty ? "N/A" : "EXISTS";
                        roleName = JSONHelper.getStringValueFromJToken(data["sessionInfo"], "roleName") == String.Empty ? "N/A" : "EXISTS";
                        databaseName = JSONHelper.getStringValueFromJToken(data["sessionInfo"], "databaseName") == String.Empty ? "N/A" : "EXISTS";
                    }
                }
                catch (Exception ex)
                {
                    // @todo We could use ex.Message here, but it might contain sensitive information.
                    loggerDiagnosticTest.Error("Failed to parse JSON response");
                    logger.Error(ex);
                }
                loggerDiagnosticTest.Info("Response Body: success: {0} | code: {1} | serverVersion: {2} | masterToken: {3} | token: {4} | sessionId: {5} | displayUserName: {6} | schemaName: {7} | warehouseName: {8} | roleName: {9} | databaseName: {10}", success, code, serverVersion, masterToken, token, sessionId, displayUserName, schemaName, warehouseName, roleName, databaseName);
            }
            else if (restApiUrl.StartsWith("session/authenticate-request"))
            {
                loggerDiagnosticTest.Info("POST /session/authenticate-request");
                loggerDiagnosticTest.Info("Response: {0} ({1}) in {2}ms", (int)response.StatusCode, response.ReasonPhrase, stopWatchElapsedMilliseconds);
                string success = "N/A";
                string code = "N/A";
                string masterToken = "N/A";
                try
                {
                    string resultString = response.Content.ReadAsStringAsync().Result;
                    JObject jsonResponse = JObject.Parse(resultString);
                    success = JSONHelper.getBoolValueFromJToken(jsonResponse, "success") == false ? "FALSE" : "TRUE";
                    code = JSONHelper.getStringValueFromJToken(jsonResponse, "code") == String.Empty ? "N/A" : "EXISTS";
                    masterToken = JSONHelper.getStringValueFromJToken(jsonResponse["data"], "masterToken") == String.Empty ? "N/A" : "EXISTS";
                }
                catch (Exception ex)
                {
                    // @todo We could use ex.Message here, but it might contain sensitive information.
                    loggerDiagnosticTest.Error("Failed to parse JSON response");
                    logger.Error(ex);
                }
                loggerDiagnosticTest.Info("Response Body: success: {0} | code: {1} | masterToken: {2}", success, code, masterToken);
            }
            else if (restApiUrl.StartsWith("oauth/authorization-request"))
            {
                // Response: 200 in 1000ms
                // Response Body: success: FALSE, code: 390301, data.redirectUrl: EXISTS, data.nextAction: OAUTH_REDIRECT, data.inFlightCtx: EXISTS
                loggerDiagnosticTest.Info("POST /oauth/authorization-request");
                loggerDiagnosticTest.Info("Response: {0} ({1}) in {2}ms", (int)response.StatusCode, response.ReasonPhrase, stopWatchElapsedMilliseconds);
                string success = "N/A";
                string code = "N/A";
                string redirectUrl = "N/A";
                string nextAction = "N/A";
                string inFlightCtx = "N/A";
                try
                {
                    string resultString = response.Content.ReadAsStringAsync().Result;
                    JObject jsonResponse = JObject.Parse(resultString);
                    success = JSONHelper.getBoolValueFromJToken(jsonResponse, "success") == false ? "FALSE" : "TRUE";
                    code = JSONHelper.getStringValueFromJToken(jsonResponse, "code") == String.Empty ? "N/A" : "EXISTS";
                    redirectUrl = JSONHelper.getStringValueFromJToken(jsonResponse["data"], "redirectUrl") == String.Empty ? "N/A" : "EXISTS";
                    nextAction = JSONHelper.getStringValueFromJToken(jsonResponse["data"], "nextAction") == String.Empty ? "N/A" : "EXISTS";
                    inFlightCtx = JSONHelper.getStringValueFromJToken(jsonResponse["data"], "inFlightCtx") == String.Empty ? "N/A" : "EXISTS";
                }
                catch (Exception ex)
                {
                    // @todo We could use ex.Message here, but it might contain sensitive information.
                    loggerDiagnosticTest.Error("Failed to parse JSON response");
                    logger.Error(ex);
                }
                loggerDiagnosticTest.Info("Response Body: success: {0} | code: {1} | redirectUrl: {2} | nextAction: {3} | inFlightCtx: {4}", success, code, redirectUrl, nextAction, inFlightCtx);
            }
            else if (Regex.Match(restApiUrl, @"^v0/organizations/(\w+)/entities/list$").Success)
            {
                loggerDiagnosticTest.Info("POST /v0/organizations/XXX/entities/list");
                loggerDiagnosticTest.Info("Response: {0} ({1}) in {2}ms", (int)response.StatusCode, response.ReasonPhrase, stopWatchElapsedMilliseconds);
                string resultString = response.Content.ReadAsStringAsync().Result;
                // does the page contents contain "defaultOrgId"?. Silly way, @todo later we should parse the JSON better
                string containsOrganizations = resultString.Contains("defaultOrgId") == true ? "EXISTS" : "N/A";
                loggerDiagnosticTest.Info("Response Body: defaultOrgId: {0}", containsOrganizations);
            }
            else if (restApiUrl.StartsWith("session/authenticator-request"))
            {
                // response should have data.tokenUrl as null, data.ssoUrl, data.proofKey, code as null, message as null and success as true
                loggerDiagnosticTest.Info("POST /session/authenticator-request");
                loggerDiagnosticTest.Info("Response: {0} ({1}) in {2}ms", (int)response.StatusCode, response.ReasonPhrase, stopWatchElapsedMilliseconds);
                string success = "N/A";
                string code = "N/A";
                string message = "N/A";
                string tokenUrl = "N/A";
                string ssoUrl = "N/A";
                string proofKey = "N/A";
                try
                {
                    string resultString = response.Content.ReadAsStringAsync().Result;
                    JObject jsonResponse = JObject.Parse(resultString);
                    success = JSONHelper.getBoolValueFromJToken(jsonResponse, "success") == false ? "FALSE" : "TRUE";
                    code = JSONHelper.getStringValueFromJToken(jsonResponse, "code") == String.Empty ? "N/A" : "EXISTS";
                    message = JSONHelper.getStringValueFromJToken(jsonResponse, "message") == String.Empty ? "N/A" : "EXISTS";

                    // data object is only returned on successful login.
                    if (jsonResponse.TryGetValue("data", out JToken data))
                    {
                        tokenUrl = JSONHelper.getStringValueFromJToken(data, "tokenUrl") == String.Empty ? "N/A" : "EXISTS";
                        ssoUrl = JSONHelper.getStringValueFromJToken(data, "ssoUrl") == String.Empty ? "N/A" : "EXISTS";
                        proofKey = JSONHelper.getStringValueFromJToken(data, "proofKey") == String.Empty ? "N/A" : "EXISTS";
                    }
                }
                catch (Exception ex)
                {
                    // @todo We could use ex.Message here, but it might contain sensitive information.
                    loggerDiagnosticTest.Error("Failed to parse JSON response");
                    logger.Error(ex);
                }

                loggerDiagnosticTest.Info("Response Body: success: {0} | code: {1} | message: {2} | tokenUrl: {3} | ssoUrl: {4} | proofKey: {5}", success, code, message, tokenUrl, ssoUrl, proofKey);
            }
            else
            {
                // else we ended up somewhere we didn't expect.. We don't want to expose this in the logfile for
                // DiagnosticTest, as this might be a sensitive URL or contain sensitive information.
                loggerExtensiveDiagnosticTest.Warn("POST {0} didn't match any known URL", restApiUrl);
                loggerDiagnosticTest.Info("Couldn't match on POST, unknown URL");
            }

            // Convert the cookies into a string, and remove sensitive information.
            // Note that for redirects, these are cookies from all redirects, not just the final URL.
            // This is because the HttpClient in .NET will follow the redirects automatically, and doesn't
            // keep track of the URLs/cookies per URL. This is fine for our purposes, as we're just trying to
            // diagnose login issues.
            // base domain of restApiUrl
            string finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? "";
            Uri restApiUri = new Uri(finalUrl);

            // get all cookies that have the base domain
            CookieCollection responseCookiesFiltered = responseCookies.GetCookies(restApiUri);
            CookieCollection requestCookiesFiltered = requestCookies.GetCookies(restApiUri);

            LogSanitiseCookies("Request", requestCookiesFiltered);
            LogSanitiseCookies("Response", responseCookiesFiltered);
        }

        private static void LogSanitiseCookies(string direction, CookieCollection cookiesList)
        {
            if (cookiesList.Count == 0)
            {
                loggerDiagnosticTest.Info("{0} Cookies: N/A", direction);
                return;
            }
            // We want to extract the cookies from the request OR response (direction), and sanitise them.
            // To do this safely (as in not returning sensitive information), we'll match on the start of the cookie.
            // Cookies we currently check for:
            // oauth-nonce- (sessionmanager/login/oauth2/authorization, complete-oauth/snowflake)
            // S8_SESSION (start-oauth/snowflake)
            // csrf- (bootstrap)
            // If not matched, return "Unknown"
            // @todo use a list instead, then join later
            var sanitisedCookiesList = new List<string>();
            var unknownCookiesList = new List<string>();
            foreach (Cookie cookie in cookiesList)
            {
                if (cookie.Name.StartsWith("oauth-nonce-"))
                {
                    sanitisedCookiesList.Add("oauth-nonce-XXX: EXISTS");
                }
                else if (cookie.Name.StartsWith("S8_SESSION"))
                {
                    sanitisedCookiesList.Add("S8_SESSION: EXISTS");
                }
                else if (cookie.Name.StartsWith("csrf-"))
                {
                    sanitisedCookiesList.Add("csrf-XXX: EXISTS");
                }
                else if (cookie.Name.StartsWith("snowflake_deployment"))
                {
                    sanitisedCookiesList.Add("snowflake_deployment: EXISTS");
                }
                else if (cookie.Name.StartsWith("user-"))
                {
                    sanitisedCookiesList.Add("user-XXX: EXISTS");
                }
                else
                {
                    sanitisedCookiesList.Add("Unknown Cookie");
                    // As the cookie is the entire string (KEY=VALUE), split by = and take the first part
                    // We add this to the unknown list, so we can see what cookies we're not handling.
                    // This is then exposed in the ExtensiveDiagnosticTest log.
                    string cookieName = cookie.Name.Split('=')[0];
                    unknownCookiesList.Add(cookieName);

                }
            }
            // @todo we should instead pass this back, instead of logging here.
            string unknownCookies = String.Join(" | ", unknownCookiesList);
            // If unknownCookies is empty, return "N/A"
            if (unknownCookies.Length == 0)
            {
                unknownCookies = "N/A";
            }
            loggerExtensiveDiagnosticTest.Info("{0} Unknown Cookie Values: {1}", direction, unknownCookies);

            string sanitisedCookies = String.Join(" | ", sanitisedCookiesList);
            if (sanitisedCookies.Length == 0)
            {
                sanitisedCookies = "N/A";
            }
            loggerDiagnosticTest.Info("{0} Cookies: {1}", direction, sanitisedCookies);
        }

        #endregion
    }
}