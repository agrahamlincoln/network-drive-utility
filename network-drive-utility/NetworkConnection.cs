using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
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

                    drivesFromWMI.Add(new NetworkConnection(LocalName, RemoteName, Domain[0], Persistent));
                }
            }
            catch (ManagementException e)
            {
                throw new ManagementException("An error occurred while querying for WMI data.\nCall Stack: " + e.Message);
            }

            return drivesFromWMI;
        }

        /// <summary>To String method.
        /// </summary>
        /// <returns>Local Drive Letter + Full UNC Path</returns>
        public string toString()
        {
            string str;
            str = "=====\n" + LocalName + " " + RemoteName + "\nUser: " + Domain + " Persistent: " + (Persistent ? "Yes" : "No");
            return str;
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
            return (drive1.RemoteName == drive2.RemoteName && drive1.Domain == drive2.Domain);
        }

        public int GetHashCode(NetworkConnection drive)
        {
            return drive.RemoteName.GetHashCode();
        }
    }
}
