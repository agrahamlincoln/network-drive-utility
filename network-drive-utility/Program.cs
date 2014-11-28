using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using cSharpUtils;

namespace network_drive_utility
{
    /// <summary>Primary class of the application, handles higher level program logic.
    /// </summary>
    static class Program
    {
        private static bool logsEnabled = true;
        private static LogWriter logger = new LogWriter(); //Local logger
        private static LogWriter globalLog = new LogWriter("Log.txt"); //Global Logger
        private static Statistics stats;  // Metadata Object
        private static DBOperator db;

        /// <summary>Main Entrypoint for the Program
        /// </summary>
        /// <param name="args">Program arguments</param>
        public static void Main(string[] args)
        {
            try
            {
                //** 1.1 Write Log Header
                Output("Running: " + typeof(Program).Assembly.FullName, true);
                Output(logger.header(), true);
                bool compatible = false; // .NET compatability

                //** 1.3 Verify Program Compatability
                using (DotNetVersionChecker dotNet = new DotNetVersionChecker())
                {
                    //version is in the form "#.#.#.#"
                    int[] version = dotNet.GetHighestDotNetVersion().Split('.').Select(n => Convert.ToInt32(n)).ToArray();
                    if (version[0] > 3)
                        compatible = true;
                    else if (version[0] == 3 && version[1] >= 5)
                        compatible = true;
                    else
                        compatible = false;
                    Output("Running .NET version: " + version);
                }

                if (compatible)
                {
                    #region program init

                    //Get DataDir from App Config
                    string dataPath = Utilities.ReadAppConfigKey("dataDir");

                    //Init Database
                    db = new DBOperator(dataPath);

                    //Get DataDir from Database, we will use this from now on.
                    string dbDataPath = db.GetSetting("dataDir");
                    if (dbDataPath != "")
                        if (dataPath == "")
                            dataPath = dbDataPath;

                    //Initialize global logger
                    globalLog.logPath = dataPath;

                    //GATHER SETTINGS FROM SQL
                    logsEnabled = StringUtils.parseBool(db.GetSetting("logging"));

                    //Get List of Network Connections from WMI
                    List<NetworkConnection> mapDrives = GetMappedDrives(); 

                    //SQL Information (to prevent querying multiple times
                    string[] currentUser;
                    string[] currentComputer;

                    #endregion

                    if (Utilities.ReadAppConfigKey("XMLdir") != "")
                        // Convert deprecated xml files to sql
                        XML_to_SQL(db);

                    //Insert session information into sqlDB
                    currentUser = db.AddAndGetRow("users", "username", Environment.UserName, Environment.UserName);
                    currentComputer = db.AddAndGetRow("computers", "hostname", Environment.MachineName, Environment.MachineName);

                    //Add mappings to database
                    //This method will also unmap fileshares that are blacklisted
                    AddMappingListToSQL(db, currentUser[0], currentComputer[0], mapDrives);
                }
                else 
                {
                    Output("Error: .NET 3.5 or greater is not installed", true);
                }
                Output("Program Exited Successfully." + Environment.NewLine);
            }
            catch (Exception e)
            {
                Output("Fatal Program Error:" + e.ToString(), true);
            }
        }

        #region Supplemental Methods

