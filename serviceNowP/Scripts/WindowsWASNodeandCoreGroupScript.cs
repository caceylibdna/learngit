#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Suma Manvi
*   Creation Date: 2007/10/04
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
using System.Collections.Specialized;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Management;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts
{
    public class WindowsWASNodeandCoreGroupScript : ICollectionScriptRuntime
    {
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
        /// <returns>Collection results.</returns>
        public CollectionScriptResults ExecuteTask(long taskId, long cleId, long elementId, long databaseTimestamp,
                long localTimestamp, IDictionary<string, string> attributes, IDictionary<string, string> scriptParameters,
                IDictionary<string, object> connection, string tftpPath, string tftpPath_login,
                string tftpPath_password, ITftpDispatcher tftpDispatcher)
        {
            m_taskId = taskId.ToString();
            m_cleId = cleId;
            m_elementId = elementId;
            m_databaseTimestamp = databaseTimestamp;
            m_localTimestamp = localTimestamp;
            m_attributes = attributes;
            m_scriptParameters = scriptParameters;
            m_tftpDispatcher = tftpDispatcher;
            m_connection = connection;
            m_executionTimer = Stopwatch.StartNew();

            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script WindowsWASNodeandCoreGroupScript.",
                                  m_taskId);
            try
            {
                // Check ManagementScope CIMV
                //ManagementScope cimvScope = null;

                if (connection == null)
                {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WindowsWASNodeandCoreGroupScript is null.",
                                          m_taskId);
                }
                else if (!connection.ContainsKey("cimv2"))
                {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV namespace is not present in connection object.",
                                          m_taskId);
                }
                else
                {
                    m_cimvScope = connection[@"cimv2"] as ManagementScope;
                    if (!m_cimvScope.IsConnected)
                    {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed",
                                              m_taskId);
                    }
                }

                //Check Script attributes
                if (resultCode.Equals(ResultCodes.RC_SUCCESS))
                {
                    if (scriptParameters.ContainsKey("profilePath"))
                    {
                        m_profileHome = scriptParameters[@"profilePath"];
                    }
                    else
                    {
                        resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Missing parameter WAS Profile Path parameter.",
                                              m_taskId);
                    }

                    if (scriptParameters.ContainsKey("cellName"))
                    {
                        m_cell = scriptParameters[@"cellName"];
                    }
                    else
                    {
                        resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Missing parameter WAS Cell Name parameter.",
                                              m_taskId);
                    }
                }

                if (resultCode == ResultCodes.RC_SUCCESS)
                {
                    StringBuilder ngDir = new StringBuilder();
                    StringBuilder ngList = new StringBuilder();
                    ngDir.Append((string)m_profileHome).Append(@"\config\cells\").Append((string)m_cell).Append(@"\nodegroups");
                    StringCollection ngNames = findGroups(ngDir.ToString());

                    foreach (string ng in ngNames)
                    {
                        if (!String.IsNullOrEmpty(ng))
                        {
                            if (ngList.Length > 0)
                            {
                                ngList.Append(BdnaDelimiters.DELIMITER_TAG);
                            }
                            ngList.Append(ng);
                        }
                    }

                    if (ngList.Length > 0)
                    {
                        BuildDataRow(s_nodeGroup, ngList.ToString());
                    }

                    // Process Cluster Info
                    StringBuilder clstDir = new StringBuilder();
                    StringBuilder clstList = new StringBuilder();
                    clstDir.Append((string)m_profileHome).Append(@"\config\cells\").Append((string)m_cell).Append(@"\clusters");
                    StringCollection clstNames = findGroups(clstDir.ToString());

                    foreach (string clst in clstNames)
                    {
                        if (!String.IsNullOrEmpty(clst))
                        {
                            if (clstList.Length > 0)
                            {
                                clstList.Append(BdnaDelimiters.DELIMITER_TAG);
                            }
                            clstList.Append(clst);
                        } 
                    }

                    if (clstList.Length > 0)
                    {
                        BuildDataRow(s_cluster, clstList.ToString());
                        BuildDataRow(s_isCluster, "True");
                    }
                    else
                    {
                        BuildDataRow(s_isCluster, "False");
                    }

                    // Process CoreGroup Info
                    StringBuilder cgDir = new StringBuilder();
                    StringBuilder cgList = new StringBuilder();
                    cgDir.Append((string)m_profileHome).Append(@"\config\cells\").Append((string)m_cell).Append(@"\coregroups");
                    StringCollection cgNames = findGroups(cgDir.ToString());

                    foreach (string cg in cgNames)
                    {
                        if (!String.IsNullOrEmpty(cg))
                        {
                            if (cgList.Length > 0)
                            {
                                cgList.Append(BdnaDelimiters.DELIMITER_TAG);
                            }
                            cgList.Append(cg);
                        }
                    }

                    if (cgList.Length > 0)
                    {
                        BuildDataRow(s_coreGroup, cgList.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                if (resultCode == ResultCodes.RC_SUCCESS)
                {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsWASNodeandCoreGroupScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                }
                else
                {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsWASNodeandCoreGroupScript.  Elapsed time {1}.\n{2}",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script WindowsWASNodeandCoreGroupScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, m_dataRow.ToString());
        }

        /// <summary>
        /// Find all servers in the node directory.
        /// </summary>
        /// <returns>Collections of directories</returns>
        private StringCollection findGroups(string dirPath)
        {
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Listing all subdirectories of each node and core group.",
                                  m_taskId);
            StringCollection arrGroups = new StringCollection();
            try
            {
                using (ManagementObject moDocDir = new ManagementObject(@"Win32_Directory='" + dirPath + @"'"))
                {
                    moDocDir.Scope = m_cimvScope;
                    moDocDir.Get();

                    foreach (ManagementObject moSubDirs in
                        moDocDir.GetRelated("Win32_Directory", null, null, null, "PartComponent", "GroupComponent", false, null))
                    {
                        string strDir = moSubDirs[@"Name"].ToString();
                        if (!String.IsNullOrEmpty(strDir))
                        {
                            if (s_pattern.IsMatch(strDir))
                            {
                                Match match = s_pattern.Match(strDir);
                                string name = match.Groups["group"].ToString();
                                if (!String.IsNullOrEmpty(name))
                                {
                                    arrGroups.Add(name);
                                }
                            }
                        }
                    }
                }
            }
            catch (ManagementException mex)
            {
                StringBuilder props = new StringBuilder();
                foreach (PropertyData mexProp in mex.ErrorInformation.Properties)
                {
                    if (mexProp != null && !mexProp.IsArray)
                    {

                        //
                        // Do NOT call ToString on the values because they may
                        // be null. StringBuilder handles null values just fine.
                        // Besides, calling ToString explicitly on the Names or
                        // Values of this collection buys you absolutely *nothing*.
                        // These are already objects, so there is no boxing to
                        // be avoided.
                        props.Append(mexProp.Name).Append(@"=").Append(mexProp.Value).AppendLine();
                    }
                }
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Unexpected error while searching for WindowsWASGetServersScript.\n{1}\n{2}",
                                      m_taskId,
                                      mex.ToString(),
                                      props.ToString());
            }
            catch (Exception ex)
            {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Unexpected error while searching for WindowsWASNodeandCoreGroupScript.\n{1}",
                                      m_taskId,
                                      ex.ToString());
            }
            return arrGroups;
        }

        /// <summary>
        /// Generate a new data row for a collected attribute and add
        /// it to the aggregate data row buffer.
        /// </summary>
        ///
        /// <param name="attributeName">Name of attribute collected.</param>
        /// <param name="collectedData">Collected data value.  Null is allowed.</param>
        private void BuildDataRow(string attributeName, string collectedData)
        {
            Console.WriteLine(collectedData);
            if (!m_attributes.ContainsKey(attributeName))
            {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Attribute \"{1}\" missing from attributeSet.",
                                      m_taskId,
                                      attributeName);
            }
            else if (string.IsNullOrEmpty(collectedData))
            {
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Script completed sucessfully with no data to return.",
                                      m_taskId);
            }
            else
            {
                m_dataRow.Append(m_elementId).Append(',')
                         .Append(m_attributes[attributeName]).Append(',')
                         .Append(m_scriptParameters[@"CollectorId"]).Append(',')
                         .Append(m_taskId).Append(',')
                         .Append(m_databaseTimestamp + m_executionTimer.ElapsedMilliseconds).Append(',')
                         .Append(attributeName).Append(',')
                         .Append(BdnaDelimiters.BEGIN_TAG).Append(collectedData).Append(BdnaDelimiters.END_TAG);
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

        /// <summary>Data row buffer.</summary>
        private StringBuilder m_dataRow = new StringBuilder();

        /// <summary>WebSphere Application Server Profile path. </summary>
        private string m_profileHome = String.Empty;

        /// <summary>WebSphere Application Server Cell Name. </summary>
        private string m_cell = String.Empty;

        /// <summary>output data buffer.</summary>
        private StringBuilder m_outputData = new StringBuilder();

        /// <summary>Stopwatch for tracking all time since start of script execution.</summary>
        private Stopwatch m_executionTimer = null;

        /// <summary>Management Scope </summary>
        private ManagementScope m_cimvScope = null;

        /// <summary>TFTP Listener</summary>
        private ITftpDispatcher m_tftpDispatcher = null;

        public static string s_nodeGroup = @"nodeGroup";

        public static string s_cluster = @"appSrv_clusterName";

        public static string s_isCluster = @"appSrv_isClusterServer";

        public static string s_coreGroup = @"coreGroup";

        private static Regex s_pattern = new Regex(@".+\\(?<group>.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    }
}


