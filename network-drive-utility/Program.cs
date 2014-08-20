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

            //Determine if the program will run properly or not.
            if (!Utilities.HasNet35())
            {
                output("Error: .NET 3.5 or greater is not installed");
            }
            else
            {
                List<NetworkConnection> allDrives;

                output("\n1: Get list of currently mapped drives from WMI");
                List<NetworkConnection> mapDrives = getMappedDrives();

                output("\n2: Get Fileshares from XML");
                //get filepath from app.config file
                AppSettingsReader appConfig = new AppSettingsReader();
                string XML_FilePath;
                try
                {
                    XML_FilePath = appConfig.GetValue("userXMLPath", typeof(string)).ToString();
                }
                catch
                {
                    output("App.Config is missing... Defaulting to User's AppData folder");
                    XML_FilePath = Utilities.appDataPath() + ".xml";
                }

                //get the list of XML drives from the file path
                List<NetworkConnection> xmlDrives = getXMLDrives(XML_FilePath);

                output("\n3: Generate list of all known Network Drives");
                if (xmlDrives.Count > 0)
                {
                    //Combine the mapped drives and the xml drives
                    allDrives = xmlDrives.Union(mapDrives, new NetworkConnectionComparer()).ToList();
                }
                else
                {
                    //only mapped drives are available
                    allDrives = mapDrives;
                }

                output("\n4: Writing the list to file");
                if (allDrives.Count > 0)
                {
                    foreach (NetworkConnection drive in allDrives)
                    {
                        output(drive.toString());
                    }

                    NetworkConnectionList listobj_allUserDrives = new NetworkConnectionList(allDrives);

                    output("\n4: Serialize the list & Write to XML file");
                    try
                    {
                        Utilities.SerializeToFile<NetworkConnectionList>(listobj_allUserDrives, XML_FilePath);
                    }
                    catch
                    {
                        output("Error writing to file. The xml file has not been saved.");
                    }
                }
                else
                {
                    output("No Fileshares found.");
                }
            }
        }

        #region Supplemental Methods

        /// <summary>Generates a list of all network connections from the XML file
        /// </summary>
        /// <returns>Returns list of all network connections from XML file; Returns empty list if file doesnt exist</returns>
        private static List<NetworkConnection> getXMLDrives(string filePath)
        {
            List<NetworkConnection> xmlDrives = new List<NetworkConnection>();
            try
            {
                if (File.Exists(filePath))
                {
                    output("XML file exists");
                    string xmlFile = Utilities.readFile(filePath);
                    xmlDrives = Utilities.Deserialize<NetworkConnectionList>(xmlFile).Items.ToList();
                }
                else
                {
                    output("XML file does not exist");
                }
            }
            catch
            {
                output("Could not locate/access the XML File in the path: \n" + filePath);
            }
            return xmlDrives;
        }


        /// <summary>Generates list of currently mapped drives
        /// </summary>
        /// <remarks>This is used to simplify the main program and to handle exceptions thrown</remarks>
        /// <returns>List of NetworkConnections with all currently mapped drives.</NetworkConnection></returns>
        private static List<NetworkConnection> getMappedDrives()
        {
            List<NetworkConnection> mappedDrives;

            try
            {
                mappedDrives = NetworkConnection.ListCurrentlyMappedDrives();
            }
            catch (Exception e)
            {
                output(e.ToString());
                mappedDrives = new List<NetworkConnection>();
            }

            return mappedDrives;
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

        #endregion
    }
}
