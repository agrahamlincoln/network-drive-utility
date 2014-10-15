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
                        
                    //Extract the path from xml File name
                    string metaDataXML_directoryPath = Utilities.trimPath(metaDataXml_FilePath);

                    try
                    {
                        if (Utilities.canIWrite(metaDataXML_directoryPath))
                        {
                            Utilities.SerializeToFile<Statistics>(stats, metaDataXml_FilePath);
                        }
                    }
                    catch (Exception e)
                    {
                        output("\t\tError writing to file. The xml file has not been saved." + Environment.NewLine + e.ToString(), true);
                    }
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
