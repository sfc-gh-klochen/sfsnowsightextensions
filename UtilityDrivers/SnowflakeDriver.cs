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

namespace Snowflake.Powershell
{
    public class SnowflakeDriver
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static Logger loggerConsole = LogManager.GetLogger("Snowflake.Powershell.Console");

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
            string csrf = "SnowflakePS";
            string stateParam = String.Format("{{\"isSecondaryUser\":false,\"csrf\":\"{0}\",\"url\":\"{1}\",\"browserUrl\":\"{2}\"}}", csrf, accountUrl, mainAppURL);
            
            Tuple<string, List<string>, HttpStatusCode> result = apiGET(
                appServerUrl,
                String.Format("start-oauth/snowflake?accountUrl={0}&state={1}", HttpUtility.UrlEncode(accountUrl), HttpUtility.UrlEncode(stateParam)),
                "text/html", 
                String.Empty,
                String.Empty,
                String.Empty,
                String.Empty,
                String.Empty,
                String.Empty
            );

            if (result.Item3 == HttpStatusCode.Redirect)
            {
                // Get the cookie
                foreach (string cookie in result.Item2)
                {
                    if (cookie.StartsWith("oauth-nonce-") == true)
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
            string stateParam = String.Format("{{\"isSecondaryUser\":false,\"csrf\":\"{0}\",\"url\":\"{1}\",\"browserUrl\":\"{2}\", \"oauthNonce\":\"{3}\"}}", csrf, accountUrl, mainAppURL, oauthNonceCookie.Value);
            
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
        ""AUTHENTICATOR"": ""externalbrowser"",
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
        ""AUTHENTICATOR"": ""externalbrowser"",
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
            string optionsParam = "{\"sort\":{\"col\":\"viewed\",\"dir\":\"desc\"},\"limit\":1000,\"owner\":null,\"types\":[\"query\"],\"showNeverViewed\":\"if-invited\"}";

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
            string optionsParam = "{\"sort\":{\"col\":\"viewed\",\"dir\":\"desc\"},\"limit\":1000,\"owner\":null,\"types\":[\"dashboard\"],\"showNeverViewed\":\"if-invited\"}";

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
            string optionsParam = "{\"sort\":{\"col\":\"viewed\",\"dir\":\"desc\"},\"limit\":1000,\"owner\":null,\"types\":[\"folder\"],\"showNeverViewed\":\"if-invited\"}";

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

                    HttpResponseMessage response = httpClient.GetAsync(restAPIUrl).Result;
                    
                    IEnumerable<string> cookiesList = new List<string>(); 
                    if (response.Headers.Contains("Set-Cookie") == true)
                    {
                        cookiesList = response.Headers.GetValues("Set-Cookie"); 
                    }

                    if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Found)
                    {
                        string resultString = response.Content.ReadAsStringAsync().Result;
                        if (resultString == null) resultString = String.Empty;

                        logger.Info("GET {0}/{1} returned {2} ({3})\nRequest Headers:\n{4}Cookies:\n{5}\nResponse Length {6}:\n{7}", baseUrl, restAPIUrl, (int)response.StatusCode, response.ReasonPhrase, httpClient.DefaultRequestHeaders, String.Join('\n', cookiesList), resultString.Length, resultString);

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

                    HttpResponseMessage response = httpClient.PostAsync(restAPIUrl, content).Result;

                    IEnumerable<string> cookiesList = new List<string>(); 
                    if (response.Headers.Contains("Set-Cookie") == true)
                    {
                        cookiesList = response.Headers.GetValues("Set-Cookie"); 
                    }

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
   }
}