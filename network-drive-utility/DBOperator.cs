using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;

namespace network_drive_utility
{
    class DBOperator
    {
        SQLiteDatabase database;
        #region constructors
        public DBOperator()
        {
            database = new SQLiteDatabase("network-drive-utility.s3db");
            if (!File.Exists("network-drive-utility.s3db"))
            {
                Create();
            }

            //Get transaction log locations
            string logPath = GetSetting("transLogLocation");
            string fileName = System.Diagnostics.Process.GetCurrentProcess().ProcessName +"_transactLog.txt";

            //Set logger object in sql database object to provide the correct path/file
            if (logPath != "")
                database.Transactlogger.logPath = logPath;
            database.Transactlogger.fileName = fileName;
        }
        #endregion

        #region insert functions

        /// <summary>Adds a new server to the servers table, will not add a duplicate
        /// </summary>
        /// <param name="serverName">Hostname of server to add</param>
        /// <param name="serverDomain">Domain of server to add</param>
        /// <returns>Row result of selecting the server</returns>
        public string[] AddAndGetServerNoDuplicate(string serverName, string serverDomain)
        {
            DateTime dateNow = DateTime.Now;
            string[] serverData = new string[3] { serverName, serverDomain, dateNow.ToString() };
            return(AddAndGetRow("servers", "hostname", serverData[0], serverData));
        }

        /// <summary>Adds a new share to the shares table, will not add a duplicate
        /// </summary>
        /// <param name="serverID">ID number of the server the share resides on</param>
        /// <param name="active">Whether the share is active or not</param>
        /// <param name="shareName">Shared name of the shared drive</param>
        /// <returns>Row result of selecting the share</returns>
        public string[] AddAndGetShareNoDuplicate(string serverID, bool active, string shareName)
        {
            string[] netConShareData = new string[3] { serverID, shareName, active.ToString() };
            return (AddAndGetRow("shares", "shareName", netConShareData[1], netConShareData));
        }

        /// <summary>Function that validates data existence before adding a duplicate row
        /// </summary>
        /// <remarks>This does not guarantee duplicates, it is up to the programmer to check enough columns</remarks>
        /// <param name="table">Table to write data to</param>
        /// <param name="checkColumn">Column to check against</param>
        /// <param name="dataCheck">Value to check against column</param>
        /// <param name="dataToAdd">The row to be added in array format</param>
        /// <returns>Array value of the row as it exists in the database</returns>
        public string[] AddAndGetRow(string table, string checkColumn, string dataCheck, string[] dataToAdd)
        {
            //construct the dictionary
            Dictionary<string, string> columnChecks = new Dictionary<string, string>();
            columnChecks.Add(checkColumn, dataCheck);

            //add the row
            return(AddAndGetRow(table, columnChecks, dataToAdd));
        }

        /// <summary>Function that validates data existence before adding a duplicate row
        /// </summary>
        /// <remarks>This does not guarantee duplicates, it is up to the programmer to check enough columns</remarks>
        /// <param name="table">Table to write data to</param>
        /// <param name="checkColumn">Column to check against</param>
        /// <param name="dataCheck">Value to check against column</param>
        /// <param name="dataToAdd">The row to be added in array format</param>
        /// <returns>Array value of the row as it exists in the database</returns>
        public string[] AddAndGetRow(string table, string checkColumn, string dataCheck, string dataToAdd)
        {
            //construct the dictionary
            Dictionary<string, string> columnChecks = new Dictionary<string, string>();
            columnChecks.Add(checkColumn, dataCheck);

            //create single-element array
            string[] _dataToAdd = { dataToAdd };

            //add the row
            return (AddAndGetRow(table, columnChecks, _dataToAdd));
        }

        /// <summary>Function that validates data existence before adding a duplicate row
        /// </summary>
        /// <remarks>This does not guarantee duplicates, it is up to the programmer to check enough columns</remarks>
        /// <param name="table">Table to write data to</param>
        /// <param name="columnChecks">Column to check against</param>
        /// <param name="dataToAdd">The row to be added in Array Format</param>
        /// <returns>Row as it exists in the database</returns>
        public string[] AddAndGetRow(string table, Dictionary<string, string> columnChecks, string[] dataToAdd)
        {
            AddRow(table, columnChecks, dataToAdd);
            return(this.GetRow(table, columnChecks));
        }

        /// <summary>Function that validates data existence before adding a duplicate row
        /// </summary>
        /// <param name="table">Table to write data to</param>
        /// <param name="columnChecks">Dictionary storing Columns and Values to check against</param>
        /// <param name="dataToAdd">The row to be added in array format</param>
        public void AddRow(string table, Dictionary<string, string> columnChecks, string[] dataToAdd)
        {
            string[] existingRow = this.GetRow(table, columnChecks);
            if (existingRow.Length == 0)
            {
                //the row does not exist
                switch (table)
                {
                    case "users":
                        AddNewUser(dataToAdd);
                        break;
                    case "master":
                        AddNewSetting(dataToAdd);
                        break;
                    case "computers":
                        AddNewComputer(dataToAdd);
                        break;
                    case "shares":
                        AddNewShare(dataToAdd);
                        break;
                    case "servers":
                        AddNewServer(dataToAdd);
                        break;
                    case "mappings":
                        AddNewMapping(dataToAdd);
                        break;
                    default:
                        //table not known, don't add
                        break;
                }
            }
        }

