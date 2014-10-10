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
            database = new SQLiteDatabase();
            if (!File.Exists("network-drive-utility.s3db"))
            {
                Create();
            }
        }
        #endregion

        #region insert functions

        /// <summary>Function that validates data existence before adding a duplicate row
        /// </summary>
        /// <remarks>This does not guarantee duplicates, it only checks a single column</remarks>
        /// <param name="table">Table to write data to</param>
        /// <param name="checkColumn">Column to check against</param>
        /// <param name="dataCheck">Value to check against column</param>
        /// <param name="dataToAdd">The row to be added in array format</param>
        /// <returns>Array value of the row as it exists in the database</returns>
        public string[] addRow(string table, string checkColumn, string dataCheck, string[] dataToAdd)
        {
            string[] existingRow = this.getRow(table, checkColumn, dataCheck);
            if (existingRow.Length == 0)
            {
                //the row does not exist
                switch (table)
                {
                    case "users":
                        addNewUser(dataToAdd);
                        break;
                    case "master":
                        addNewSetting(dataToAdd);
                        break;
                    case "computers":
                        addNewComputer(dataToAdd);
                        break;
                    case "shares":
                        addNewShare(dataToAdd);
                        break;
                    case "servers":
                        addNewServer(dataToAdd);
                        break;
                    case "mappings":
                        addNewMapping(dataToAdd);
                        break;
                    default:
                        //table not known, don't add
                        break;
                }
                existingRow = this.getRow(table, checkColumn, dataCheck);
            }
            return existingRow;
        }

        public void addNewSetting(string[] dataToAdd)
        {
            //store the column and values to insert in a dictionary
            Dictionary<string, string> insertSetting = new Dictionary<string, string>();
            insertSetting.Add("setting", dataToAdd[0]);
            insertSetting.Add("value", dataToAdd[1]);

            //execute sql insert command with the dictionary
            database.Insert("master", insertSetting);
        }

        public void addNewUser(string[] dataToAdd)
        {
            //store the column and values to insert in a dictionary
            Dictionary<string, string> insertUser = new Dictionary<string,string>();
            insertUser.Add("username", dataToAdd[0]);

            //execute sql insert command with the dictionary
            database.Insert("users", insertUser);
        }

        public void addNewComputer(string[] dataToAdd)
        {
            //store the column and values to insert in a dictionary
            Dictionary<string, string> insertComputer = new Dictionary<string, string>();
            insertComputer.Add("hostname", dataToAdd[0]);

            //execute sql insert command with the dictionary
            database.Insert("computers", insertComputer);
        }

        public void addNewServer(string[] dataToAdd)
        {
            //store the column and values to insert in a dictionary
            Dictionary<string, string> insertServer = new Dictionary<string, string>();
            insertServer.Add("hostname", dataToAdd[0]);
            insertServer.Add("active", "true");
            insertServer.Add("domain", dataToAdd[1]);

            //execute sql insert command with the dictionary
            database.Insert("servers", insertServer);
        }

        public void addNewShare(string[] dataToAdd)
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

        public void addNewMapping(string[] dataToAdd)
        {
            //store the column and values to insert in a dictionary
            Dictionary<string, string> insertMapping = new Dictionary<string, string>();
            insertMapping.Add("shareID", dataToAdd[0]);
            insertMapping.Add("userID", dataToAdd[1]);
            insertMapping.Add("computerID", dataToAdd[2]);
            insertMapping.Add("letter", dataToAdd[3]);

            //execute sql insert command with the dictionary
            database.Insert("mappings", insertMapping);
        }

        #endregion

        #region query functions

        /// <summary>Queries a database and returns a row from the table that matches certain criteria
        /// </summary>
        /// <remarks>Will only select the first row if multiple rows match</remarks>
        /// <param name="tableName">table to query</param>
        /// <param name="columnQuery">column in table to match search term</param>
        /// <param name="searchTerm">value to match with a column to select only a certain row</param>
        /// <returns></returns>
        public string[] getRow(string tableName, string columnQuery, string searchTerm)
        {
            List<string> row = new List<string>();

            string qry = string.Format("SELECT * from [{0}] where [{1}] == '{2}'", tableName, columnQuery, searchTerm);
            DataTable result = database.GetDataTable(qry);
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
        public void updateTable(string table, Dictionary<string, string> setOperations, string whereClause)
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
            addNewSetting("logPath", Utilities.readAppConfigKey("logPath"));
            addNewSetting("userXMLPath", Utilities.readAppConfigKey("userXMLPath"));
            addNewSetting("blacklistXMLPath", Utilities.readAppConfigKey("blacklistXMLPath"));
            addNewSetting("metaDataXMLPath", Utilities.readAppConfigKey("metaDataXMLPath"));
            addNewSetting("logging", "false");
            addNewSetting("dedupe", "false");

            //close connection
            dbConnection.Close();
        }
    }
}
