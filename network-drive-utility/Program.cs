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
    static class Program
    {
        private static bool logsEnabled = true;
        private static LogWriter logger = new LogWriter(); //Local logger
        private static LogWriter globalLog = new LogWriter("Log.txt"); //Global Logger
        private static DBOperator db;

        /// <summary>Main Entrypoint for the Program
        /// </summary>
        /// <param name="args">Program arguments</param>
        public static void Main(string[] args)
        {
            try
            {
                //** 1.1 Write Log Header
                Output(logger.header(), true);

                //** 1.3 Verify Program Compatability
                if (!Utilities.HasNet35())
                {
                    Output("Error: .NET 3.5 or greater is not installed", true);
                }
                else
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
                    logsEnabled = Utilities.parseBool(db.GetSetting("logging"));

                    //Get List of Network Connections from WMI
                    List<NetworkConnection> mapDrives = GetMappedDrives();

                    //SQL Information (to prevent querying multiple times
                    User currentUser = new User(Environment.UserName);
                    if (!currentUser.exists)
                        currentUser.addToDB();

                    Computer currentComputer = new Computer(Environment.MachineName);
                    if (!currentComputer.exists)
                        currentComputer.addToDB();

                    #endregion

                    //Add mappings to database
                    //This method will also unmap fileshares that are blacklisted
                    AddMappingListToSQL(currentUser, currentComputer, mapDrives);
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

            try
            {
                mappedDrives = NetworkConnection.ListCurrentlyMappedDrives();
            }
            catch (Exception crap)
            {
                Output(crap.ToString());
                mappedDrives = new List<NetworkConnection>();
            }

            return mappedDrives;
        }

        /// <summary>Adds shares to the database that match mapped criteria
        /// </summary>
        /// <remarks>Criteria: Server must be Pingable, Share must not be blacklisted</remarks>
        /// <param name="user">User with the mapping</param>
        /// <param name="computer">Computer with the mapping</param>
        /// <param name="drives">List of NetworkConnection objects to add to the mapping table</param>
        private static void AddMappingListToSQL(User user, Computer computer, List<NetworkConnection> drives)
        {
            List<Server> servers = new List<Server>();  //List of servers mapped
            DateTime dateNow = DateTime.Now;

            foreach (NetworkConnection netCon in drives)
            {
                //check if server has been used before
                var server = servers.FirstOrDefault(srvr => (srvr.hostname == netCon.getServerName()) &&
                                                            (srvr.domain == netCon.Domain));
                if (server == null)
                {
                    //Create and Validate a new Server Object
                    server = new Server(netCon.getServerName(), netCon.Domain);

                    if (server.dnsVerify())
                    {
                        //DNS Resolves, we can add the server to the DB
                        string msg = string.Format("{0,20}: {1}.{2}", "Found New Server", netCon.getServerName(), netCon.Domain);
                        Notice(msg, true);

                        if (!server.exists)
                            server.addToDB();

                        servers.Add(server);
                    }
                    else
                    {
                        //DNS Does not Resolve
                        //Don't add the share, don't add the server, don't unmap share
                        continue; //Proceed to next share in mapped list
                    }
                }

                //Unmap share if server is inactive!
                if (!server.active)
                {
                    netCon.unmap();

                    string msg = string.Format("{0,20}: {1}", "Unmapping Drive", netCon.toString());
                    Notice(msg, true);
                }

                //create a share object to represent this share
                var share = new Share(netCon.getShareName(), server);
                if (!share.exists)
                {
                    share.addToDB();
                    //share does not exist in DB: Add it
                    string msg = string.Format("{0,20}: {1,30} Domain: {2,-25}", "Discovered New Drive", netCon.RemoteName, netCon.Domain);
                    Notice(msg, true);
                }

                //check if share is blacklisted
                if (!share.active)
                {
                    netCon.unmap();

                    string notice = string.Format("{0,20}: {1}", "Unmapping Drive", netCon.toString());
                    Notice(notice, true);

                }
                else //Add the mapping to the DB
                {
                    var mapping = new Mapping(share, computer, user, netCon.LocalName, netCon.UserName);

                    if (!mapping.exists)
                        mapping.addToDB();
                }
            }
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