        #region public void addNew<Table>
        public void AddNewSetting(string[] dataToAdd)
        {
            //Call overloaded function
            AddNewSetting(dataToAdd[0], dataToAdd[1]);
        }

        public void AddNewSetting(string setting, string value)
        {
            //store the column and values to insert in a dictionary
            Dictionary<string, string> insertSetting = new Dictionary<string, string>();
            insertSetting.Add("setting", setting);
            insertSetting.Add("value", value);

            //execute sql insert command with the dictionary
            database.Insert("master", insertSetting);
        }

        public void AddNewUser(string[] dataToAdd)
        {
            //store the column and values to insert in a dictionary
            Dictionary<string, string> insertUser = new Dictionary<string,string>();
            insertUser.Add("username", dataToAdd[0]);

            //execute sql insert command with the dictionary
            database.Insert("users", insertUser);
        }

        public void AddNewComputer(string[] dataToAdd)
        {
            //store the column and values to insert in a dictionary
            Dictionary<string, string> insertComputer = new Dictionary<string, string>();
            insertComputer.Add("hostname", dataToAdd[0]);

            //execute sql insert command with the dictionary
            database.Insert("computers", insertComputer);
        }

        public void AddNewServer(string[] dataToAdd)
        {
            //store the column and values to insert in a dictionary
            Dictionary<string, string> insertServer = new Dictionary<string, string>();
            insertServer.Add("hostname", dataToAdd[0]);
            insertServer.Add("active", "true");
            insertServer.Add("domain", dataToAdd[1]);
            insertServer.Add("date", dataToAdd[2]);

            //execute sql insert command with the dictionary
            database.Insert("servers", insertServer);
        }

        public void AddNewShare(string[] dataToAdd)
        {
            string active = true.ToString();
            if (dataToAdd.Length == 3)
            {
                active = dataToAdd[2];
            }
            //store the column and values to insert in a dictionary
            Dictionary<string, string> insertShare = new Dictionary<string, string>();
            insertShare.Add("serverID", dataToAdd[0]);
            insertShare.Add("shareName", dataToAdd[1]);
            insertShare.Add("active", active);

            //execute sql insert command with the dictionary
            database.Insert("shares", insertShare);
        }

        public void AddNewMapping(string[] dataToAdd)
        {
            //store the column and values to insert in a dictionary
            Dictionary<string, string> insertMapping = new Dictionary<string, string>();
            insertMapping.Add("shareID", dataToAdd[0]);
            insertMapping.Add("userID", dataToAdd[1]);
            insertMapping.Add("computerID", dataToAdd[2]);
            insertMapping.Add("letter", dataToAdd[3]);
            insertMapping.Add("username", dataToAdd[4]);
            insertMapping.Add("date", dataToAdd[5]);

            //execute sql insert command with the dictionary
            database.Insert("mappings", insertMapping);
        }
        #endregion

        #endregion

        #region query functions

        /// <summary>Gets the setting from the master table
        /// </summary>
        /// <param name="settingName">Key for the table</param>
        /// <returns>string in the setting column, or String.Empty if nothing found</returns>
        public string GetSetting(string settingName)
        {
            string[] row = GetRow("master", "setting", settingName);
            if (row.Length == 0)
            {
                return "";
            }
            else
                return row[2];
        }

        /// <summary>Queries a database and returns a row from a table that matches certain criteria
        /// </summary>
        /// <param name="tableName">Table to query</param>
        /// <param name="column">Column to check against</param>
        /// <param name="value">Value to find in column</param>
        /// <returns>First row returned from query</returns>
        public string[] GetRow(string tableName, string column, string value)
        {
            Dictionary<string, string> columnChecks = new Dictionary<string, string>();
            columnChecks.Add(column, value);

            return (GetRow(tableName, columnChecks));
        }

        /// <summary>Queries a database and returns a row from a table that matches certain criteria
        /// </summary>
        /// <remarks>Single field where statement select query</remarks>
        /// <param name="tableName">Table to query</param>
        /// <param name="columnChecks">Column and Values to check against</param>
        /// <returns>First row returned from query</returns>
        public string[] GetRow(string tableName, Dictionary<string,string> columnChecks)
        {
            List<string> columnList = new List<string>(); //used to store dictionary

            //first part of select statement
            string selectStatement = string.Format("SELECT * from {0} where ", tableName);

            //Iterate through dictionary and build list
            foreach (KeyValuePair<String, String> row in columnChecks)
            {
                columnList.Add(string.Format("{0} == '{1}'", row.Key, row.Value));
            }

            //Append the where criteria to the select statement
            selectStatement += string.Join(" AND ", columnList.ToArray());

            //Perform Query
            return GetRow(selectStatement);
        }


