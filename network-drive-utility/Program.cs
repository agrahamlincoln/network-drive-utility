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
        private static bool isSilent = true;
        private static bool isVerbose = true; //Default is false

        /// <summary>Main Entrypoint for the Program
        /// </summary>
        /// <param name="args">Program arguments</param>
        public static void Main(string[] args)
        {
            output("Now Running: " + System.Diagnostics.Process.GetCurrentProcess().ProcessName + " v" + Utilities.getVersion());
            //Determine whether program is running silently or not.
            if (args.Length != 0)
            {
                foreach (string arg in args)
                {
                    output("Running with arg: " + arg);
                    isSilent = (arg == "silent" ? true : false);
                    isVerbose = (arg == "verbose" ? true : false);
                }
            }

            //Determine if the program will run properly or not.
            if (!Utilities.HasNet35())
            {
                output("Error: .NET 3.5 or greater is not installed");
            }
            else
            {
                List<NetworkConnection> allDrives;

                output("1:\tGet list of currently mapped drives from WMI");
                List<NetworkConnection> mapDrives = getMappedDrives();

                output("2:\tRemove blacklisted Fileshares");
                string blacklistXml_FilePath = Utilities.readAppConfigKey("blacklistXMLPath");
                List<NetworkConnection> blacklistShares = getXMLDrives(blacklistXml_FilePath);

                //find all mapped drives that are currently blacklisted and remove them
                List<NetworkConnection> toUnmapDrives = mapDrives.Intersect(blacklistShares, new WildcardNetworkConnectionComparer()).ToList();
                //Remove unmapped drives from list
                List<NetworkConnection> clean_mapDrives = mapDrives.Except(toUnmapDrives, new NetworkConnectionComparer()).ToList();
                output("\t2.1:\tDeleting Blacklisted Fileshares");
                foreach (NetworkConnection netCon in toUnmapDrives)
                {
                    try
                    {
                        output("\t\tUnmapping Drive: " + netCon.LocalName);
                        netCon.unmap();
                    }
                    catch (Exception e)
                    {
                        output("\t\tError unmapping Drive " + netCon.LocalName + " " + netCon.RemoteName);
                        output(e.ToString());
                    }
                }

                output("3:\tGet Fileshares from XML");
                //get filepath from app.config file
                string globalXML_FilePath = Utilities.readAppConfigKey("userXMLPath");

                //get the list of XML drives from the file path
                List<NetworkConnection> xmlDrives = getXMLDrives(globalXML_FilePath);

                output("4:\tGenerate list of all known Network Drives");
                if (xmlDrives.Count > 0)
                {
                    //Combine the mapped drives and the xml drives
                    allDrives = xmlDrives.Union(clean_mapDrives, new NetworkConnectionComparer()).ToList();
                }
                else
                {
                    //only mapped drives are available
                    allDrives = mapDrives;
                }

                output("5:\tWriting the list to file");
                if (allDrives.Count > 0)
                {
                    foreach (NetworkConnection drive in allDrives)
                    {
                        output(drive.toString(), isVerbose);
                    }

                    NetworkConnectionList listobj_allUserDrives = new NetworkConnectionList(allDrives);

                    output("\t5.1:\tSerialize the list & Write to XML file");
                    try
                    {
                        Utilities.SerializeToFile<NetworkConnectionList>(listobj_allUserDrives, globalXML_FilePath);
                    }
                    catch
                    {
                        output("\t\tError writing to file. The xml file has not been saved.");
                    }
                }
                else
                {
                    output("\tNo Fileshares found.");
                }
            }
            output(Environment.NewLine);
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

        #region output methods
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


        /// <summary>Standard program output method, determines where and what to output
        /// </summary>
        /// <param name="message">Output message</param>
        /// <param name="verbose">If true, will only display in verbose mode.</param>
        public static void output(string message, bool verbose)
        {
            if (verbose)
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
        #endregion

        #endregion
    }
}