        /// <summary>Generates list of currently mapped drives
        /// </summary>
        /// <remarks>This is used to simplify the main program and to handle exceptions thrown</remarks>
        /// <returns>List of NetworkConnections with all currently mapped drives.</returns>
        private static List<NetworkConnection> GetMappedDrives()
        {
            List<NetworkConnection> mappedDrives;

            try { 
                mappedDrives = NetworkConnection.ListCurrentlyMappedDrives(); 
            }
            catch (Exception crap)
            {
                Output(crap.ToString());
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
                AddShare(db, netCon, active);
        }

        /// <summary>Add Share to SQL Database - Will not add duplicate shares
        /// </summary>
        /// <param name="db">DBOperator object to write to database</param>
        /// <param name="NetCon">Network Connection object to write to database</param>
        /// <param name="active">whether the fileshare is active or inactive</param>
        private static void AddShare(DBOperator db, NetworkConnection NetCon, bool active)
        {
            string[] currentServer = db.AddAndGetServer(NetCon.getServerName(), NetCon.Domain);

            if (NetCon.getShareName() != "*")
            {
                //share is not only a wildcard
                db.AddAndGetShare(currentServer[0], active, NetCon.getShareName());
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
                server = db.GetRow("servers", "hostname", netCon.getServerName());

                //check for wildcard
                if (netCon.getShareName() == "*")
                {
                    //Deactivate the server
                    whereClause = string.Format("serverID='{0}' AND domain='{1}'", server[0], netCon.Domain);
                    db.UpdateTable("servers", setClause, whereClause);

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
                db.UpdateTable("shares", setClause, whereClause);
            }
        }

        /// <summary>Adds shares to the database that match mapped criteria
        /// </summary>
        /// <remarks>Criteria: Server must be Pingable, Share must not be blacklisted</remarks>
        /// <param name="db">DBOperator object to write to database</param>
        /// <param name="userID">ID of the user with the mapping</param>
        /// <param name="computerID">ID of the computer with the mapping</param>
        /// <param name="drives">List of NetworkConnection objects to add to the mapping table</param>
        private static void AddMappingListToSQL(DBOperator db, string userID, string computerID, List<NetworkConnection> drives)
        {
            string[] server; //server row from Table: Servers
            string[] share; //share row from Table: Shares
            List<string[]> savedMappings = db.GetUserMappings(userID); //all rows for this user in mappings
            DateTime dateNow = DateTime.Now;

            foreach (NetworkConnection netCon in drives)
            {
                //check existing rows
                server = db.GetRow("servers", "hostname", netCon.getServerName());

                if (server.Length == 0)
                {
                    //server does not exist in SQL
                    if (netCon.RDNSVerify())
                    {
                        //DNS resolves: add server to SQL
                        string notice = string.Format("{0,20}: {1}.{2}", "Found New Server", netCon.getServerName(), netCon.Domain);
                        Notice(notice, true);
                        server = db.AddAndGetServer(netCon.getServerName(), netCon.Domain);
                    }
                    else
                    {
                        //Server does not resolve & does not exist in SQL
                        //Don't add share, don't add server, don't unmap share
                        continue; //Proceed to the next share
                    }
                }

                //construct the dictionary to check if share exists
                Dictionary<string, string> shareExistsCheck = new Dictionary<string, string>();
                shareExistsCheck.Add("serverID", server[0]);
                shareExistsCheck.Add("shareName", netCon.getShareName());

                //check the shares table
                share = db.GetRow("shares", shareExistsCheck);

                if (share.Length == 0)
                {
                    //share does not exist in SQL: add it
                    string notice = string.Format("{0,20}: {1,30} Domain: {2,-25}", "Discovered New Drive", netCon.RemoteName, netCon.Domain);
                    Notice(notice, true);
                    AddShare(db, netCon, true);

                    //requery the share
                    share = db.GetRow("shares", shareExistsCheck);
                }

                if (share[3] == "false")
                {
                    //share is blacklisted
                    netCon.unmap();
                    string notice = string.Format("{0,20}: {1}", "Unmapping Drive", netCon.toString());
                    Notice(notice, true);
                    stats.FilesharesUnmapped += 1;
                }
                else //Add the mapping to the DB
                {
                    //Verify if the user already has it mapped or not.
                    Output("DEBUG POINT", true);
                    string[] row = savedMappings.Find(x => x[0].Contains(share[0]));

                    if (row.Length > 0)
                    {
                        //share exists
                        Output("Map Exists", true);
                    }
                    else
                    {
                        //construct a string array with the new row
                        string[] mappingsRow = new string[6] { share[0], computerID, userID, netCon.LocalName, netCon.UserName, dateNow.ToString() };

                        db.AddNewRow("mappings", mappingsRow);
                    }
                }
            }
        }

        #endregion

        #region Read from XML

        /// <summary>Generates a list of all network connections from the XML file
        /// </summary>
        /// <returns>Returns list of all network connections from XML file; Returns empty list if file doesnt exist</returns>
        private static List<NetworkConnection> GetXMLDrives(string filePath)
        {
            List<NetworkConnection> xmlDrives = new List<NetworkConnection>();
            try
            {
                if (File.Exists(filePath))
                {
                    Output("XML file exists");
                    string xmlFile = FileOperations.readFile(filePath);
                    xmlDrives = Utilities.Deserialize<NetworkConnectionList>(xmlFile).Items.ToList();
                }
                else
                {
                    Output("XML file does not exist");
                }
            }
            catch (Exception crap)
            {
                Output("Error Deserializing the File: \n" + filePath);
                Output("Stack trace for error: " + crap.ToString());
            }
            return xmlDrives;
        }

        /// <summary>DeSerializes the MetaData XML file
        /// </summary>
        /// <param name="filePath">Path of the MetaData XML file</param>
        /// <returns>Statistics object with information from MetaData XML File</returns>
        private static Statistics ReadMetaData(string filePath)
        {
            Statistics stats = new Statistics();

            try
            {
                if (File.Exists(filePath))
                {
                    Output("XML file Exists");
                    string xmlFile = FileOperations.readFile(filePath);
                    stats = Utilities.Deserialize<Statistics>(xmlFile);
                }
                else
                {
                    Output("MetaData XML file doesnt exist");
                }
            }
            catch
            {
                Output("Could not locate/access the XML File in the path:" + Environment.NewLine + filePath);
            }

            return stats;
        }

        /// <summary>Converts all XML files into SQL
        /// </summary>
        /// <remarks>XML File paths must be stored in the app.config file.</remarks>
        /// <param name="db">Database Operator to connect to database</param>
        private static void XML_to_SQL(DBOperator db)
        {
            //File Locations
            string xmlDir = Utilities.ReadAppConfigKey("XMLdir");

            List<NetworkConnection> blacklistShares;    // Blacklisted Fileshares
            List<NetworkConnection> xmlDrives;          // Network Drives from XML File

            stats = ReadMetaData(xmlDir + "\\MetaData.xml");
            blacklistShares = GetXMLDrives(xmlDir + "\\blacklist.xml");
            xmlDrives = GetXMLDrives(xmlDir + "\\global.xml");

            //Import information into sqldatabase FROM XML
            ShareListToSQL(db, xmlDrives, true);
            ShareListToSQL(db, blacklistShares, true);
            DeactivateBlacklisted(db, blacklistShares);
        }

        #endregion
        
        #region Output Methods
        /// <summary>Standard program output method, determines where to direct output
        /// </summary>
        /// <param name="message">Output Message</param>
        private static void Output(string message)
        {
            logger.Write(message, logsEnabled);
        }

        /// <summary>Standard program output method, Takes boolean value to override the logging setting
        /// </summary>
        /// <remarks>Note: If the program is run with the "logging" argument, it will override this.</remarks>
        /// <param name="message">Output Message</param>
        /// <param name="print">Boolean value that overrides the default setting.</param>
        private static void Output(string message, bool print)
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
        private static void Notice(string message, bool print)
        {
            string logMessage = string.Format("{0,15}@{1,-15} | {2}", Environment.UserName, Environment.MachineName, message);
            if (logsEnabled)
            {
                globalLog.Write(logMessage, true);
            }
            else
            {
                globalLog.Write(logMessage, print);
            }
        }
        #endregion
    }
}
