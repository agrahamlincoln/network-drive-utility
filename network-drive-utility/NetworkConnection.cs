using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace network_drive_utility
{
    /// <summary>Class wrapper for the Network Connection to store a list
    /// </summary>
    /// <remarks>This is primarily used for the serialization process</remarks>
    [XmlRoot("ArrayOfNetworkConnection")]
    public class NetworkConnectionList
    {
        [XmlArrayItem()]
        public List<NetworkConnection> Items { get; set; }

        #region Constructors

        /// <summary>No-Arg Constructor
        /// </summary>
        public NetworkConnectionList()
        {
            Items = new List<NetworkConnection>();
        }

        /// <summary>All-Args Constructor
        /// </summary>
        /// <param name="list">List to set as instance</param>
        public NetworkConnectionList(List<NetworkConnection> list)
        {
            Items = list;
        }

        #endregion
    }


    /// <summary>Stores information about a Network Connection, Mimics the structure of Windows WMI queries from root\CIMV2\Win32_NetworkConnection
    /// </summary>
    [XmlRoot("NetworkConnection")]
    public class NetworkConnection
    {
        /// <summary>The following block are all used to unmap the drives.
        /// </summary>
        /// <remarks>This code was used from aejw's Network Drive class: build 0015 05/14/2004 aejw.com</remarks>
        [DllImport("mpr.dll")]
        private static extern int WNetCancelConnection2A(string psName, int piFlags, int pfForce);
        private const int CONNECT_UPDATE_PROFILE = 0x00000001;
        [StructLayout(LayoutKind.Sequential)]
        private struct structNetResource
        {
            public int iScope;
            public int iType;
            public int iDisplayType;
            public int iUsage;
            public string sLocalName;
            public string sRemoteName;
            public string sComment;
            public string sProvider;
        }

        //Class Variables
        [XmlElement("LocalName")]
        public string LocalName { get; set; }
        [XmlElement("RemoteName")]
        public string RemoteName { get; set; }
        [XmlElement("Domain")]
        public string Domain { get; set; }
        [XmlElement("Persistent")]
        public Boolean Persistent { get; set; }

        #region Constructors
        /// <summary>No-Arg constructor
        /// </summary>
        public NetworkConnection()
        {
            this.LocalName = "";
            this.RemoteName = "";
            this.Domain = "";
            this.Persistent = false;
        }

        /// <summary>Constructor with Drive Letter and Share Path
        /// </summary>
        /// <param name="LocalName">Local Drive Letter/Local Name of the Network Drive Mapping</param>
        /// <param name="RemoteName">Full Path of the Network Drive</param>
        public NetworkConnection(string LocalName, string RemoteName)
        {
            this.LocalName = LocalName;
            this.RemoteName = RemoteName;
            this.Domain = "";
            this.Persistent = false;
        }

        /// <summary>Constructor with all Arguments
        /// </summary>
        /// <param name="LocalName">Local Drive Letter/Local Name of the Network Drive Mapping</param>
        /// <param name="RemoteName">Full Path of the Network Drive</param>
        /// <param name="UserName">Username and Domain of the user associated to this mapping</param>
        /// <param name="Persistent">Drive Mapping persistence</param>
        public NetworkConnection(string LocalName, string RemoteName, string UserName, bool Persistent)
        {
            this.LocalName = LocalName;
            this.RemoteName = RemoteName;
            this.Domain = UserName;
            this.Persistent = Persistent;
        }
        #endregion

        /// <summary>Creates a list of Network Connections from WMI
        /// </summary>
        /// <returns>List of Network Connections from WMI</returns>
        public static List<NetworkConnection> ListCurrentlyMappedDrives()
        {
            List<NetworkConnection> drivesFromWMI = new List<NetworkConnection>();

            Regex driveLetter = new Regex("^[A-z]:");

            try
            {
                ManagementObjectSearcher searcher =
                new ManagementObjectSearcher("root\\CIMV2",
                "SELECT * FROM Win32_NetworkConnection");

                string LocalName;
                string RemoteName;
                string[] Domain;
                bool Persistent;

                //Enumerate all network drives and store in ArrayList object.
                foreach (ManagementObject queryObj in searcher.Get())
                {
                    //get information using WMI
                    LocalName = String.Format("{0}", queryObj["LocalName"]);
                    Persistent =  Boolean.Parse(String.Format("{0}", queryObj["Persistent"]));
                    RemoteName =  String.Format("{0}", queryObj["RemoteName"]);
                    Domain =  String.Format("{0}", queryObj["UserName"]).Split('\\');

                    if (driveLetter.IsMatch(LocalName))
                    {
                        drivesFromWMI.Add(new NetworkConnection(LocalName, RemoteName, Domain[0], Persistent));
                    }
                }
            }
            catch (ManagementException e)
            {
                throw new ManagementException("An error occurred while querying for WMI data.\nCall Stack: " + e.Message);
            }

            return drivesFromWMI;
        }

        /// <summary>Unmaps the drive using core windows API's
        /// </summary>
        /// <remarks>This code was used from aejw's Network Drive class: build 0015 05/14/2004 aejw.com</remarks>
        public void unmap()
        {
            bool force = false;
			//call unmap and return
			int iFlags=0;
			if(Persistent){iFlags+=CONNECT_UPDATE_PROFILE;}
			int i = WNetCancelConnection2A(LocalName, iFlags, Convert.ToInt32(force));
			if(i>0){throw new System.ComponentModel.Win32Exception(i);}
        }

        /// <summary>To String method.
        /// </summary>
        /// <returns>Local Drive Letter + Full UNC Path</returns>
        public string toString()
        {
            string str;
            str = LocalName + "\t" + RemoteName + "\tDomain: " + Domain + "\tPersistent: " + (Persistent ? "Yes" : "No");
            return str;
        }

        /// <summary>Splits the full share path and returns only the share name
        /// </summary>
        /// <returns>String - share name</returns>
        public string getShareName()
        {
            string share;

            share = Utilities.getToken(this.RemoteName, 3, '\\');

            return share;
        }

        /// <summary>Splits the full share path and returns only the server name
        /// </summary>
        /// <returns>String - server name</returns>
        public string getServerName()
        {
            string server;

            server = Utilities.getToken(this.RemoteName, 2, '\\');

            return server;
        }

        /// <summary>Attempts a DNS lookup on the host and puts the server's domain name in the field.
        /// </summary>
        public void DNSVerify()
        {
            try
            {
                this.Domain = Utilities.GetDomainName(Utilities.GetFQDN(this.getServerName()));
            }
            catch (Exception e)
            {
                throw new Exception("Failed to look up the host through dns. Full Details:" + Environment.NewLine + e.ToString());
            }
        }

        /// <summary>Runs the DNSVerify method and returns a boolean value of whether the host was found or not.
        /// </summary>
        /// <returns>Whether the host is found or not</returns>
        public bool RDNSVerify()
        {
            try
            {
                DNSVerify();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Class used to compare two Network Connection objects
    /// </summary>
    class NetworkConnectionComparer : IEqualityComparer<NetworkConnection>
    {
        /// <summary>Class used to compare two Network Connection Objects
        /// </summary>
        /// <remarks>Verifies the RemoteName and Domain is match, does not care about Persistent or LocalName</remarks>
        /// <param name="drive1">NetworkConnection object to Compare</param>
        /// <param name="drive2">NetworkConnection object to Compare</param>
        /// <returns>Boolean value of whether the objects are equal or not.</returns>
        public bool Equals(NetworkConnection drive1, NetworkConnection drive2)
        {
            bool isEqual;

            //check if domain is empty
            if (drive1.Domain == "" || drive1.Domain == null || drive2.Domain == "" || drive2.Domain == null)
            {
                //ignore domain
                isEqual = Utilities.matchString_IgnoreCase(drive1.RemoteName, drive2.RemoteName);
            }
            else
            {
                //verify domains are equal
                isEqual = (Utilities.matchString_IgnoreCase(drive1.RemoteName, drive2.RemoteName)
                    && Utilities.matchString_IgnoreCase(drive1.Domain, drive2.Domain));
            }

            return isEqual;
        }

        public int GetHashCode(NetworkConnection drive)
        {
            string shareName = drive.getShareName();

            //Force Lowercase on RemoteName
            string ShareName_lower = shareName.ToLower();
            return ShareName_lower.GetHashCode();
        }
    }

    /// <summary>Class used to compare two Network Connection objects
    /// </summary>
    /// <remarks>This Comparer will implement RemoteName and Domain wildcards to cover all specific shares on a server.</remarks>
    class WildcardNetworkConnectionComparer : IEqualityComparer<NetworkConnection>
    {
        /// <summary>Class used to compare two Network Connection Objects
        /// </summary>
        /// <remarks></remarks>
        /// <param name="drive1">NetworkConnection object to Compare</param>
        /// <param name="drive2">NetworkConnection object to Compare</param>
        /// <returns>Boolean value of whether the objects are equal or not.</returns>
        public bool Equals(NetworkConnection drive1, NetworkConnection drive2)
        {
            bool isEqual;

            string drive1_server = drive1.getServerName();
            string drive1_share = drive1.getShareName();

            string drive2_server = drive2.getServerName();
            string drive2_share = drive2.getShareName();

            string server_Pattern = Utilities.RegexBuild(drive1_server);
            Regex serverMatch = new Regex(server_Pattern, RegexOptions.IgnoreCase);

            string domain_Pattern = Utilities.RegexBuild(drive1.Domain);
            Regex domainMatch = new Regex(domain_Pattern, RegexOptions.IgnoreCase);

            //Match domain FIRST (Will be true if domain is null or "")
            if (domainMatch.IsMatch(drive2.Domain))
            {
                //Wildcard shares
                if (drive1_share == "*" || drive2_share == "*")
                {
                    isEqual = (serverMatch.IsMatch(drive2_server));
                }
                //Match like normal
                else
                {
                    isEqual = Utilities.matchString_IgnoreCase(drive1.RemoteName, drive2.RemoteName);
                }
            }
            else
            {
                isEqual = false;
            }

            return isEqual;
        }

        /// <summary>Override the GetHashCode Method. This way the hashcodes will ALWAYS equate
        /// </summary>
        /// <remarks>Hashcodes must be equivalent AS WELL AS the .equals method</remarks>
        /// <returns>0, Everytime.</returns>
        public int GetHashCode(NetworkConnection drive) { return 0; }
        /*public int GetHashCode(NetworkConnection drive)
        {
            string shareName = drive.getShareName();

            //Force Lowercase on RemoteName
            string ShareName_lower = shareName.ToLower();
            return ShareName_lower.GetHashCode();
        }*/
    }
}
