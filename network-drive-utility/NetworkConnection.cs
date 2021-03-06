﻿using System;
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
    sealed public class NetworkConnectionList
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
    sealed public class NetworkConnection
    {
        /// <summary>The following block are all used to unmap the drives.
        /// </summary>
        /// <remarks>This code was used from aejw's Network Drive class: build 0015 05/14/2004 aejw.com</remarks>
        [DllImport("mpr.dll")]
        private static extern int WNetCancelConnection2A(string psName, int piFlags, int pfForce);
        private const int CONNECT_UPDATE_PROFILE = 0x00000001;

        //Class Variables
        [XmlElement("LocalName")]
        public string LocalName { get; set; }
        [XmlElement("RemoteName")]
        public string RemoteName { get; set; }
        [XmlElement("Domain")]
        public string Domain { get; set; }
        [XmlElement("UserName")]
        public string UserName { get; set; }
        [XmlElement("Persistent")]
        public Boolean Persistent { get; set; }
        [XmlElement("ServerName")]
        public string ServerName { get; set; }

        #region Constructors
        /// <summary>No-Arg constructor
        /// </summary>
        public NetworkConnection()
        {
            this.LocalName = "";
            this.RemoteName = "";
            this.Domain = "";
            this.UserName = "";
            this.ServerName = "";
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
            this.UserName = "";
            this.ServerName = getServerName();
            this.Persistent = false;
        }

        /// <summary>Constructor with all Arguments
        /// </summary>
        /// <param name="LocalName">Local Drive Letter/Local Name of the Network Drive Mapping</param>
        /// <param name="RemoteName">Full Path of the Network Drive</param>
        /// <param name="domain">Domain of the user associated to this mapping</param>
        /// <param name="UserName">Username of the user associated to this mapping</param>
        /// <param name="Persistent">Drive Mapping persistence</param>
        public NetworkConnection(string LocalName, string RemoteName, string domain, string UserName, bool Persistent)
        {
            this.LocalName = LocalName;
            this.RemoteName = RemoteName;
            this.Domain = domain;
            this.UserName = UserName;
            this.ServerName = getServerName();
            this.Persistent = Persistent;
        }
        #endregion

        /// <summary>Creates a list of Network Connections from WMI
        /// </summary>
        /// <returns>List of Network Connections from WMI</returns>
        internal static List<NetworkConnection> ListCurrentlyMappedDrives()
        {
            List<NetworkConnection> drivesFromWMI = new List<NetworkConnection>();

            Regex driveLetter = new Regex("^[A-z]:");

            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2",
                                                                                 "SELECT * FROM Win32_NetworkConnection");

                string LocalName;
                string RemoteName;
                string[] QualifiedUserName;
                bool Persistent;

                //Enumerate all network drives and store in ArrayList object.
                foreach (ManagementObject queryObj in searcher.Get())
                {
                    //get information using WMI
                    LocalName = String.Format("{0}", queryObj["LocalName"]);
                    Persistent = Boolean.Parse(String.Format("{0}", queryObj["Persistent"]));
                    RemoteName = String.Format("{0}", queryObj["RemoteName"]);
                    QualifiedUserName = String.Format("{0}", queryObj["UserName"]).Split('\\');

                    if (driveLetter.IsMatch(LocalName))
                    {
                        if (QualifiedUserName.Length >= 2)
                            drivesFromWMI.Add(new NetworkConnection(LocalName, RemoteName, QualifiedUserName[0], QualifiedUserName[1], Persistent));
                        else
                            drivesFromWMI.Add(new NetworkConnection(LocalName, RemoteName, "", "", Persistent));
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
        internal void unmap()
        {
            bool force = false;
            //call unmap and return
            int iFlags = 0;
            if (Persistent) { iFlags += CONNECT_UPDATE_PROFILE; }
            int i = WNetCancelConnection2A(LocalName, iFlags, Convert.ToInt32(force));
            if (i > 0) { throw new System.ComponentModel.Win32Exception(i); }
        }

        /// <summary>To String method.
        /// </summary>
        /// <returns>Local Drive Letter + Full UNC Path</returns>
        internal string toString()
        {
            string str;
            str = LocalName + "\t" + RemoteName + "\tDomain: " + Domain;
            return str;
        }

        /// <summary>Splits the full share path and returns only the share name
        /// </summary>
        /// <returns>String - share name</returns>
        internal string getShareName()
        {
            string share;

            share = Utilities.getToken(this.RemoteName, 3, '\\');

            return share;
        }

        /// <summary>Splits the full share path and returns only the server name
        /// </summary>
        /// <returns>String - server name</returns>
        internal string getServerName()
        {
            string server;

            server = Utilities.getToken(this.RemoteName, 2, '\\');

            return server.ToUpper();
        }
    }
}
