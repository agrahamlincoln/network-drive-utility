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

                output("\n2: Remove blacklisted Fileshares");
                string blacklistXml_FilePath = Utilities.readAppConfigKey("blacklistXMLPath");
                List<NetworkConnection> blacklistShares = getXMLDrives(blacklistXml_FilePath);

                //find all mapped drives that are currently blacklisted and remove them
                List<NetworkConnection> toUnmapDrives = mapDrives.Intersect(blacklistShares, new WildcardNetworkConnectionComparer()).ToList();
                output("\n2.1: Deleting Blacklisted Fileshares");
                foreach (NetworkConnection netCon in toUnmapDrives)
                {
                    try
                    {
                        netCon.unmap();
                    }
                    catch (Exception e)
                    {
                        output("Error unmapping Drive " + netCon.LocalName + " " + netCon.RemoteName);
                        output(e.ToString());
                    }
                }

                output("\n3: Get Fileshares from XML");
                //get filepath from app.config file
                string globalXML_FilePath = Utilities.readAppConfigKey("userXMLPath");

                //get the list of XML drives from the file path
                List<NetworkConnection> xmlDrives = getXMLDrives(globalXML_FilePath);

                output("\n4: Generate list of all known Network Drives");
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

                output("\n5: Writing the list to file");
                if (allDrives.Count > 0)
                {
                    foreach (NetworkConnection drive in allDrives)
                    {
                        output(drive.toString());
                    }

                    NetworkConnectionList listobj_allUserDrives = new NetworkConnectionList(allDrives);

                    output("\n5.1: Serialize the list & Write to XML file");
                    try
                    {
                        Utilities.SerializeToFile<NetworkConnectionList>(listobj_allUserDrives, globalXML_FilePath);
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
