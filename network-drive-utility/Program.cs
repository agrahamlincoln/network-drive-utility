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
        private static bool isSilent;

        /// <summary>Main Entrypoint for the Program
        /// </summary>
        /// <param name="args">Program arguments</param>
        public static void Main(string[] args)
        {
            //Determine whether program is running silently or not.
            if (args.Length != 0)
            {
                isSilent = (args[0] == "silent" ? true : false);
            }

            output("1: Get list of currently mapped drives from WMI");
            List<NetworkConnection> mappedDrives = NetworkConnection.ListCurrentlyMappedDrives();
            List<NetworkConnection> allUserDrives;

            output("2: Get currently logged in user");
            // Note: The Drive Map users are stored in each NetworkConnection object
            string currentUser = Environment.UserName;
            output("Currently Logged in User: " + currentUser);

            output("3: Get User's fileshares from XML");
            AppSettingsReader appConfig = new AppSettingsReader();
            string Users_XML_FilePath = appConfig.GetValue("userXMLPath", typeof(string)).ToString();

            if (File.Exists(Users_XML_FilePath))
            {
                output("XML file exists");
                string xmlFile = Utilities.readFile(Users_XML_FilePath);
                NetworkConnectionList userDrives = Utilities.Deserialize<NetworkConnectionList>(xmlFile);

                //Combine the mapped list and the xml list
                allUserDrives = userDrives.Items.Union(mappedDrives, new NetworkConnectionComparer()).ToList();
            }
            else
            {
                output("XML file does not exist");
                //all of the user drives are all of the ones currently mapped.
                allUserDrives = mappedDrives;
            }

            foreach (NetworkConnection drive in allUserDrives)
            {
                output(drive.toString());
            }

            NetworkConnectionList listobj_allUserDrives = new NetworkConnectionList(allUserDrives);

            output("4: Serialize the list & Write to XML file");
            Utilities.SerializeToFile<NetworkConnectionList>(listobj_allUserDrives, Users_XML_FilePath);
        }

        /// <summary>Standard program output method, determines where to direct output
        /// </summary>
        /// <param name="message">Output Message</param>
        public static void output(string message)
        {
            if (isSilent)
            {
                Utilities.writeLog(message);
            }
            else
            {
                Console.WriteLine(message);
            }
        }
    }
}
