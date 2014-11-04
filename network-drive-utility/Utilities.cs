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
using GrahamUtils;

namespace network_drive_utility
{
    /// <summary>This class is used to store utility methods that have no direct link to this application.
    /// </summary>
    /// <remarks>Includes: Error-Logging methods, Alphabet enumeration</remarks>
    /// <remarks>Excludes: Anything that could not be dropped into another application without modification</remarks>
    static class Utilities
    {
        static LogWriter logger = new LogWriter();

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

        /// <summary>Gets a custom key from the App.Config file.
        /// </summary>
        /// <param name="keyName">name of the XML key</param>
        /// <returns>Value of the XML key</returns>
        /// <returns>"not found" - if app.config is not found.</returns>
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
                value = "";
            }

            //This occurs when the App.Config is found but the key is invalid
            if (value == null)
            {
                value = "";
            }

            return value;
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
    }
}
