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
using System.Threading;
using System.Xml.Serialization;

namespace network_drive_utility
{
    /// <summary>This class is used to store utility methods that have no direct link to this application.
    /// </summary>
    /// <remarks>Includes: Error-Logging methods, Alphabet enumeration</remarks>
    /// <remarks>Excludes: Anything that could not be dropped into another application without modification</remarks>
    static class Utilities
    {
        static LogWriter logger = new LogWriter();

        #region .NET Checking

        /// <summary>Checks the current machine for .NET Installations
        /// </summary>
        /// <returns>Boolean value representing whether .NET 3.5 or greater is installed or not.</returns>
        internal static bool HasNet35()
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
        private static List<string> GetVersionFromRegistry()
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
                            versions.Add(versionKeyName);
                        else
                            if (sp != "" && install == "1")
                                versions.Add(versionKeyName);

                        if (name != "")
                            continue;
                        foreach (string subKeyName in versionKey.GetSubKeyNames())
                        {
                            RegistryKey subKey = versionKey.OpenSubKey(subKeyName);
                            name = (string)subKey.GetValue("Version", "");
                            if (name != "")
                                sp = subKey.GetValue("SP", "").ToString();
                            install = subKey.GetValue("Install", "").ToString();
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
        internal static T Deserialize<T>(string xmlString)
        {
            T result;

            XmlSerializer deserializer = new XmlSerializer(typeof(T));
            using (TextReader textReader = new StringReader(xmlString))
            {
                result = (T)deserializer.Deserialize(textReader);
            }

            return result;
        }

        #endregion

        #region Miscellaneous

        /// <summary>Reads an entire file.
        /// </summary>
        /// <param name="fullPath">Full UNC Path of the file</param>
        /// <returns>string of the entire file</returns>
        internal static string readFile(string fullPath)
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
        internal static bool IsWritable(string fullPath)
        {
            bool writable = false;
            try
            {
                //Instance variables
                DirectoryInfo di = new DirectoryInfo(fullPath);
                DirectorySecurity acl = di.GetAccessControl();
                AuthorizationRuleCollection rules = acl.GetAccessRules(true, true, typeof(NTAccount));

                //Current user
                WindowsIdentity currentUser = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(currentUser);

                //Iterate through rules & verify permissions
                foreach (AuthorizationRule rule in rules)
                {
                    FileSystemAccessRule fsAccessRule = rule as FileSystemAccessRule;
                    if (fsAccessRule == null)
                        continue;

                    if ((fsAccessRule.FileSystemRights & FileSystemRights.WriteData) > 0)
                    {
                        NTAccount ntAccount = rule.IdentityReference as NTAccount;
                        if (ntAccount == null)
                            continue;

                        //User has permissions
                        if (principal.IsInRole(ntAccount.Value))
                        {
                            logger.Write(string.Format("Current user is in role of {0}, has write access", ntAccount.Value));
                            writable = true;
                            continue;
                        }

                        //User has no permissions
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
        internal static string ReadAppConfigKey(string keyName)
        {
            AppSettingsReader appConfig = new AppSettingsReader();
            string value;

            try
            {
                value = appConfig.GetValue(keyName, typeof(string)).ToString();
            }
            catch (Exception crap)
            {
                logger.Write("App.Config is missing... Defaulting to XML file in User's AppData folder. Stack trace: " + crap.ToString());
                value = logger.logPath + ".xml";
            }

            //This occurs when the App.Config is found but the key is invalid
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
        internal static string RegexBuild(string pattern)
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

        /// <summary>Splits a string with the passed delimeter and returns the token specified
        /// </summary>
        /// <param name="str">string to split</param>
        /// <param name="token">index of token to return</param>
        /// <param name="delim">delimeter to split string with</param>
        /// <returns>the resulting token from the string</returns>
        internal static string getToken(string str, int token, char delim)
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

        /// <summary>Parses the Domain from a Fully Qualified Domain Name
        /// </summary>
        /// <param name="fqdn">Fully qualified domain name of a host</param>
        /// <returns>Only the domain name of the passed FQDN</returns>
        internal static string GetDomainName(string fqdn)
        {
            List<string> fqdnList = fqdn.Split('.').ToList();
            fqdnList.RemoveAt(0);
            return String.Join(".", fqdnList.ToArray());
        }

        /// <summary>Performs a DNS lookup and returns the fully qualified domain name.
        /// </summary>
        /// <param name="hostname">Hostname or IP Address to look up</param>
        /// <returns>Fully Qualified Domain Name of the host</returns>
        internal static string GetFQDN(string hostname)
        {
            IPHostEntry host;
            host = Dns.GetHostEntry(hostname);
            return host.HostName;
        }

        /// <summary>Parses a Boolean string using Boolean.Parse and handles exceptions
        /// </summary>
        /// <param name="str">string to parse for boolean value</param>
        /// <returns>Parsed value or false</returns>
        internal static bool parseBool(string str)
        {
            bool parsed = false; //Return value - False by default

            try 
            {
                parsed = Boolean.Parse(str);
            }
            catch (ArgumentException)
            {
                //string is null
                parsed = false;
            }
            catch (FormatException)
            {
                //string is invalid
                parsed = false;
            }

            return parsed;
        }

        #endregion
    }
}
