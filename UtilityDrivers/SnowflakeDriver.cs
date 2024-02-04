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
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Newtonsoft.Json.Linq;
using NLog.Fluent;

namespace Snowflake.Powershell
{
    public class SnowflakeDriver
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static Logger loggerConsole = LogManager.GetLogger("Snowflake.Powershell.Console");
        private static Logger loggerDiagnosticTest = LogManager.GetLogger("Snowflake.Powershell.DiagnosticTest");
        private static Logger loggerExtensiveDiagnosticTest = LogManager.GetLogger("Snowflake.Powershell.ExtensiveDiagnosticTest");

        #region Snowsight Client Metadata

        public static string GetAccountAppEndpoints(string mainAppURL, string accountName)
        {
            return apiGET(
                mainAppURL, // "https://app.snowflake.com"
                String.Format("v0/validate-snowflake-url?url={0}", accountName),
                "*/*",
                String.Empty,
                String.Empty,
                String.Empty,
                String.Empty,
                String.Empty,
                String.Empty                
            ).Item1;
        }

        public static Tuple<string, string> OAuth_Start_GetSnowSightClientIDInDeployment(string mainAppURL, string appServerUrl, string accountUrl)
        {
            // When a user logs in via the Snowsight UI at https://app.snowflake.com, Snowflake redirects them to the `start-oauth/snowflake` endpoint:
            //
            // `https://apps-api.c1.us-east-999.aws.app.snowflake.com/start-oauth/snowflake?accountUrl=https%3A%2F%2Faccount12345us-east-999.snowflakecomputing.com&&state=%7B%22csrf%22%3A%22abcdefab%22%2C%22url%22%3A%22https%3A%2F%2Faccount12345.us-east-999.snowflakecomputing.com%22%2C%22windowId%22%3A%2200000000-0000-0000-0000-000000000000%22%2C%22browserUrl%22%3A%22https%3A%2F%2Fapp.snowflake.com%2F%22%7D`
            //
            // > Note that the `&&state` is not a typo, it is what Snowflake sends, so we send the same.
            //
            // This string is URL-encoded; its decoded form appears as follows:
            //
            // `https://apps-api.c1.us-east-999.aws.app.snowflake.com/start-oauth/snowflake?accountUrl=https://account12345us-east-999.snowflakecomputing.com&&state={"csrf":"abcdefab","url":"https://account12345.us-east-999.snowflakecomputing.com","windowId":"00000000-0000-0000-0000-000000000000","browserUrl":"https://app.snowflake.com/"}`
            //
            // Snowflake expects the following keys in the state object:
            //
            // 1. csrf - The csrf token from the earlier step
            // 2. url - The URL of the user's Snowflake instance (https://account12345.us-east-999.snowflakecomputing.com)
            // 3. windowId - This parameter is not needed, this parameter is the unique window ID of the user's web browser session, used to mitigate forgery risks.
            // 4. browserUrl - https://app.snowflake.com

            string csrf = "SnowflakePS";
            string stateParam = String.Format("{{\"csrf\":\"{0}\",\"url\":\"{1}\",\"browserUrl\":\"{2}\"}}", csrf, accountUrl, mainAppURL);

            Tuple<string, List<string>, HttpStatusCode> result = apiGET(
                appServerUrl,
                String.Format("start-oauth/snowflake?accountUrl={0}&&state={1}", HttpUtility.UrlEncode(accountUrl), HttpUtility.UrlEncode(stateParam)),
                "text/html", 
                String.Empty,
                String.Empty,
                String.Empty,
                String.Empty,
                String.Empty,
                String.Empty
            );

            // If we have the S8_SESSION cookie and oauth-nonce- cookie, then it's a success.
            // However, these could change in future (Snowflake adding/modifying cookies),
            // so we'll just check for the presence of at least one cookie.
            if (result.Item2.Count == 0)
            {
                throw new Exception("No cookies returned from start-oauth/snowflake");
            }

            string cookiesString = String.Join(";", result.Item2);

            return new Tuple<string, string>(result.Item1, cookiesString);
        }

        #endregion

        #region Snowsight Authentication

