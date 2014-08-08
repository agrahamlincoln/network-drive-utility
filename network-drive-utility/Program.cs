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
    class Program
    {
        /// <summary>Main Entrypoint for the Program
        /// </summary>
        /// <param name="args">Program arguments</param>
        public static void Main(string[] args)
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
                NetworkConnectionList userDrives = Utilities.Deserialize<NetworkConnectionList>(xmlFile);

                //Combine the mapped list and the xml list
                allUserDrives = userDrives.Items.Union(mappedDrives, new NetworkConnectionComparer()).ToList();
            }
            else
            {
                //There is no XML file, so we can skip the deserialization
                //all of the user drives are all of the ones currently mapped.
                allUserDrives = mappedDrives;
            }
            NetworkConnectionList listobj_allUserDrives = new NetworkConnectionList(allUserDrives);

            // 4: Serialize the list & Write to XML file
            Utilities.SerializeToFile<NetworkConnectionList>(listobj_allUserDrives, Users_XML_FilePath);
        }



    }
}
