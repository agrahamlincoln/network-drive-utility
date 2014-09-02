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
        private static bool logsEnabled = false;
        private static bool deduplicate = false;

        /// <summary>Main Entrypoint for the Program
        /// </summary>
        /// <param name="args">Program arguments</param>
        public static void Main(string[] args)
        {
            output("Now Running: " + System.Diagnostics.Process.GetCurrentProcess().ProcessName + " v" + Utilities.getVersion(), true);
            //Determine whether program is running silently or not.
            if (args.Length != 0)
            {
                foreach (string arg in args)
                {
                    output("Running with arg: " + arg);
                    logsEnabled = (arg == "logging" ? true : false);
                    deduplicate = (arg == "dedupe" ? true : false);
                }
            }

            //Determine if the program will run properly or not.
            if (!Utilities.HasNet35())
            {
                output("Error: .NET 3.5 or greater is not installed", true);
            }
            else
            {
                string metaDataXml_FilePath = Utilities.readAppConfigKey("metaDataXMLPath");
                Statistics stats = readMetaData(metaDataXml_FilePath);

                List<NetworkConnection> allDrives;

                output("1:\tGet list of currently mapped drives from WMI");
                List<NetworkConnection> mapDrives = getMappedDrives();

                foreach (NetworkConnection netCon in mapDrives)
                {
                    output("Mapped: " + netCon.toString());
                }

                output("2:\tRemove blacklisted Fileshares");
                string blacklistXml_FilePath = Utilities.readAppConfigKey("blacklistXMLPath");
                List<NetworkConnection> blacklistShares = getXMLDrives(blacklistXml_FilePath);

                foreach (NetworkConnection netCon in blacklistShares)
                {
                    output("Blacklisted: " + netCon.toString());
                }

                //find all mapped drives that are currently blacklisted and remove them
                List<NetworkConnection> toUnmapDrives = mapDrives.Intersect(blacklistShares, new WildcardNetworkConnectionComparer()).ToList();
                //Remove unmapped drives from list
                List<NetworkConnection> clean_mapDrives = mapDrives.Except(toUnmapDrives, new NetworkConnectionComparer()).ToList();
                output("\t2.1:\tDeleting Blacklisted Fileshares");
                foreach (NetworkConnection netCon in toUnmapDrives)
                {
                    output("To Be Unmapped: " + netCon.toString());
                    try
                    {
                        output("\t\tUnmapping Drive: " + netCon.LocalName, true);
                        netCon.unmap();

                        //Increment the metadata
                        stats.FilesharesUnmapped = stats.FilesharesUnmapped + 1;
                    }
                    catch (Exception e)
                    {
                        output("\t\tError unmapping Drive " + netCon.LocalName + " " + netCon.RemoteName, true);
                        output(e.ToString());
                    }
                }

                foreach (NetworkConnection netCon in clean_mapDrives)
                {
                    output("Cleaned List: " + netCon.toString());
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

                //Deduplicate the drive list, only if specified in program arguments.
                if (deduplicate)
                {
                    allDrives = removeDuplicates(allDrives);
                }

                if (allDrives.Count > 0)
                {
                    stats.FilesharesFound = 0;
                    foreach (NetworkConnection drive in allDrives)
                    {
                        output(drive.toString());

                        //write the count to metadata
                        stats.FilesharesFound = stats.FilesharesFound + 1;
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
                    Utilities.SerializeToFile<Statistics>(stats, metaDataXml_FilePath);

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
                output("Error Deserializing the File: \n" + filePath);
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

        /// <summary>DeSerializes the MetaData XML file
        /// </summary>
        /// <param name="filePath">Path of the MetaData XML file</param>
        /// <returns>Statistics object with information from MetaData XML File</returns>
        private static Statistics readMetaData(string filePath)
        {
            Statistics stats = new Statistics();

            try
            {
                if (File.Exists(filePath))
                {
                    output("XML file Exists");
                    string xmlFile = Utilities.readFile(filePath);
                    stats = Utilities.Deserialize<Statistics>(xmlFile);
                }
                else
                {
                    output("MetaData XML file doesnt exist");
                }
            }
            catch
            {
                output("Could not locate/access the XML File in the path:" + Environment.NewLine + filePath);
            }

            return stats;
        }

        /// <summary>Checks a list with itself and cleans up any duplicate shares
        /// </summary>
        /// <param name="dirtyList">List of NetworkConnection objects</param>
        /// <returns>List of NetworkConnection objects without duplicates</returns>
        private static List<NetworkConnection> removeDuplicates(List<NetworkConnection> dirtyList)
        {
            List<NetworkConnection> cleanedList = new List<NetworkConnection>();

            cleanedList = dirtyList.Distinct(new NetworkConnectionComparer()).ToList();

            return cleanedList;
        }

        /// <summary>Standard program output method, determines where to direct output
        /// </summary>
        /// <param name="message">Output Message</param>
        public static void output(string message)
        {
            Utilities.writeLog(message, logsEnabled);
        }

        /// <summary>Standard program output method, Takes boolean value to override the logging setting
        /// </summary>
        /// <remarks>Note: If the program is run with the "logging" argument, it will override this.</remarks>
        /// <param name="message">Output Message</param>
        /// <param name="print">Boolean value that overrides the default setting.</param>
        public static void output(string message, bool print)
        {
            if (logsEnabled)
            {
                Utilities.writeLog(message, true);
            }
            else
            {
                Utilities.writeLog(message, print);
            }
        }

        #endregion
    }
}
