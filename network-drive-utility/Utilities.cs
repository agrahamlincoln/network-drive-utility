using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security;
using System.Security.AccessControl;
using System.Security.Permissions;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace network_drive_utility
{
    /// <summary>This class is used to store utility methods that have no direct link to this application.
    /// </summary>
    /// <remarks>Includes: Error-Logging methods, Alphabet enumeration</remarks>
    /// <remarks>Excludes: Anything that could not be dropped into another application without modification</remarks>
    class Utilities
    {
        static LogWriter logger = new LogWriter();
        /*
        #region Log writing
        
        const string TIMESTAMP_FORMAT = "MM/dd HH:mm:ss";

        /// <summary>Returns a timestamp in string format.
        /// </summary>
        /// <remarks>Uses the Timestamp Format defined in the Constants of this class.</remarks>
        /// <param name="value">DateTime to change into string</param>
        /// <returns>Timestamp in string format.</returns>
        private static string getTimestamp(DateTime value)
        {
            return value.ToString(TIMESTAMP_FORMAT);
        }

        /// <summary>Public method to access the writelog method
        /// </summary>
        /// <param name="message">Message to write to log</param>
        /// <param name="print">Whether to write or not</param>
        public static void writeLog(string message, bool print)
        {
            if (print)
            {
                owriteLog(message);
            }
        }

        /// <summary>Appends a new message to the end of the log file.
        /// </summary>
        /// <remarks>If there is no log file available, this will create one. The log files are stored in the current user's Appdata/Roaming folder.</remarks>
        /// <param name="message"></param>
        private static void owriteLog(string message)
        {
            string logLocation = appDataPath() + "_log.txt";

            if (!System.IO.File.Exists(logLocation))
            {
                using (StreamWriter sw = System.IO.File.CreateText(logLocation))
                {
                    sw.WriteLine(getTimestamp(DateTime.Now) + message);
                }
            }
            else
            {
                StreamWriter sWriter = new StreamWriter(logLocation, true);
                sWriter.WriteLine(getTimestamp(DateTime.Now) + "\t" + message);

                sWriter.Close();
            }
        }

        /// <summary>Gets the file path in the current user's AppData/Roaming folder. The filename will be the current process name by default.
        /// </summary>
        /// <returns>string file path in user's AppData/Roaming folder</returns>
        public static string appDataPath()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            return path;
        }

        /// <summary>Returns the currently runnning program's version number.
        /// </summary>
        /// <returns>String version number of currently running program.</returns>
        public static string getVersion()
        {
            return typeof(Utilities).Assembly.GetName().Version.ToString();
        }

        #endregion
         */

        #region .NET Checking

        /// <summary>Checks the current machine for .NET Installations
        /// </summary>
        /// <returns>Boolean value representing whether .NET 3.5 or greater is installed or not.</returns>
        public static bool HasNet35()
        {
            bool returnValue = false;
            try
            {
                List<string> versions = GetVersionFromRegistry();
                Regex netVersion = new Regex("(v3\\.5|v4.0|v4)+");

                foreach (string ver in versions)
                {
                    if (netVersion.IsMatch(ver))
                    {
                        logger.Write(".NET version " + ver + " is installed.", true);
                        returnValue = true;
                        break;
                    }
                }
                if (returnValue == false)
                    logger.Write(".NET 3.5 must be installed to run this.");
                return returnValue;
            }
            catch (Exception e)
            {
                logger.Write("Could not query registry for .NET Versions" + e.ToString());
                return false;
            }

        }

        /// <summary>Checks registry on this machine for keys added during .NET installs.
        /// </summary>
        /// <returns>string List of .NET versions installed.</returns>
        public static List<string> GetVersionFromRegistry()
        {
            List<string> versions = new List<string>();

            // Opens the registry key for the .NET Framework entry. 
            using (RegistryKey ndpKey =
                RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, "").
                OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\"))
            {
                // As an alternative, if you know the computers you will query are running .NET Framework 4.5  
                // or later, you can use: 
                // using (RegistryKey ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,  
                // RegistryView.Registry32).OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\"))
                foreach (string versionKeyName in ndpKey.GetSubKeyNames())
                {
                    if (versionKeyName.StartsWith("v"))
                    {

                        RegistryKey versionKey = ndpKey.OpenSubKey(versionKeyName);
                        string name = (string)versionKey.GetValue("Version", "");
                        string sp = versionKey.GetValue("SP", "").ToString();
                        string install = versionKey.GetValue("Install", "").ToString();
                        if (install == "")
                        { //no install info, must be later.
                            //writeLog(versionKeyName + "  " + name);
                            versions.Add(versionKeyName);
                        }
                        else
                        {
                            if (sp != "" && install == "1")
                            {
                                //writeLog(versionKeyName + "  " + name + "  SP" + sp);
                                versions.Add(versionKeyName);
                            }

                        }
                        if (name != "")
                        {
                            continue;
                        }
                        foreach (string subKeyName in versionKey.GetSubKeyNames())
                        {
                            RegistryKey subKey = versionKey.OpenSubKey(subKeyName);
                            name = (string)subKey.GetValue("Version", "");
                            if (name != "")
                                sp = subKey.GetValue("SP", "").ToString();
                            install = subKey.GetValue("Install", "").ToString();
                            if (install == "")
                            { //no install info, must be later.
                                //writeLog(versionKeyName + "  " + name);
                            }
                            else
                            {
                                if (sp != "" && install == "1")
                                {
                                    //writeLog("  " + subKeyName + "  " + name + "  SP" + sp);
                                }
                                else if (install == "1")
                                {
                                    //writeLog("  " + subKeyName + "  " + name);
                                }
                            }
                        }
                    }
                }
            }
            return versions;
        }
        #endregion

        #region XML (de)Serializers

        /// <summary>Generic List Deserializer
        /// </summary>
        /// <typeparam name="T">Any type of serializable object stored in a list</typeparam>
        /// <param name="xmlString">string of XML objects to deserialize</param>
        /// <returns>List of Objects deserialized from the string</returns>
        public static T Deserialize<T>(string xmlString)
        {
            T result;

            XmlSerializer deserializer = new XmlSerializer(typeof(T));
            using (TextReader textReader = new StringReader(xmlString))
            {
                result = (T)deserializer.Deserialize(textReader);
            }

            return result;
        }

        /// <summary>Serializes a list of objects and writes the serialized obj to a XML file specified
        /// </summary>
        /// <typeparam name="T">Any type of serializable object</typeparam>
        /// <param name="obj">List of serializable objects</param>
        /// <param name="filePath">Full Path of XML file to write to</param>
        public static void SerializeToFile<T>(T obj, string filePath)
        {
            XmlSerializer serializer = new XmlSerializer(obj.GetType());
            using (StreamWriter sw = new StreamWriter(filePath))
            {
                serializer.Serialize(sw, obj);
            }
        }

        #endregion

        #region Miscellaneous

        /// <summary>Reads an entire file.
        /// </summary>
        /// <param name="fullPath">Full UNC Path of the file</param>
        /// <returns>string of the entire file</returns>
        public static string readFile(string fullPath)
        {
            string str;
            //Read json from file on network
            using (StreamReader file = new StreamReader(fullPath))
            {
                str = file.ReadToEnd();
            }
            return str;
        }

        /// <summary>Verifies if the current user has write permissions to a folder.
        /// </summary>
        /// <remarks>This code was found on stackoverflow.com http://stackoverflow.com/questions/1410127/c-sharp-test-if-user-has-write-access-to-a-folder </remarks>
        /// <param name="fullPath">Path of folder to check</param>
        /// <returns>Whether the user can write to the path or not</returns>
        public static bool canIWrite(string fullPath)
        {
            bool writable = false;
            try
            {
                DirectoryInfo di = new DirectoryInfo(fullPath);
                DirectorySecurity acl = di.GetAccessControl();
                AuthorizationRuleCollection rules = acl.GetAccessRules(true, true, typeof(NTAccount));

                WindowsIdentity currentUser = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(currentUser);
                foreach (AuthorizationRule rule in rules)
                {
                    FileSystemAccessRule fsAccessRule = rule as FileSystemAccessRule;
                    if (fsAccessRule == null)
                        continue;

                    if ((fsAccessRule.FileSystemRights & FileSystemRights.WriteData) > 0)
                    {
                        NTAccount ntAccount = rule.IdentityReference as NTAccount;
                        if (ntAccount == null)
                        {
                            continue;
                        }

                        if (principal.IsInRole(ntAccount.Value))
                        {
                            logger.Write(string.Format("Current user is in role of {0}, has write access", ntAccount.Value));
                            writable = true;
                            continue;
                        }
                        logger.Write(string.Format("Current user is not in role of {0}, does not have write access", ntAccount.Value));
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                logger.Write("does not have write access");
                writable = false;
            }

            return writable;
        }

        /// <summary>Gets a custom key from the App.Config file.
        /// </summary>
        /// <param name="keyName">name of the XML key</param>
        /// <returns>Value of the XML key</returns>
        public static string readAppConfigKey(string keyName)
        {
            AppSettingsReader appConfig = new AppSettingsReader();
            string value;

            try
            {
                value = appConfig.GetValue(keyName, typeof(string)).ToString();
            }
            catch
            {
                logger.Write("App.Config is missing... Defaulting to XML file in User's AppData folder");
                value = logger.logPath + ".xml";
            }

            //This occurs when the appdata is found but the key is invalid
            if (value == null)
            {
                value = "";
            }

            return value;
        }

        /// <summary>Regex Builder
        /// </summary>
        /// <param name="pattern">String Pattern with wildcards</param>
        /// <returns name="regex_pattern">Regular Expression Formatted Pattern</returns>
        public static string RegexBuild(string pattern)
        {
            string regex_pattern;
            if (pattern == "" || pattern == null)
            {
                regex_pattern = "^.*$";
            }
            else
            {
                regex_pattern =  "^" + Regex.Escape(pattern)
                    .Replace(@"\*", ".*")
                    .Replace(@"\?", ".")
                    + "$";
            }
            return regex_pattern;
        }

        /// <summary>Matches two strings, ignoring case.
        /// </summary>
        /// <param name="str1">String to compare</param>
        /// <param name="str2">String to compare</param>
        /// <returns>Boolean value of whether strings match or not.</returns>
        public static bool matchString_IgnoreCase(string str1, string str2)
        {
            bool isMatch = false;
            if (str1.Equals(str2, StringComparison.OrdinalIgnoreCase))
            {
                isMatch = true;
            }
            return isMatch;
        }

        /// <summary>Splits a string with the passed delimeter and returns the token specified
        /// </summary>
        /// <param name="str">string to split</param>
        /// <param name="token">index of token to return</param>
        /// <param name="delim">delimeter to split string with</param>
        /// <returns>the resulting token from the string</returns>
        public static string getToken(string str, int token, char delim)
        {
            string parsed;
            string[] strArray = str.Split(delim);
            if (token > strArray.Length || token < 0)
            {
                //return the last token in the string
                parsed = strArray.Last();
            }
            else
            {
                parsed = strArray[token];
            }
            return parsed;
        }

        /// <summary>Takes a file path and cleans it up so it does not include any filenames and is only the filepath
        /// </summary>
        /// <param name="fullPath">full file/directory path</param>
        /// <returns>Path of the directory and not any files.</returns>
        public static string trimPath(string fullPath)
        {
            string trimmed;

            //split the string
            List<string> strArray = fullPath.Split('\\').ToList();
            //remove the last item
            strArray.RemoveAt(strArray.Count - 1);
            //re-join the string
            trimmed = string.Join("\\", strArray.ToArray());

            return trimmed;
        }

        /// <summary>Parses the Domain from a Fully Qualified Domain Name
        /// </summary>
        /// <param name="fqdn">Fully qualified domain name of a host</param>
        /// <returns>Only the domain name of the passed FQDN</returns>
        public static string GetDomainName(string fqdn)
        {
            List<string> fqdnList = fqdn.Split('.').ToList();
            fqdnList.RemoveAt(0);
            return String.Join(".", fqdnList.ToArray());
        }

        /// <summary>Parses the Hostname from a Fully Qualified Domain Name
        /// </summary>
        /// <param name="fqdn">Fully qualified domain name of a host</param>
        /// <returns>Only the hostname of the passed FQDN</returns>
        public static string GetHostName(string fqdn)
        {
            return getToken(fqdn, 0, '.');
        }

        /// <summary>Performs a DNS lookup and returns the fully qualified domain name.
        /// </summary>
        /// <param name="hostname">Hostname or IP Address to look up</param>
        /// <returns>Fully Qualified Domain Name of the host</returns>
        public static string GetFQDN(string hostname)
        {
            IPHostEntry host;
            host = Dns.GetHostEntry(hostname);
            return host.HostName;
        }

        #endregion
    }
}
