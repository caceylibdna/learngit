#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Alex Meau
*   Creation Date: 2006/01/17
*
* Current Status
*       $Revision: 1.10 $
*           $Date: 2014/07/16 23:02:43 $
*         $Author: ameau $
*
*******************************************************************
*
* Copyright (c) 2007-2008 BDNA Corporation.
* All Rights Reserved. BDNA products and services are protected
* by the following U.S. patent: #6,988,134. BDNA is trademark of 
* BDNA Corporation.
*
* ******BDNA CONFIDENTIAL******
*
* The following code was developed and is owned by BDNA Corporation
* This code is confidential and may contain
* trade secrets. The code must not be distributed to any party
* outside of BDNA Corporation Inc. without written
* permission from BDNA.  The code may be covered by patents,
* patents pending, or patents applied for in the US or elsewhere.
*
******************************************************************/
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Management;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {
    public class WindowsOracleInstanceDBOptionsStaticScript : ICollectionScriptRuntime {
        /// <summary>
        /// Perform collection task specific processing.
        /// </summary>
        /// 
        /// <param name="taskId">Database assigned task Id.</param>
        /// <param name="cleId">Database Id of owning Collection Engine.</param>
        /// <param name="elementId">Database Id of element being collected.</param>
        /// <param name="databaseTimestamp">Database relatvie task dispatch timestamp.</param>
        /// <param name="localTimestamp">Local task dispatch timestamp.</param>
        /// <param name="attributes">Map of attribute names to Id for attributes being collected.</param>
        /// <param name="scriptParameters">Collection script specific parameters (name/value pairs).</param>
        /// <param name="connection">Connection script results (null if this script does not
        ///     require a remote host connection).</param>
        /// <param name="tftpDispatcher">Dispatcher for TFTP transfer requests.</param>
        /// 
        /// <returns>Collection results.</returns>
        public CollectionScriptResults ExecuteTask(
                long taskId,
                long cleId,
                long elementId,
                long databaseTimestamp,
                long localTimestamp,
                IDictionary<string, string> attributes,
                IDictionary<string, string> scriptParameters,
                IDictionary<string, object> connection,
                string tftpPath,
                string tftpPath_login,
                string tftpPath_password,
                ITftpDispatcher tftpDispatcher) {

            m_taskId = taskId.ToString();
            m_cleId = cleId;
            m_elementId = elementId;
            m_databaseTimestamp = databaseTimestamp;
            m_localTimestamp = localTimestamp;
            m_attributes = attributes;
            m_scriptParameters = scriptParameters;
            m_connection = connection;
            string strOracleHome = null, strSchemaName = null, strSchemaPassword = null;

            m_executionTimer = Stopwatch.StartNew();
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script WindowsOracleInstanceDBOptionsStaticScript.",
                                  m_taskId);

            try {
                // Check ManagementScope CIMV
                ManagementScope cimvScope = null;
                if (connection == null) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WindowsOracleInstanceDBOptionsStaticScript is null.",
                                          m_taskId);
                } else if (!connection.ContainsKey("cimv2")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV namespace is not present in connection object.",
                                          m_taskId);
                } else {
                    cimvScope = connection[@"cimv2"] as ManagementScope;
                    if (!cimvScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed",
                                              m_taskId);
                    }
                }

                if (!scriptParameters.ContainsKey("OracleHome")) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"OracleHome\".",
                                          m_taskId);
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                } else {
                    strOracleHome = scriptParameters["OracleHome"].Trim();
                    if (strOracleHome.EndsWith(@"\")) {
                        strOracleHome = strOracleHome.Substring(0, strOracleHome.Length - 1);
                    }
                }

                if (!scriptParameters.ContainsKey("version")) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"version\".",
                                          m_taskId);
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                } else {
                    m_strVersion = scriptParameters["version"].Trim();
                }

                if (!connection.ContainsKey("schemaName")) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"schemaName\".",
                                          m_taskId);
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                } else {
                    strSchemaName = connection["schemaName"].ToString().Trim();
                }

                if (!connection.ContainsKey("schemaPassword")) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"schemaPassword\".",
                                          m_taskId);
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                } else {
                    strSchemaPassword = connection["schemaPassword"].ToString().Trim();
                }


                if (ResultCodes.RC_SUCCESS == resultCode) {
                    // Check Remote Process Temp Directory
                    if (!connection.ContainsKey("TemporaryDirectory")) {
                        connection["TemporaryDirectory"] = @"%TMP%";
                    } else {
                        if (!m_connection[@"TemporaryDirectory"].Equals(@"%TMP%")) {
                            if (!Lib.ValidateDirectory(m_taskId, m_connection[@"TemporaryDirectory"].ToString(), cimvScope)) {
                                Lib.Logger.TraceEvent(TraceEventType.Error,
                                                      0,
                                                      "Task Id {0}: Temporary directory {1} is not valid.",
                                                      m_taskId,
                                                      connection[@"TemporaryDirectory"].ToString());
                                resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;  //@TODO: change to RC_TEMP_DIRECTORY_NOT_EXIST
                            } else {
                                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                      0,
                                                      "Task Id {0}: Temporary directory {1} has been validated.",
                                                      m_taskId,
                                                      connection[@"TemporaryDirectory"].ToString());
                            }
                        }
                    }
                }

                if (resultCode == ResultCodes.RC_SUCCESS) {
                    string strTempDir = connection["TemporaryDirectory"].ToString().Trim();
                    if (strTempDir.EndsWith(@"\")) {
                        strTempDir = strTempDir.Substring(0, strTempDir.Length - 1);
                    }
                    string strBatchFileContent = buildBatchFile(strTempDir, strOracleHome, strSchemaName, strSchemaPassword);
                    StringBuilder stdoutData = new StringBuilder();
                    using (IRemoteProcess rp = RemoteProcess.ExecuteBatchFile
                        (m_taskId, cimvScope, strBatchFileContent, connection, tftpPath, tftpPath_login, tftpPath_password, tftpDispatcher)) {
                        //This method will block until the entire remote process operation completes.
                        resultCode = rp.Launch();
                        Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                              0,
                                              "Task Id {0}: Remote process operation completed with result code {1}.",
                                              m_taskId,
                                              resultCode.ToString());

                        if (resultCode == ResultCodes.RC_SUCCESS) {
                            stdoutData.Append(rp.Stdout);
                            if (rp.Stdout != null && rp.Stdout.Length > 0) {
                                if (rp.Stdout.ToString().Contains("ORA-01017")) {
                                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                                          0,
                                                          "Task Id {0}: Oracle L3 credential is invalid.\nResult code changed to RC_PROCESSING_EXCEPTION.\nSTDOUT/STDERR:\n{1}",
                                                          m_taskId,
                                                          rp.Stdout.ToString());
                                    resultCode = ResultCodes.RC_HOST_CONNECT_FAILED;
                                } else if (rp.Stdout.ToString().Contains("ERROR-")) {
                                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                                          0,
                                                          "Task Id {0}: Batch file execution exception.\nResult code changed to RC_PROCESSING_EXCEPTION.\nSTDOUT/STDERR:\n{1}",
                                                          m_taskId,
                                                          rp.Stdout.ToString());
                                    resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                                } else if (!rp.Stdout.ToString().Contains(@"BDNA")) {
                                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                                          0,
                                                          "Task Id {0}: SQLPLUS exception, no proper data returned.\nResult code changed to RC_PROCESSING_EXCEPTION.\nSTDOUT/STDERR:\n{1}",
                                                          m_taskId,
                                                          rp.Stdout.ToString());
                                    resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                                } else if (!rp.Stdout.ToString().Contains(@"Execution completed")) {
                                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                                          0,
                                                          "Task Id {0}: Exception with batch return data.\nData returned is shorter than expected, possibly due to transfer failure.\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                                          m_taskId);
                                    resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                                }
                            } else {
                                Lib.Logger.TraceEvent(TraceEventType.Error,
                                                      0,
                                                      "Task Id {0}: No data returned.\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                                      m_taskId);
                                resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                            }
                        } else {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Remote execution error.\nSTDOUT.STDERR:\n{1}",
                                                  m_taskId,
                                                  rp.Stdout.ToString());
                        }
                    }
                    if (resultCode == ResultCodes.RC_SUCCESS && stdoutData.Length > 0) {
                        foreach (KeyValuePair<string, QueryTableEntry> entry in s_queryTable) {
                            entry.Value.ResultHandler(this, entry.Key, stdoutData.ToString());
                        }
                        foreach (KeyValuePair<string, string> kvp in m_collectedData) {
                            this.BuildDataRow(kvp.Key, kvp.Value);
                        }
                        //Console.WriteLine(CollectedData[@"dbaFeatures"]);
                    }
                }
            } catch (Exception ex) {
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleInstanceDBOptionsStaticScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleInstanceDBOptionsStaticScript.  Elapsed time {1}.\n{2}",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script WindowsOracleInstanceDBOptionsStaticScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, m_dataRow.ToString());
        }

        /// <summary>
        /// Save collected data
        /// </summary>
        /// <param name="attributeName">attribute name</param>
        /// <param name="collectedData">collected data</param>
        public void SaveCollectedData(string attributeName, string collectedData) {
            m_collectedData[attributeName] = collectedData;
        }

        public IDictionary<string, string> CollectedData {
            get {
                return m_collectedData;
            }
        }

        /// <summary>
        /// Generate a new data row for a collected attribute and add
        /// it to the aggregate data row buffer.
        /// </summary>
        /// 
        /// <param name="attributeName">Name of attribute collected.</param>
        /// <param name="collectedData">Collected data value.  Null is allowed.</param>
        public void BuildDataRow(string attributeName, string collectedData) {
            if (!m_attributes.ContainsKey(attributeName)) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Attribute \"{1}\" missing from attributeSet.",
                                      m_taskId,
                                      attributeName);
            } else if (string.IsNullOrEmpty(collectedData)) {
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Script completed sucessfully with no data to return.",
                                      m_taskId);
            } else {
                m_dataRow.Append(m_elementId).Append(',')
                         .Append(m_attributes[attributeName]).Append(',')
                         .Append(m_scriptParameters[@"CollectorId"]).Append(',')
                         .Append(m_taskId).Append(',')
                         .Append(m_databaseTimestamp + m_executionTimer.ElapsedMilliseconds).Append(',')
                         .Append(attributeName).Append(',')
                         .Append(BdnaDelimiters.BEGIN_TAG).Append(collectedData).Append(BdnaDelimiters.END_TAG);
            }
        }

        /// <summary>
        /// Build temporary batch file.
        /// </summary>
        /// <param name="strTempDir"></param>
        private string buildBatchFile(string strTempDir, string strOracleHome, string strSchemaName, string strSchemaPassword) {
            StringBuilder strBatchFile = new StringBuilder();
            if (!String.IsNullOrEmpty(strTempDir)) {
                if (strTempDir.EndsWith(@"\")) {
                    strTempDir = strTempDir.Substring(0, strTempDir.Length - 1);
                }
            }
            strBatchFile.AppendLine(@"@ECHO OFF");
            strBatchFile.AppendLine(@"IF (%1) == () GOTO :ERROR_NULL_PARAMETER");
            strBatchFile.Append(@"IF EXIST ").Append(strTempDir)
                .AppendLine(@"\%1 GOTO :ERROR_CACHE_DIR_EXISTS");
            strBatchFile.AppendLine();
            strBatchFile.Append(@"MKDIR ").Append(strTempDir).AppendLine(@"\%1");
            strBatchFile.Append(@"SET TNS_ADMIN=").Append(strTempDir).AppendLine(@"\%1");
            for (int i = 0; i < 4; i++) {
                strBatchFile.Append(@"ECHO QUIT >> ").Append(strTempDir).Append(@"\%1").AppendLine(@"\CMDLINE.TXT");
            }
            strBatchFile.Append(@"ECHO # EMPTY FILE >> ")
                        .Append(strTempDir).Append(@"\%1").AppendLine(@"\SQLNET.ORA");
            strBatchFile.AppendLine();
            foreach (KeyValuePair<string, QueryTableEntry> entry in s_queryTable) {
                string strQuery = entry.Value.QueryString.ToString();
                string strName = entry.Key;
                Regex regex = entry.Value.regex;

                if ((!string.IsNullOrEmpty(strQuery)) && (regex.IsMatch(m_strVersion))) {
                    strBatchFile.Append("ECHO PROMPT " + strName + @"_BEGIN___;")
                                .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
                    strBatchFile.Append("ECHO ");
                    strBatchFile.Append(strQuery.Trim()
                        .Replace("<", "^<").Replace(">", "^>").Replace("|", @"^|")
                        .Replace("&", "^&").Replace("%", "%%"));
                    if (!strQuery.EndsWith(";")) {
                        strBatchFile.Append(";");
                    }
                    strBatchFile.Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
                    strBatchFile.Append("ECHO PROMPT " + @"___" + strName + @"_END;")
                                .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
                }
            }
            strBatchFile.Append(@"ECHO QUIT; >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
            strBatchFile.AppendLine();
            strBatchFile.Append(@"CD ").Append(strTempDir).AppendLine(@"\%1");
            strBatchFile.AppendLine();
            strBatchFile.Append(strOracleHome.Trim()).Append(@"\BIN\SQLPLUS ")
                .Append(strSchemaName).Append(@"/").Append(strSchemaPassword).Append(@" ")
                .Append(@"@").Append(@"QUERY.SQL ")
                .Append(@" 0<").AppendLine(@"CMDLINE.TXT 2>&1");
            strBatchFile.AppendLine();

            strBatchFile.Append(@"CD ").AppendLine(strTempDir);
            strBatchFile.Append(@"DEL ").Append(strTempDir).AppendLine(@"\%1\CMDLINE.TXT");
            strBatchFile.Append(@"DEL ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
            strBatchFile.Append(@"DEL ").Append(strTempDir).Append(@"\%1").AppendLine(@"\SQLNET.ORA");
            strBatchFile.Append(@"RMDIR ").Append(strTempDir).AppendLine(@"\%1");
            strBatchFile.AppendLine();
            strBatchFile.AppendLine(@"GOTO :SUCCESS");
            strBatchFile.AppendLine();
            strBatchFile.AppendLine(@":ERROR_NULL_PARAMETER");
            strBatchFile.AppendLine(@"ECHO ERROR- null batch parameter.");
            strBatchFile.AppendLine(@"GOTO :END");
            strBatchFile.AppendLine();
            strBatchFile.AppendLine(@":ERROR_CACHE_DIR_EXISTS");
            strBatchFile.AppendLine(@"ECHO ERROR- cache directory exists.");
            strBatchFile.AppendLine(@"GOTO :END");
            strBatchFile.AppendLine();
            strBatchFile.AppendLine(@":SUCCESS");
            strBatchFile.AppendLine(@"ECHO Execution completed.");
            strBatchFile.AppendLine();
            strBatchFile.AppendLine(@":END");
            return strBatchFile.ToString();
        }

        /// <summary>
        /// This is a helper method that will return first group that matches given expression
        /// </summary>
        /// 
        /// <param name="line">single line to be matched</param>
        /// <param name="regex">Regular expression to use.</param>
        /// 
        /// <returns>First group</returns>
        private static string matchFirstGroup(string line, Regex regex) {
            String ret = "";
            MatchCollection matches = regex.Matches(line);
            foreach (Match m in matches) {
                if (m.Groups.Count > 0) {
                    ret = m.Groups[1].ToString().Trim();
                }
                break;
            }
            return ret;
        }

        /// <summary>
        /// Parse query result for V$OPTION value.
        /// </summary>
        /// <param name="scriptInstance">script reference</param>
        /// <param name="attributeNames">attribute</param>
        /// <param name="queryOutput">Output</param>
        private static void vOptionsValueHandler
            (WindowsOracleInstanceDBOptionsStaticScript scriptInstance, String attributeName, String queryOutput) {

            Regex r = new Regex(@"^<BDNA>vOptions<BDNA>(.*?)<BDNA>(.*?)<BDNA>$");
            StringBuilder result = new StringBuilder();
            StringBuilder logData = new StringBuilder();

            foreach (String line in queryOutput.Split('\n', '\r')) {
                if (r.IsMatch(line)) {
                    Match match = r.Match(line);
                    if (match.Length > 1) {
                        String PARAMETER = match.Groups[1].ToString();
                        String VALUE = match.Groups[2].ToString();

                        if (result.Length > 0) {
                            result.Append(@"<BDNA,>");
                        }
                        result.Append(@"PARAMETER=").Append(PARAMETER);
                        result.Append(@"<BDNA,1>VALUE=").Append(VALUE);
                        logData.AppendFormat("{0}: {1}:{2}\n", attributeName, PARAMETER, VALUE);
                    }
                }
            }
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Parse query results for attribute {1}:\n{2}",
                                  scriptInstance.m_taskId,
                                  attributeName,
                                  logData.ToString());
            if (result.Length > 0) {
                scriptInstance.SaveCollectedData(attributeName, result.ToString());
            }
        }


        /// <summary>
        /// Parse query result for DBA_FEATURE_USAGE_STATISTICS value.
        /// </summary>
        /// <param name="scriptInstance">script reference</param>
        /// <param name="attributeNames">attribute</param>
        /// <param name="queryOutput">Output</param>
        private static void dbaFeatureValueHandler
            (WindowsOracleInstanceDBOptionsStaticScript scriptInstance, String attributeName, String queryOutput) {

            Regex r = new Regex(@"^dbaFeatures<BDNA,1>(.*?)<BDNA,1>(.*?)<BDNA,1>(.*?)<BDNA,1>(.*?)<BDNA,1>(.*?)<=BDNA>");
            StringBuilder result = new StringBuilder();
            StringBuilder logData = new StringBuilder();

            string output = queryOutput.Replace("\n", "").Replace("\r", "");
            foreach (String line in output.Split(new string[] { "<BDNA=>" }, StringSplitOptions.RemoveEmptyEntries)) {
                Match match = r.Match(line);
                if (match.Length > 1) {
                    String FEATURE_NAME = match.Groups[1].ToString();
                    String DETECTED = match.Groups[2].ToString();
                    String CUR_USED = match.Groups[3].ToString();
                    String FIRST_USE = match.Groups[4].ToString();
                    String LAST_USE = match.Groups[5].ToString();

                    if (result.Length > 0) {
                        result.Append(@"<BDNA,>");
                    }
                    result.Append(FEATURE_NAME).Append(@"<BDNA,1>");
                    result.Append(DETECTED).Append(@"<BDNA,1>");
                    result.Append(CUR_USED).Append(@"<BDNA,1>");
                    result.Append(FIRST_USE).Append(@"<BDNA,1>");
                    result.Append(LAST_USE);
                    logData.AppendFormat("{0}: {1}:{2}:{3}:{4}:{5}\n", attributeName, FEATURE_NAME, DETECTED, CUR_USED, FIRST_USE, LAST_USE);
                } else if (no_row_selected_pattern.IsMatch(line)) {
                    result.Append(matchFirstGroup(line, no_row_selected_pattern));
                    logData.AppendLine("No rows selected.");
                    break;
                } else if (ora_error_pattern.IsMatch(line)) {
                    result.Append(matchFirstGroup(line, ora_error_pattern));
                    logData.AppendLine("Oracle error..");
                }
            }
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Parse query results for attribute {1}:\n{2}",
                                  scriptInstance.m_taskId,
                                  attributeName,
                                  logData.ToString());
            if (result.Length > 0) {
                scriptInstance.SaveCollectedData(attributeName, result.ToString());
            }
        }


        /// <summary>
        /// Signature for query result handlers.
        /// </summary>
        private delegate void QueryResultHandler(WindowsOracleInstanceDBOptionsStaticScript scriptInstance, string attributeName, string outputData);

        /// <summary>
        /// Helper class to match up a query with the correct result handler.
        /// </summary>
        private class QueryTableEntry {

            public QueryTableEntry(StringBuilder queryString, Regex regex, QueryResultHandler resultHandler) {
                m_queryString = queryString;
                m_resultHandler = resultHandler;
                m_regex = regex;
            }

            /// <summary>
            /// Get regex 
            /// </summary>
            public Regex regex {
                get { return m_regex; }
            }

            /// <summary>
            /// Gets the query string.
            /// </summary>
            public StringBuilder QueryString {
                get { return m_queryString; }
            }

            /// <summary>\
            /// Gets the result handler.
            /// </summary>
            public QueryResultHandler ResultHandler {
                get { return m_resultHandler; }
            }

            /// <summary>Regex</summary>
            private readonly Regex m_regex;

            /// <summary>Query string.</summary>
            private readonly StringBuilder m_queryString;

            /// <summary>Result handler.</summary>
            private readonly QueryResultHandler m_resultHandler;
        }

        /// <summary>
        /// Static initializer to build up a map of supported attribute
        /// names to their associated query strings at class load time.
        /// </summary>
        static WindowsOracleInstanceDBOptionsStaticScript() {
            ICollection<KeyValuePair<string, QueryTableEntry>> ic = (ICollection<KeyValuePair<string, QueryTableEntry>>)s_attributeMap;

            foreach (KeyValuePair<string, QueryTableEntry> kvp in s_queryTable) {
                ic.Add(kvp);
            }
        }

        /// <summary>Database assigned task id.</summary>
        private string m_taskId;

        /// <summary>CLE element id.</summary>
        private long m_cleId;

        /// <summary>Id of element being collected.</summary>
        private long m_elementId;

        /// <summary>Database relative task dispatch timestamp.</summary>
        private long m_databaseTimestamp;

        /// <summary>CLE local dispatch timestamp.</summary>
        private long m_localTimestamp;

        /// <summary>Map of attribute names to attribute element ids.</summary>
        private IDictionary<string, string> m_attributes;

        /// <summary>Map of collection script specific parameters.</summary>
        private IDictionary<string, string> m_scriptParameters;

        /// <summary>Map of connection parameters.</summary>
        private IDictionary<string, object> m_connection;

        /// <summary>Collected Data</summary>
        private IDictionary<string, string> m_collectedData = new Dictionary<string, string>();

        /// <summary>Data row buffer.</summary>
        private StringBuilder m_dataRow = new StringBuilder();

        /// <summary>Stopwatch for tracking all time since start of script execution.</summary>
        private Stopwatch m_executionTimer = null;

        /// <summary>Oracle version</summary>
        private string m_strVersion = String.Empty;

        /// <summary> udt pattern</summary>
        private static Regex s_pattern = new Regex(@"(?<name>.+?)\s*=\s*(?<value>.+)",
                                                   RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static Regex all_ver_pattern = new Regex(@".*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex ver1011_pattern = new Regex(@"^10\..*|^11\..*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex no_row_selected_pattern = new Regex(@"^(no rows selected)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex rows_selected_pattern = new Regex(@"^(\d+ rows selected.)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex ora_error_pattern = new Regex(@"^(ORA-\d+: .*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// This table pairs up known attribute names with the query string needed to get the correct value from an Oracle
        /// database.  This table exists merely to seed the attribute map which will be used by the task execution code.
        /// </summary>
        private static readonly KeyValuePair<string, QueryTableEntry>[] s_queryTable = {
            new KeyValuePair<string, QueryTableEntry>(@"vOptions",
            new QueryTableEntry(new StringBuilder("select '<BDNA>vOptions<BDNA>'||PARAMETER||'<BDNA>'||VALUE||'<BDNA>' FROM V$OPTION;"),
            all_ver_pattern,
            new QueryResultHandler(vOptionsValueHandler))),

            new KeyValuePair<string, QueryTableEntry>(@"dbaFeatures",
            new QueryTableEntry(new StringBuilder("SELECT '<BDNA=>dbaFeatures<BDNA,1>FEATURE_NAME='||NAME||'<BDNA,1>DETECTED='||DETECTED_USAGES")
            .Append("||'<BDNA,1>CUR_USED='||CURRENTLY_USED||'<BDNA,1>FIRST_USE='||FIRST_USAGE_DATE||'<BDNA,1>LAST_USE='||LAST_USAGE_DATE")
            .Append("||'<=BDNA>' FROM DBA_FEATURE_USAGE_STATISTICS;"),
            ver1011_pattern,
            new QueryResultHandler(dbaFeatureValueHandler)))            
        };

        /// <summary>Map of supported attribute names to associated query strings.</summary>
        private static readonly IDictionary<string, QueryTableEntry> s_attributeMap = new Dictionary<string, QueryTableEntry>();

    }
}