        /// <summary>Queries a database and returns a row from the table that matches certain criteria
        /// </summary>
        /// <remarks>Will only select the first row if multiple rows match</remarks>
        /// <param name="selectClause">SQL Select statement to execute</param>
        /// <returns>first found Row in string array format</returns>
        private string[] GetRow(string selectClause)
        {
            List<string> row = new List<string>();

            DataTable result = database.GetDataTable(selectClause);
            if (result.Rows.Count > 0)
            {
                for (int i = 0; i < result.Columns.Count; i++)
                {
                    row.Add(result.Rows[0][i].ToString());
                }
            }

            return row.ToArray<string>();
        }
        #endregion

        #region update functions

        /// <summary>Basic Update Executer, executes an update sql command
        /// </summary>
        /// <remarks>This is only necessary because the main program is not accessing the sqlDatabase class directly.</remarks>
        /// <param name="table">table to update</param>
        /// <param name="setOperations">columns to set</param>
        /// <param name="whereClause">conditions for subset of rows to update</param>
        public void UpdateTable(string table, Dictionary<string, string> setOperations, string whereClause)
        {
            database.Update(table, setOperations, whereClause);
        }
        #endregion

        //create new database
        public void Create()
        {

            #region initiaize databases & tables
            //query strings
            string create_TblMaster = @"CREATE TABLE [master](
                [ID] integer NOT NULL,
                [setting] text,
                [value] text,
                PRIMARY KEY (ID))";
            string create_TblUsers = @"CREATE TABLE [users](
                [userID] integer NOT NULL,
                [username] text,
                PRIMARY KEY(userID))";
            string create_TblComputers = @"CREATE TABLE [computers](
                [computerID] integer NOT NULL,
                [hostname] VARCHAR(15),
                PRIMARY KEY(computerID))";
            string create_TblServers = @"CREATE TABLE [servers](
                [serverID] integer NOT NULL,
                [hostname] VARCHAR(15),
                [active] boolean NOT NULL,
                [domain] VARCHAR(255),
                [date] VARCHAR(255),
                PRIMARY KEY(serverID))";
            string create_TblShares = @"CREATE TABLE [shares](
                [shareID] integer NOT NULL, 
                [serverID] integer NOT NULL,
                [shareName] VARCHAR(255),
                [active] boolean NOT NULL,
                FOREIGN KEY(serverID) REFERENCES [servers](serverID),
                PRIMARY KEY(shareID))";
            string create_TblMappings = @"CREATE TABLE [mappings](
                [shareID] integer NOT NULL,
                [computerID] integer NOT NULL,
                [userID] integer NOT NULL,
                [letter] NVARCHAR(1),
                [username] text,
                [date] VARCHAR(255),
                FOREIGN KEY (shareID) REFERENCES [shares](shareID),
                FOREIGN KEY (computerID) REFERENCES [computers](computerID),
                FOREIGN KEY (userID) REFERENCES [users](userID),
                PRIMARY KEY (shareID, computerID, userID))";

            //create db file & connect to it
            SQLiteConnection.CreateFile("network-drive-utility.s3db");
            SQLiteConnection dbConnection = new SQLiteConnection("Data Source=network-drive-utility.s3db;");
            dbConnection.Open();

            //create sqlcommands
            SQLiteCommand create_master = new SQLiteCommand(create_TblMaster, dbConnection);
            SQLiteCommand create_users = new SQLiteCommand(create_TblUsers, dbConnection);
            SQLiteCommand create_computers = new SQLiteCommand(create_TblComputers, dbConnection);
            SQLiteCommand create_servers = new SQLiteCommand(create_TblServers, dbConnection);
            SQLiteCommand create_shares = new SQLiteCommand(create_TblShares, dbConnection);
            SQLiteCommand create_mappings = new SQLiteCommand(create_TblMappings, dbConnection);

            //execute all queries
            create_master.ExecuteNonQuery();
            create_users.ExecuteNonQuery();
            create_computers.ExecuteNonQuery();
            create_servers.ExecuteNonQuery();
            create_shares.ExecuteNonQuery();
            create_mappings.ExecuteNonQuery();
            #endregion

            //set default settings
            AddNewSetting("logPath", Utilities.readAppConfigKey("logPath"));
            AddNewSetting("userXMLPath", Utilities.readAppConfigKey("userXMLPath"));
            AddNewSetting("blacklistXMLPath", Utilities.readAppConfigKey("blacklistXMLPath"));
            AddNewSetting("metaDataXMLPath", Utilities.readAppConfigKey("metaDataXMLPath"));
            AddNewSetting("logging", "false");
            AddNewSetting("dedupe", "false");

            //close connection
            dbConnection.Close();
        }
    }
}
