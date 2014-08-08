using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace network_drive_utility
{
    /// <summary>Primary class of the application, handles higher level program logic.
    /// </summary>
    class Main
    {
        /// <summary>Main Entrypoint for the Program
        /// </summary>
        /// <param name="args">Program arguments</param>
        public void Main(string[] args)
        {
            // 1: Get list of currently mapped drives from WMI
            List<NetworkConnection> mappedDrives = NetworkConnection.ListCurrentlyMappedDrives();
            List<NetworkConnection> allUserDrives;

            // 2: Get currently logged in user
            // Note: The Drive Map users are stored in each NetworkConnection object
            String currentUser = Environment.UserName;

            // 3: Get User's fileshares from XML

            // 3a: Check if the file exists
            AppSettingsReader appConfig = new AppSettingsReader();
            string Users_XML_FilePath = appConfig.GetValue("userXMLPath", typeof(string)).ToString();

            if (File.Exists(Users_XML_FilePath))
            {
                //XML file exists, read from file
                string xmlFile = Utilities.readFile(Users_XML_FilePath);
                List<NetworkConnection> userDrives = Utilities.Deserialize<NetworkConnection>(xmlFile);

                //Combine the mapped list and the xml list
                allUserDrives = userDrives.Union(mappedDrives, new NetworkConnectionComparer()).ToList();
            }
            else
            {
                //Users.XML does not exist; create one
                File.Create(Users_XML_FilePath);

                //all of the user drives are all of the ones currently mapped.
                allUserDrives = mappedDrives;
            }

            // 4: Serialize the list & Write to XML file
            Utilities.SerializeToFile<NetworkConnection>(allUserDrives, Users_XML_FilePath);
        }



    }
}