        public static string OAuth_Authenticate_GetMasterTokenFromCredentials(string accountUrl, string accountName, string userName, string password)
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
                "session/authenticate-request",
                "application/json",
                requestBody,
                "application/json");
        }

        public static string OAuth_Authorize_GetOAuthRedirectFromOAuthToken(string accountUrl, string clientID, string oAuthToken)
        {            
            string requestJSONTemplate = 
@"{{
    ""masterToken"": ""{0}"",
    ""clientId"": ""{1}""
}}";
            string requestBody = String.Format(requestJSONTemplate,
                oAuthToken,
                clientID);

            return apiPOST(
                accountUrl,
                "oauth/authorization-request",
                "application/json",
                requestBody,
                "application/json");
        }

        public static Tuple<string, string> OAuth_Complete_GetAuthenticationTokenFromOAuthRedirectToken(string appServerUrl, string accountUrl, string oAuthRedirectCode, string oAuthNonceCookie, string mainAppURL)
        {
            string csrf = "SnowflakePS";
            Cookie oauthNonceCookie = getOAuthNonceCookie(oAuthNonceCookie, "doesn't matter");
            string stateParam = String.Format("{{\"csrf\":\"{0}\",\"url\":\"{1}\",\"browserUrl\":\"{2}\", \"oauthNonce\":\"{3}\"}}", csrf, accountUrl, mainAppURL, oauthNonceCookie.Value);
            
            Tuple<string, List<string>, HttpStatusCode> result = apiGET(
                appServerUrl,
                String.Format("complete-oauth/snowflake?code={0}&state={1}", oAuthRedirectCode, HttpUtility.UrlEncode(stateParam)),
                "text/html",
                String.Empty,
                String.Empty,
                String.Empty,
                String.Empty,
                String.Empty,
                oAuthNonceCookie
            );

            if (result.Item3 == HttpStatusCode.OK)
            {
                // Get the cookie
                foreach (string cookie in result.Item2)
                {
                    if (cookie.StartsWith("user-") == true)
                    {
                        return new Tuple<string, string>(result.Item1, cookie);
                        // resultString = String.Format("{{\"authenticationCookie\": \"{0}\", \"resultPage\": \"{1}\"}}", cookie, Convert.ToBase64String(Encoding.UTF8.GetBytes(resultString)));
                    }
                }
            }

            // Default return
            return new Tuple<string, string>(String.Empty, String.Empty);
        }

        #endregion

        #region Classic UI Authentication

        public static string GetMasterTokenAndSessionTokenFromCredentials(string accountUrl, string accountName, string userName, string password)
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
                "application/json");
        }

        public static string GetMasterTokenAndSessionTokenFromSSOToken(string accountUrl, string accountName, string userName, string token, string proofKey)
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
                "application/json");
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
                "application/json");
        }

        #endregion

        #region Snowsight Org Metadata

        public static string GetOrganizationAndUserContext(AppUserContext authContext)
        {
            return apiGET(
                authContext.AppServerUrl,
                "bootstrap",
                "*/*",
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                authContext.AuthTokenSnowsight,
                String.Empty,
                String.Empty,
                String.Empty
            ).Item1;
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
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                authContext.AuthTokenSnowsight,
                String.Empty
            );
        }

        public static string GetWorksheet(
            AppUserContext authContext, 
            string worksheetID)
        {
            return apiGET(
                authContext.AppServerUrl,
                String.Format("v0/queries/{0}", worksheetID),
                "application/json",
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                authContext.AuthTokenSnowsight,
                String.Empty,
                String.Empty,
                String.Empty
            ).Item1;
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
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                authContext.AuthTokenSnowsight,
                String.Empty
            );
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
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                authContext.AuthTokenSnowsight,
                String.Empty
            );
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
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                authContext.AuthTokenSnowsight,
                String.Empty
            );
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
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                authContext.AuthTokenSnowsight,
                String.Empty
            );
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
                authContext.AuthTokenSnowsight,
                String.Empty
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
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                authContext.AuthTokenSnowsight,
                String.Empty
            );
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
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                authContext.AuthTokenSnowsight,
                String.Empty
            );
        }

        public static string GetDashboard(
            AppUserContext authContext, 
            string dashboardID)
        {
            return apiGET(
                authContext.AppServerUrl,
                String.Format("v0/folders/{0}", dashboardID),
                "application/json",
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                authContext.AuthTokenSnowsight,
                String.Empty,
                String.Empty,
                String.Empty
            ).Item1;
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
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                authContext.AuthTokenSnowsight,
                String.Empty
            );
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
                "application/x-www-form-urlencoded",
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                authContext.AuthTokenSnowsight,
                String.Empty
            );
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
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                authContext.AuthTokenSnowsight,
                String.Empty
            );
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
                authContext.AuthTokenSnowsight,
                String.Empty
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
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                authContext.AuthTokenSnowsight,
                String.Empty
            );
        }


        #endregion

        #region Snowsight Charts

        public static string GetChart(
            AppUserContext authContext,
            string worksheetID, string chartID)
        {
            return apiGET(
                authContext.AppServerUrl,
                String.Format("v0/queries/{0}/charts/{1}", worksheetID, chartID),
                "application/json",
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                authContext.AuthTokenSnowsight,
                String.Empty,
                String.Empty,
                String.Empty
            ).Item1;
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
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                authContext.AuthTokenSnowsight,
                String.Empty
            );
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
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                authContext.AuthTokenSnowsight,
                String.Empty
            );
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
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                authContext.AuthTokenSnowsight,
                String.Empty,
                roleToUse,
                String.Empty
            ).Item1;
        }

        public static string GetQueryProfile(
            AppUserContext authContext, 
            string queryID, string roleToUse)
        {
            return apiGET(
                authContext.AppServerUrl,
                String.Format("v0/session/request/monitoring/query-plan-data/{0}", queryID),
                "application/json",
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                authContext.AuthTokenSnowsight,
                String.Empty, 
                roleToUse,
                String.Empty
            ).Item1;
        }

        public static string GetQueryProfile(
            AppUserContext authContext, 
            string queryID, string roleToUse, int retryNumber)
        {
            return apiGET(
                authContext.AppServerUrl,
                String.Format("v0/session/request/monitoring/query-plan-data/{0}?jobRetryAttemptRank={1}", queryID, retryNumber),
                "application/json",
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                authContext.AuthTokenSnowsight,
                String.Empty, 
                roleToUse,
                String.Empty
            ).Item1;
        }

        #endregion

        #region Snowsight Filters

        public static string CreateOrUpdateFilter(
            AppUserContext authContext, 
            string filterKeyword, string filterConfiguration)
        {
            string requestBody = String.Format("paramConfig={0}",HttpUtility.UrlEncode(filterConfiguration));

            return apiPOST(
                authContext.AppServerUrl,
                String.Format("v0/organizations/{0}/param/{1}", authContext.OrganizationID, filterKeyword), 
                "application/json",
                requestBody,
                "application/x-www-form-urlencoded",
                authContext.ContextUserNameUrl,
                String.Format("{0}/", authContext.MainAppUrl), // "https://app.snowflake.com/",
                authContext.AuthTokenSnowsight,
                String.Empty
            );
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
                authContext.AuthTokenSnowsight,
                String.Empty
            );
        }

        #endregion

        #region Retrieval GET and POST API

        // private static string apiGET(string baseUrl, string restAPIUrl, string acceptHeader)
        // {
        //     return apiGET(baseUrl, restAPIUrl, acceptHeader, String.Empty, String.Empty, String.Empty, String.Empty, String.Empty);
        // }

        // private static string apiGET(string baseUrl, string restAPIUrl, string acceptHeader, List<string> cookies)
        // {
        //     return apiGET(baseUrl, restAPIUrl, acceptHeader, String.Empty, String.Empty, String.Empty, String.Empty, String.Empty);
        // }

        /// Returns Tuple<string, List<string>, HttpStatusCode>
        ///               ^^^^^^                                    results of the page
        ///                       ^^^^^^^^^^^^                      list of cookies
        ///                                     ^^^^^^^^^^^^^^^     HTTP Result Code
        private static Tuple<string, List<string>, HttpStatusCode> apiGET(string baseUrl, string restAPIUrl, string acceptHeader, string snowflakeContext, string referer, string snowSightAuthToken, string classicUIAuthToken, string roleToUse, string oauthNonceCookie)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {   
                HttpClientHandler httpClientHandler = new HttpClientHandler();
                httpClientHandler.UseCookies = true;
                httpClientHandler.CookieContainer = new CookieContainer();
                httpClientHandler.AllowAutoRedirect = true;
                // If customer certificates are not in trusted store, let's not fail
                httpClientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                using (HttpClient httpClient = new HttpClient(httpClientHandler))
                {
                    httpClient.Timeout = new TimeSpan(0, 1, 0);
                    Uri baseUri = new Uri(baseUrl);
                    httpClient.BaseAddress = baseUri;

                    httpClient.DefaultRequestHeaders.Add("User-Agent", String.Format("Snowflake Snowsight Extensions {0}", Assembly.GetExecutingAssembly().GetName().Version));

                    if (referer.Length > 0) 
                    {
                        httpClient.DefaultRequestHeaders.Referrer = new Uri(referer);
                    }
                    if (snowflakeContext.Length > 0)
                    {
                        httpClient.DefaultRequestHeaders.Add("x-snowflake-context", snowflakeContext);
                    }
                    if (snowSightAuthToken.Length > 0)
                    {
                        httpClientHandler.CookieContainer.Add(getAuthenticationCookie(snowSightAuthToken, baseUri.DnsSafeHost));
                    }
                    if (oauthNonceCookie.Length > 0)
                    {
                        httpClientHandler.CookieContainer.Add(getOAuthNonceCookie(oauthNonceCookie, baseUri.DnsSafeHost));
                    }
                    if (classicUIAuthToken.Length > 0)
                    {
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", String.Format("Snowflake Token=\"{0}\"", classicUIAuthToken));
                    }
                    if (roleToUse.Length > 0)
                    {
                        httpClient.DefaultRequestHeaders.Add("x-snowflake-role", roleToUse);
                    }

                    MediaTypeWithQualityHeaderValue accept = new MediaTypeWithQualityHeaderValue(acceptHeader);
                    if (httpClient.DefaultRequestHeaders.Accept.Contains(accept) == false)
                    {
                        httpClient.DefaultRequestHeaders.Accept.Add(accept);
                    }
                    // Cookies we ended up sending in the request.
                    List<string> requestCookieList = httpClientHandler.CookieContainer.GetAllCookies()
                        .Select(cookie => cookie.ToString())
                        .ToList();

                    HttpResponseMessage response = httpClient.GetAsync(restAPIUrl).Result;
                    stopWatch.Stop();

                    // extract all cookies from cookieContainer, into the list
                    var cookiesList = httpClientHandler.CookieContainer.GetCookies(baseUri)
                        .Select(cookie => cookie.ToString())
                        .ToList();
                    
                    ApiGetLogDiagnostic(response, restAPIUrl, requestCookieList, cookiesList, stopWatch.ElapsedMilliseconds);

                    if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Found)
                    {
                        string resultString = response.Content.ReadAsStringAsync().Result;
                        if (resultString == null) resultString = String.Empty;

                        logger.Info("GET {0}/{1} returned {2} ({3})\nRequest Headers:\n{4}Cookies:\n{5}\nResponse Length {6}:\n{7}", baseUrl, restAPIUrl, (int)response.StatusCode, response.ReasonPhrase, httpClient.DefaultRequestHeaders, String.Join('\n', cookiesList), resultString.Length, resultString);

                        // Workaround for the issue that the API now returns the client_id in the URL, and the apiGET
                        // function doesn't return the URL. So we need to return the final redirected URL as the result for
                        // `start-oauth/snowflake`.
                        if (restAPIUrl.Contains("start-oauth/snowflake"))
                        {
                            resultString = response.RequestMessage.RequestUri.ToString();
                        }

                        return new Tuple<string, List<string>, HttpStatusCode>(resultString, cookiesList.ToList(), response.StatusCode);
                    }
                    else
                    {
                        string resultString = response.Content.ReadAsStringAsync().Result;
                        if (resultString == null) resultString = String.Empty;
                        if (resultString.Length > 0)
                        {
                            logger.Info("GET {0}/{1} returned {2} ({3})\nRequest Headers:\n{4}Cookies:\n{5}\nResponse Length {6}:\n{7}", baseUrl, restAPIUrl, (int)response.StatusCode, response.ReasonPhrase, httpClient.DefaultRequestHeaders, String.Join('\n', cookiesList), resultString.Length, resultString);
                        }
                        else
                        {
                            logger.Error("GET {0}/{1} returned {2} ({3})\nRequest Headers:\n{4}Cookies:\n{5}", baseUrl, restAPIUrl, (int)response.StatusCode, response.ReasonPhrase, httpClient.DefaultRequestHeaders, String.Join('\n', cookiesList));
                        }

                        if (response.StatusCode == HttpStatusCode.Unauthorized || 
                            response.StatusCode == HttpStatusCode.Forbidden)
                        {
                            loggerConsole.Error("GET {0}/{1} returned {2} ({3})", baseUrl, restAPIUrl, (int)response.StatusCode, response.ReasonPhrase);
                        }

                        return new Tuple<string, List<string>, HttpStatusCode>(String.Empty, cookiesList.ToList(), response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("GET {0}/{1} threw {2} ({3})", baseUrl, restAPIUrl, ex.Message, ex.Source);
                logger.Error(ex);

                loggerConsole.Error("GET {0}/{1} threw {2} ({3})", baseUrl, restAPIUrl, ex.Message, ex.Source);

                return new Tuple<string, List<string>, HttpStatusCode>(String.Empty, new List<string>(0), HttpStatusCode.InternalServerError);
            }
            finally
            {
                stopWatch.Stop();
                logger.Info("GET {0}/{1} took {2:c} ({3} ms)", baseUrl, restAPIUrl, stopWatch.Elapsed.ToString("c"), stopWatch.ElapsedMilliseconds);
            }
        }

        private static string apiPOST(string baseUrl, string restAPIUrl, string acceptHeader, string requestBody, string requestTypeHeader)
        {
            return apiPOST(baseUrl, restAPIUrl, acceptHeader, requestBody, requestTypeHeader, String.Empty, String.Empty, String.Empty, String.Empty);
        }

        private static string apiPOST(string baseUrl, string restAPIUrl, string acceptHeader, string requestBody, string requestTypeHeader, string snowflakeContext, string referer, string snowSightAuthToken, string classicUIAuthToken)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                HttpClientHandler httpClientHandler = new HttpClientHandler();
                httpClientHandler.UseCookies = true;
                httpClientHandler.CookieContainer = new CookieContainer();
                httpClientHandler.AllowAutoRedirect = false;
                // If customer certificates are not in trusted store, let's not fail
                httpClientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                using (HttpClient httpClient = new HttpClient(httpClientHandler))
                {
                    httpClient.Timeout = new TimeSpan(0, 1, 0);
                    Uri baseUri = new Uri(baseUrl);
                    httpClient.BaseAddress = baseUri;

                    httpClient.DefaultRequestHeaders.Add("User-Agent", String.Format("Snowflake Snowsight Extensions {0}", Assembly.GetExecutingAssembly().GetName().Version));

                    if (referer.Length > 0) 
                    {
                        httpClient.DefaultRequestHeaders.Referrer = new Uri(referer);
                    }
                    if (snowflakeContext.Length > 0)
                    {
                        httpClient.DefaultRequestHeaders.Add("x-snowflake-context", snowflakeContext);
                    }
                    if (snowSightAuthToken.Length > 0)
                    {
                        httpClientHandler.CookieContainer.Add(getAuthenticationCookie(snowSightAuthToken, baseUri.DnsSafeHost));
                    }
                    if (classicUIAuthToken.Length > 0)
                    {
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", String.Format("Snowflake Token=\"{0}\"", classicUIAuthToken));
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

                    // Cookies we ended up sending in the request.
                    List<string> requestCookieList = httpClientHandler.CookieContainer.GetAllCookies()
                        .Select(cookie => cookie.ToString())
                        .ToList();

                    HttpResponseMessage response = httpClient.PostAsync(restAPIUrl, content).Result;
                    stopWatch.Stop();

                    List<string> cookiesList = new List<string>(); 
                    if (response.Headers.Contains("Set-Cookie") == true)
                    {
                        cookiesList = response.Headers.GetValues("Set-Cookie").ToList();
                    }

                    ApiPostLogDiagnostic(response, restAPIUrl, requestCookieList, cookiesList, stopWatch.ElapsedMilliseconds);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultString = response.Content.ReadAsStringAsync().Result;
                        if (resultString == null) resultString = String.Empty;

                        logger.Info("POST {0}/{1} returned {2} ({3})\nRequest Headers:\n{4}Cookies:\n{5}\nRequest:\n{6}\nResponse Length {7}:\n{8}", baseUrl, restAPIUrl, (int)response.StatusCode, response.ReasonPhrase, httpClient.DefaultRequestHeaders, String.Join('\n', cookiesList), requestBody, resultString.Length, resultString);

                        return resultString;
                    }
                    else
                    {
                        string resultString = response.Content.ReadAsStringAsync().Result;
                        if (resultString == null) resultString = String.Empty;
                        if (resultString.Length > 0)
                        {
                            logger.Error("POST {0}/{1} returned {2} ({3})\nRequest Headers:\n{4}Cookies:\n{5}\nRequest:\n{6}\nResponse Length {7}:\n{8}", baseUrl, restAPIUrl, (int)response.StatusCode, response.ReasonPhrase, httpClient.DefaultRequestHeaders, String.Join('\n', cookiesList), requestBody, resultString.Length, resultString);
                        }
                        else
                        {
                            logger.Error("POST {0}/{1} returned {2} ({3})\nRequest Headers:\n{4}Cookies:\n{5}\nRequest:\n{6}", baseUrl, restAPIUrl, (int)response.StatusCode, response.ReasonPhrase, httpClient.DefaultRequestHeaders, String.Join('\n', cookiesList), requestBody);
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

        private static string apiDELETE(string baseUrl, string restAPIUrl, string acceptHeader)
        {
            return apiDELETE(baseUrl, restAPIUrl, acceptHeader, String.Empty, String.Empty, String.Empty, String.Empty);
        }

        private static string apiDELETE(string baseUrl, string restAPIUrl, string acceptHeader, string snowflakeContext, string referer, string snowSightAuthToken, string classicUIAuthToken)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                HttpClientHandler httpClientHandler = new HttpClientHandler();
                httpClientHandler.UseCookies = true;
                httpClientHandler.CookieContainer = new CookieContainer();
                httpClientHandler.AllowAutoRedirect = false;

                using (HttpClient httpClient = new HttpClient(httpClientHandler))
                {
                    httpClient.Timeout = new TimeSpan(0, 1, 0);
                    Uri baseUri = new Uri(baseUrl);
                    httpClient.BaseAddress = baseUri;

                    httpClient.DefaultRequestHeaders.Add("User-Agent", String.Format("Snowflake Snowsight Extensions {0}", Assembly.GetExecutingAssembly().GetName().Version));

                    if (referer.Length > 0) 
                    {
                        httpClient.DefaultRequestHeaders.Referrer = new Uri(referer);
                    }
                    if (snowflakeContext.Length > 0)
                    {
                        httpClient.DefaultRequestHeaders.Add("x-snowflake-context", snowflakeContext);
                    }
                    if (snowSightAuthToken.Length > 0)
                    {
                        httpClientHandler.CookieContainer.Add(getAuthenticationCookie(snowSightAuthToken, baseUri.DnsSafeHost));
                    }
                    if (classicUIAuthToken.Length > 0)
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

        private static Cookie getAuthenticationCookie(string snowSightAuthToken, string domain)
        {
            // Example cookie:
            //      user-646f646965766963683a3a68747470733a2f2f6177735f636173312e736e6f77666c616b65636f6d707574696e672e636f6dcbf29ce484222325=CFBrZWxTQ0U3sDQ0u1zOfZ39kTXK86vhH3K2wYqrYUhA12WKg0q8XmVRmqO65eJvRV3gsLUE4gQI3oDnBaXSznunNYRssRVY2H6w3k9MHenZZ6mvGI9br8da8Ah0d3X_W7E__p7Y41vrt7_eWRB02Ie3eUwWhPc_kTGbJZ7oxw==; Path=/; Expires=Wed, 12 May 2021 02:18:33 GMT; Max-Age=2419200; HttpOnly; Secure; SameSite=Lax
            // OR better formatted
            //      user-646f646965766963683a3a68747470733a2f2f6177735f636173312e736e6f77666c616b65636f6d707574696e672e636f6dcbf29ce484222325=CFBrZWxTQ0U3sDQ0u1zOfZ39kTXK86vhH3K2wYqrYUhA12WKg0q8XmVRmqO65eJvRV3gsLUE4gQI3oDnBaXSznunNYRssRVY2H6w3k9MHenZZ6mvGI9br8da8Ah0d3X_W7E__p7Y41vrt7_eWRB02Ie3eUwWhPc_kTGbJZ7oxw==; 
            //      Path=/; 
            //      Expires=Wed, 12 May 2021 02:18:33 GMT; 
            //      Max-Age=2419200; 
            //      HttpOnly; 
            //      Secure; 
            //      SameSite=Lax
            Cookie cookie = new Cookie();
            cookie.Domain = domain;

            string[] cookieComponents = snowSightAuthToken.Split(';', StringSplitOptions.TrimEntries);
            foreach (string cookieComponent in cookieComponents)
            {
                string[] cookieComponentTokens = cookieComponent.Split('=');
                if (cookieComponentTokens.Length >= 2)
                {
                    string authCookieComponentName = cookieComponentTokens[0];
                    string authCookieComponentValue = cookieComponentTokens[1];
                    switch (authCookieComponentName)
                    {
                        case "Path":
                            cookie.Path = authCookieComponentValue;
                            break;
                        
                        case "Expires":
                            DateTime expirationDateTime = DateTime.MinValue;
                            if (DateTime.TryParse(authCookieComponentValue, out expirationDateTime) == true)
                            {
                                    cookie.Expires = expirationDateTime;
                            }
                            break;

                        case "HttpOnly":
                            cookie.HttpOnly = true;
                            break;

                        case "Secure":
                            cookie.Secure = true;
                            break;

                        default:
                            if (authCookieComponentName.StartsWith("user-") == true)
                            {
                                cookie.Name = authCookieComponentName;
                                // There is an = at the end of the value, so it's just best to grab everything after the first =
                                cookie.Value = cookieComponent.Substring(authCookieComponentName.Length + 1);
                            }
                            break;
                    }
                }
            }

            if (cookie.Name.Length == 0)
            {
                throw new ArgumentException("No cookie name was found in the authentication token");
            }

            return cookie;
        }

        private static Cookie getOAuthNonceCookie(string oAuthNonceCookie, string domain)
        {
            // Example cookie:
            //      oauth-nonce-1qK1Es6m=1qK1Es6mjeZ; Path=/; Max-Age=3600; HttpOnly; Secure; SameSite=Lax
            // OR better formatted
            //      oauth-nonce-1qK1Es6m=1qK1Es6mjeZ; 
            //      Path=/; 
            //      Max-Age=3600; 
            //      HttpOnly; 
            //      Secure; 
            //      SameSite=Lax
            Cookie cookie = new Cookie();
            cookie.Domain = domain;

            string[] cookieComponents = oAuthNonceCookie.Split(';', StringSplitOptions.TrimEntries);
            foreach (string cookieComponent in cookieComponents)
            {
                string[] cookieComponentTokens = cookieComponent.Split('=');
                if (cookieComponentTokens.Length >= 2)
                {
                    string authCookieComponentName = cookieComponentTokens[0];
                    string authCookieComponentValue = cookieComponentTokens[1];
                    switch (authCookieComponentName)
                    {
                        case "Path":
                            cookie.Path = authCookieComponentValue;
                            break;
                        
                        case "Expires":
                            DateTime expirationDateTime = DateTime.MinValue;
                            if (DateTime.TryParse(authCookieComponentValue, out expirationDateTime) == true)
                            {
                                    cookie.Expires = expirationDateTime;
                            }
                            break;

                        case "HttpOnly":
                            cookie.HttpOnly = true;
                            break;

                        case "Secure":
                            cookie.Secure = true;
                            break;

                        default:
                            if (authCookieComponentName.StartsWith("oauth-nonce-") == true)
                            {
                                cookie.Name = authCookieComponentName;
                                cookie.Value = cookieComponent.Substring(authCookieComponentName.Length + 1);
                            }
                            break;
                    }
                }
            }

            if (cookie.Name.Length == 0)
            {
                throw new ArgumentException("No cookie name was found in the oauth nonce token");
            }

            return cookie;
        }

        #endregion
        
        private static void ApiGetLogDiagnostic(HttpResponseMessage response, string restApiUrl,
            List<string> requestCookieList, List<string> responseCookiesList, long stopWatchElapsedMilliseconds)
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
                loggerDiagnosticTest.Info("Response: {0} ({1}) in {2}ms", (int)response.StatusCode, response.ReasonPhrase, stopWatchElapsedMilliseconds);
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
                    account = JSONHelper.getStringValueFromJToken(jsonResponse, "account") == String.Empty ? "N/A" : "EXISTS";
                    appServerUrl = JSONHelper.getStringValueFromJToken(jsonResponse, "appServerUrl") == String.Empty ? "N/A" : "EXISTS";
                    region = JSONHelper.getStringValueFromJToken(jsonResponse, "region") == String.Empty ? "N/A" : "EXISTS";
                    url = JSONHelper.getStringValueFromJToken(jsonResponse, "url") == String.Empty ? "N/A" : "EXISTS";
                }
                catch (Exception ex)
                {
                    // @todo We could use ex.Message here, but it might contain sensitive information.
                    loggerDiagnosticTest.Error("Failed to parse JSON response");
                }
                loggerDiagnosticTest.Info("Response Body: valid: {0} | account: {1} | appServerUrl: {2} | region: {3} | url: {4}", valid, account, appServerUrl, region, url);
            }
            else if (restApiUrl.StartsWith("start-oauth/snowflake"))
            {
                loggerDiagnosticTest.Info("GET /start-oauth/snowflake");
                loggerDiagnosticTest.Info("Response: {0} ({1}) in {2}ms", (int)response.StatusCode, response.ReasonPhrase, stopWatchElapsedMilliseconds);

                // We only care if if the redirect URL contains oauth/authorize, as that is the current final URL
                bool correctRedirectUrl = finalUrl.Contains("oauth/authorize");

                loggerDiagnosticTest.Info("Redirected to oauth/authorize: {0}", correctRedirectUrl);
            }
            else if (restApiUrl.StartsWith("complete-oauth/snowflake"))
            {
                loggerDiagnosticTest.Info("GET /complete-oauth/snowflake");
                loggerDiagnosticTest.Info("Response: {0} ({1}) in {2}ms", (int)response.StatusCode, response.ReasonPhrase, stopWatchElapsedMilliseconds);
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
                loggerDiagnosticTest.Info("Response: {0} ({1}) in {2}ms", (int)response.StatusCode, response.ReasonPhrase, stopWatchElapsedMilliseconds);
                // Response Body: BuildVersion: 240125-9-f8441ccb37, user.id: EXISTS
                string resultString = response.Content.ReadAsStringAsync().Result;
                // BuildVersion is from the JSON
                string buildVersion = "N/A";
                string userId = "N/A";
                try
                {
                    JObject jsonResponse = JObject.Parse(resultString);
                    buildVersion = JSONHelper.getStringValueFromJToken(jsonResponse, "BuildVersion") == String.Empty ? "N/A" : "EXISTS";
                    userId = JSONHelper.getStringValueFromJToken(jsonResponse["User"], "id") == String.Empty ? "N/A" : "EXISTS";
                }
                catch (Exception ex)
                {
                    // @todo We could use ex.Message here, but it might contain sensitive information.
                    loggerExtensiveDiagnosticTest.Error("Failed to parse JSON response | error: {0}", ex.Message);
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
            LogSanitiseCookies("Request", requestCookieList);
            LogSanitiseCookies("Response", responseCookiesList);
        }

        private static void ApiPostLogDiagnostic(HttpResponseMessage response, string restApiUrl, List<string> requestCookieList, List<string> responseCookieList, long stopWatchElapsedMilliseconds)
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
            LogSanitiseCookies("Request", requestCookieList);
            LogSanitiseCookies("Response", responseCookieList);
        }

        private static void LogSanitiseCookies(string direction, IEnumerable<string> cookiesList)
        {
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
            foreach (string cookie in cookiesList)
            {
                if (cookie.StartsWith("oauth-nonce-"))
                {
                    sanitisedCookiesList.Add("oauth-nonce-XXX: EXISTS");
                }
                else if (cookie.StartsWith("S8_SESSION"))
                {
                    sanitisedCookiesList.Add("S8_SESSION: EXISTS");
                }
                else if (cookie.StartsWith("csrf-"))
                {
                    sanitisedCookiesList.Add("csrf-XXX: EXISTS");
                }
                else if (cookie.StartsWith("snowflake_deployment"))
                {
                    sanitisedCookiesList.Add("snowflake_deployment: EXISTS");
                }
                else if (cookie.StartsWith("user-"))
                {
                    sanitisedCookiesList.Add("user-XXX: EXISTS");
                }
                else
                {
                    sanitisedCookiesList.Add("Unknown Cookie");
                    // As the cookie is the entire string (KEY=VALUE), split by = and take the first part
                    // We add this to the unknown list, so we can see what cookies we're not handling.
                    // This is then exposed in the ExtensiveDiagnosticTest log.
                    string cookieName = cookie.Split('=')[0];
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
    }
}