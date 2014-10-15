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
        private static LogWriter logger = new LogWriter(); //Local logger
        private static LogWriter globalLog = new LogWriter("Log.txt"); //Global Logger
        private static Statistics stats;  // Metadata Object

        /// <summary>Main Entrypoint for the Program
        /// </summary>
        /// <param name="args">Program arguments</param>
        public static void Main(string[] args)
        {

            try
            {
                #region** 1 Initialize Program
                //** 1.1 Write Log Header
                output(logger.header(), true);

                //** 1.2 Parse Program Parameters
                if (args.Length != 0)
                {
                    foreach (string arg in args)
                    {
                        output("Running with arg: " + arg);
                        logsEnabled = (arg == "logging" ? true : false);
                        deduplicate = (arg == "dedupe" ? true : false);
                    }
                }

                //** 1.3 Verify Program Compatability
                if (!Utilities.HasNet35())
                {
                    output("Error: .NET 3.5 or greater is not installed", true);
                }
                else
                {
                    #region program init
                    //Initialize Program Variables

                    //File Locations
                    string metaDataXml_FilePath = Utilities.readAppConfigKey("metaDataXMLPath");
                    string blacklistXml_FilePath = Utilities.readAppConfigKey("blacklistXMLPath");
                    string globalXML_FilePath = Utilities.readAppConfigKey("userXMLPath");

                    //Database
                    DBOperator db = new DBOperator();

                    //Lists
                    List<NetworkConnection> mapDrives;          // Currently Mapped Drives
                    List<NetworkConnection> blacklistShares;    // Blacklisted Fileshares
                    List<NetworkConnection> xmlDrives;          // Network Drives from XML File
                    //List<NetworkConnection> allDrives;          // All Valid Known Fileshares (Clean + Known)
                    //Utility Lists
                    //List<NetworkConnection> toUnmapDrives;      // Utility List: Maps to be Unmapped
                    //List<NetworkConnection> clean_mapDrives;    // Utility List: After Unmapping and Verifying servers.
                    //List<NetworkConnection> newDrives;          // Fileshares that are being added to the allDrives list.
                    List<string> dnshosts = new List<string>(); // List to store all hosts to DNSlookup
                    List<string> DNSDomains = new List<string>();  // List of all domains of each host

                    //SQL Information (to prevent querying multiple times
                    string[] currentUser;
                    string[] currentComputer;

                    //GATHER SETTINGS FROM SQL
                    string dedupe = db.getSetting("dedupe");
                    if (dedupe != "")
                        deduplicate = Boolean.Parse(dedupe);

                    string logging = db.getSetting("logging");
                    if (logging != "")
                        logsEnabled = Boolean.Parse(dedupe);

                    //Set the Global Log Path to what is in SQL
                    string globalLogPath = db.getSetting("logPath");
                    if (globalLogPath != "")
                        globalLog.logPath = globalLogPath;

                    #endregion
                #endregion


                    //** 2.1 Read all XML data
                    output("1:\tGathering Information...");
                    stats = readMetaData(metaDataXml_FilePath);
                    blacklistShares = getXMLDrives(blacklistXml_FilePath);
                    xmlDrives = getXMLDrives(globalXML_FilePath);
                    
                    // Get List of Network Connections from WMI
                    mapDrives = getMappedDrives();

                    //Insert session information into sqlDB
                    string[] usernameArry = {Environment.UserName};
                    currentUser = db.addAndGetRow("users", "username", usernameArry[0], usernameArry);
                    string[] computerArry = { Environment.MachineName };
                    currentComputer = db.addAndGetRow("computers", "hostname", computerArry[0], computerArry);

                    //Import information into sqldatabase FROM XML
                    ShareListToSQL(db, xmlDrives, true);
                    ShareListToSQL(db, blacklistShares, true);
                    DeactivateBlacklisted(db, blacklistShares);

                    //Add mappings to database
                    //This method will unmap fileshares that are blacklisted
                    addMappingListToSQL(db, currentUser[0], currentComputer[0], mapDrives);

                    //NEED TO RECORD STATISTICS NOW
                    //CREATE REPORTS

                    /*#region PRUNE THIS
                    #region** ** 2.5.1 Write Mapped Drives to Logs
                    foreach (NetworkConnection netCon in mapDrives)
                    {
                        //run DNS Verify for logging purposes
                        try
                        {
                            netCon.DNSVerify();
                        }
                        catch (Exception e)
                        {
                            output(e.ToString());
                        }
                        output("Mapped: " + netCon.toString());
                    }
                    #endregion
                    #region** ** 2.5.2 Write Blacklisted Drives to Logs
                    foreach (NetworkConnection netCon in blacklistShares)
                    {
                        output("Blacklisted: " + netCon.toString());
                    }
                    #endregion
                    #region** ** 2.5.3 Write Known Drives to Logs
                    foreach (NetworkConnection netCon in xmlDrives)
                    {
                        output("Known Shares: " + netCon.toString());
                    }
                    #endregion

                    #region** 3 Process Information
                    //** 3.1 Compare Current to Blacklist
                    //find all mapped drives that are currently blacklisted and remove them
                    toUnmapDrives = mapDrives.Intersect(blacklistShares, new WildcardNetworkConnectionComparer()).ToList();
                    #region** ** 3.1.1 Write To Be Unmapped Drives to Logs & Record Statistics
                    foreach (NetworkConnection netCon in toUnmapDrives)
                    {
                        output("To Be Unmapped: " + netCon.toString());
                    }
                    #endregion

                    //** 3.2 Remove Blacklisted Fileshares
                    foreach (NetworkConnection netCon in toUnmapDrives)
                    {
                        try // Unmap the fileshare
                        {
                            output("\t\tUnmapping Drive: " + netCon.LocalName, true);
                            netCon.unmap();

                            //Increment the metadata
                            stats.FilesharesUnmapped = stats.FilesharesUnmapped + 1;
                            //Write to the global log
                            notice("Unmapping Drive: " + netCon.toString(), true);
                        }
                        catch (Exception e)
                        {
                            output("\t\tError unmapping Drive " + netCon.LocalName + " " + netCon.RemoteName, true);
                            output(e.ToString());
                        }
                    }

                    //** 3.4 Remove Shares that are not DNS-able and that were unmapped
                    //Remove unmapped drives from list
                    clean_mapDrives = mapDrives.Except(toUnmapDrives, new NetworkConnectionComparer()).ToList();
                    //DNS Pruning, ONLY add new shares that are DNS-able
                    foreach (NetworkConnection netCon in clean_mapDrives)
                    {
                        dnshosts.Add(netCon.getServerName());
                        if (!netCon.RDNSVerify())
                        {
                            //the host is not dns-able; do not add it to the list
                            clean_mapDrives.Remove(netCon);
                            continue;
                        }
                        else
                        {
                            netCon.DNSVerify();
                        }
                        //Write Cleaned List to Logs
                        output("Cleaned List: " + netCon.toString());
                    }
                    foreach (string domain in DNSDomains)
                    {

                    }
                    //Generate list of Newly Found Network Drives
                    newDrives = clean_mapDrives.Except(xmlDrives, new NetworkConnectionComparer()).ToList();
                    foreach (NetworkConnection netCon in newDrives)
                    {
                        
                    }

                    //** 3.5 Combine Cleaned list and Known List
                    output("4:\tGenerating list of all known Network Drives");
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

                    //Deduplicate the drive list; default = false
                    if (deduplicate)
                    {
                        allDrives = removeDuplicates(allDrives);
                    }

                    #region** ** 3.5.1 Write Complete List to Logs & Record Statistics
                    output("\t5.1:\tWrite to Log and record Statistics");
                    //Write to Log and record statistics
                    stats.FilesharesFound = 0;
                    foreach (NetworkConnection drive in allDrives)
                    {
                        output(drive.toString());

                        //write the count to metadata
                        stats.FilesharesFound = stats.FilesharesFound + 1;
                    }
                    #endregion
                    #endregion

                    #region** 4 Generate and Write Output
                    //** 4.2 Serialize and Write XML Files
                    *//*if (allDrives.Count > 0)
                    {*/
                        output("\t5.1:\tSerialize the list & Write to XML file");
                        //verify the user has access to write to the XML folder
                        
                        //Extract the path from each xml File name
                        //string globalXML_directoryPath = Utilities.trimPath(globalXML_FilePath);
                    string metaDataXML_directoryPath = Utilities.trimPath(metaDataXml_FilePath);

                    try
                    {
                        /*if (Utilities.canIWrite(globalXML_directoryPath))
                        {
                            Utilities.SerializeToFile<NetworkConnectionList>(new NetworkConnectionList(allDrives), globalXML_FilePath);
                        }*/
                        if (Utilities.canIWrite(metaDataXML_directoryPath))
                        {
                            Utilities.SerializeToFile<Statistics>(stats, metaDataXml_FilePath);
                        }
                    }
                    catch (Exception e)
                    {
                        output("\t\tError writing to file. The xml file has not been saved." + Environment.NewLine + e.ToString(), true);
                    }

                    /*}
                    else
                    {
                        output("\tNo Fileshares found.");
                    }*/
                }
                output(Environment.NewLine);
            }
            catch (Exception e)
            {
                output("Fatal Program Error:" + e.ToString(), true);
            }
        }

        #region Supplemental Methods

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

        /// <summary>Takes a list of network connection objects and adds them to a sql database
        /// </summary>
        /// <param name="db">DBOperator object to write to database</param>
        /// <param name="drives">List of network connection shares to add</param>
        /// <param name="active">Whether the network drive is blacklisted or active</param>
        private static void ShareListToSQL(DBOperator db, List<NetworkConnection> drives, bool active)
        {
            foreach (NetworkConnection netCon in drives)
            {
                addShare(db, netCon, active);
            }
        }

        /// <summary>Add Share to SQL Database - Will not add duplicate shares
        /// </summary>
        /// <param name="db">DBOperator object to write to database</param>
        /// <param name="NetCon">Network Connection object to write to database</param>
        /// <param name="active">whether the fileshare is active or inactive</param>
        private static void addShare(DBOperator db, NetworkConnection NetCon, bool active)
        {
            string[] currentServer = db.addAndGetServerNoDuplicate(NetCon.getServerName(), NetCon.Domain);
            string[] currentShare;

            if (NetCon.getShareName() != "*")
            {
                //share is not only a wildcard
                currentShare = db.addAndGetShareNoDuplicate(currentServer[0], active, NetCon.getShareName());
            }
        }

        /// <summary>Blacklists a share or server by setting the "active" column to disabled
        /// </summary>
        /// <param name="db">DBOperator object to write to database</param>
        /// <param name="drives">List of network connection shares to blacklist</param>
        private static void DeactivateBlacklisted(DBOperator db, List<NetworkConnection> drives)
        {
            //Set clause
            Dictionary<string, string> setClause = new Dictionary<string, string>();
            //Note: 'active' is the same column name in both servers and shares table
            setClause.Add("active", false.ToString());

            string[] server; //Server row from Table: Servers
            string whereClause; //Where statement for sql query

            foreach (NetworkConnection netCon in drives)
            {
                //Deactivate shares that match the server
                server = db.getRow("servers", "hostname", netCon.getServerName());

                //check for wildcard
                if (netCon.getShareName() == "*")
                {
                    //Deactivate the server
                    whereClause = string.Format("serverID='{0}' AND domain='{1}'", server[0], netCon.Domain);
                    db.updateTable("servers", setClause, whereClause);

                    //Match all shares that have the serverID that was just disabled
                    whereClause = string.Format("serverID='{0}'", server[0]);
                }
                else if (netCon.getShareName().Contains('*'))
                {
                    //partial wildcard
                    //sql uses % as a wildcard for any string
                    string sqlWildcard = netCon.getShareName().Replace(@"\*", "%");

                    //mactch all shares on both share and server
                    whereClause = string.Format("shareName='{0}' AND serverID='{1}'", sqlWildcard, server[0]);
                }
                else
                {
                    //no wildcard
                    //match on only share and server
                    whereClause = string.Format("shareName='{0}' AND serverID='{1}'", netCon.getShareName(), server[0]);
                }

                //Update "shares" table 
                db.updateTable("shares", setClause, whereClause);
            }
        }

        /// <summary>Adds shares to the database that match mapped criteria
        /// </summary>
        /// <remarks>Criteria: Server must be Pingable, Share must not be blacklisted</remarks>
        /// <param name="db">DBOperator object to write to database</param>
        /// <param name="userID">ID of the user with the mapping</param>
        /// <param name="computerID">ID of the computer with the mapping</param>
        /// <param name="drives">List of NetworkConnection objects to add to the mapping table</param>
        private static void addMappingListToSQL(DBOperator db, string userID, string computerID, List<NetworkConnection> drives)
        {
            string[] server; //server row from Table: Servers
            string[] share; //share row from Table: Shares
            DateTime dateNow = DateTime.Now;

            foreach (NetworkConnection netCon in drives)
            {
                //check existing rows
                server = db.getRow("servers", "hostname", netCon.getServerName());

                //construct the dictionary to check if share exists
                Dictionary<string, string> shareExistsCheck = new Dictionary<string,string>();
                shareExistsCheck.Add("serverID", server[0]);
                shareExistsCheck.Add("shareName", netCon.getShareName());

                //check the shares table
                share = db.getRow("shares", shareExistsCheck);

                //construct the dictionary to check if the mapping exists
                Dictionary<string, string> mappingExistsCheck = new Dictionary<string,string>();
                mappingExistsCheck.Add("shareID", share[0]);
                mappingExistsCheck.Add("computerID", computerID);
                mappingExistsCheck.Add("userID", userID);

                //construct a string array with the new row
                string[] mappingsRow = new string[6] { share[0], computerID, userID, netCon.LocalName, netCon.UserName, dateNow.ToString() };
                
                if (server.Length == 0) //if server doesnt exist in SQL
                {
                    //Check if server resolves in a DNS lookup
                    if (netCon.RDNSVerify())
                    {
                        addShare(db, netCon, true);
                        db.addRow("mappings", mappingExistsCheck, mappingsRow);
                    }
                }
                else
                {
                    if (share[3] == "false")
                    {
                        //share is blacklisted
                        netCon.unmap();
                        notice("Unmapping Drive: " + netCon.toString(), true);
                        stats.FilesharesUnmapped += 1;
                    }
                    else {
                        //Add share regardless of DNS
                        addShare(db, netCon, true);
                        db.addRow("mappings", mappingExistsCheck, mappingsRow);
                    }
                }
            }
        }

        #endregion

        #region Read from XML

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

        #endregion
        
        #region Output Methods
        /// <summary>Standard program output method, determines where to direct output
        /// </summary>
        /// <param name="message">Output Message</param>
        private static void output(string message)
        {
            logger.Write(message, logsEnabled);
        }

        /// <summary>Standard program output method, Takes boolean value to override the logging setting
        /// </summary>
        /// <remarks>Note: If the program is run with the "logging" argument, it will override this.</remarks>
        /// <param name="message">Output Message</param>
        /// <param name="print">Boolean value that overrides the default setting.</param>
        private static void output(string message, bool print)
        {
            if (logsEnabled)
            {
                logger.Write(message, true);
            }
            else
            {
                logger.Write(message, print);
            }
        }

        /// <summary>Standard program output method, determines where to direct output
        /// </summary>
        /// <param name="message">Output Message</param>
        private static void notice(string message)
        {
            globalLog.Write(Environment.UserName + "@" + Environment.MachineName + " | " + message, logsEnabled);
        }

        /// <summary>Standard program output method, Takes boolean value to override the logging setting
        /// </summary>
        /// <remarks>Note: If the program is run with the "logging" argument, it will override this.</remarks>
        /// <param name="message">Output Message</param>
        /// <param name="print">Boolean value that overrides the default setting.</param>
        private static void notice(string message, bool print)
        {
            if (logsEnabled)
            {
                globalLog.Write(Environment.UserName + "@" + Environment.MachineName + " | " + message, true);
            }
            else
            {
                globalLog.Write(Environment.UserName + "@" + Environment.MachineName + " | " + message, print);
            }
        }
        #endregion
    }
}
