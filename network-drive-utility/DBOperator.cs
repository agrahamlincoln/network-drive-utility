using System;
using System.Collections.Generic;
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
            if (!File.Exists("network-drive-utility.s3db"))
            {
                Create();
            }

            database = new SQLiteDatabase();
        }
        #endregion

        #region insert functions
        public void addNewSetting(string setting, string value)
        {
            //store the column and values to insert in a dictionary
            Dictionary<string, string> insertSetting = new Dictionary<string, string>();
            insertSetting.Add("setting", setting);
            insertSetting.Add("value", value);

            //execute sql insert command with the dictionary
            database.Insert("master", insertSetting);
        }

        public void addNewUser(string username)
        {
            //store the column and values to insert in a dictionary
            Dictionary<string, string> insertUser = new Dictionary<string,string>();
            insertUser.Add("username", username);

            //execute sql insert command with the dictionary
            database.Insert("users", insertUser);
        }

        public void addNewComputer(string hostname)
        {
            //store the column and values to insert in a dictionary
            Dictionary<string, string> insertComputer = new Dictionary<string, string>();
            insertComputer.Add("hostname", hostname);

            //execute sql insert command with the dictionary
            database.Insert("computers", insertComputer);
        }

        public void addNewServer(string hostname, string domain)
        {
            //store the column and values to insert in a dictionary
            Dictionary<string, string> insertServer = new Dictionary<string, string>();
            insertServer.Add("hostname", hostname);
            insertServer.Add("domain", domain);

            //execute sql insert command with the dictionary
            database.Insert("servers", insertServer);
        }

        public void addNewShare(int serverID, string shareName)
        {
            //store the column and values to insert in a dictionary
            Dictionary<string, string> insertShare = new Dictionary<string, string>();
            insertShare.Add("serverID", serverID.ToString());
            insertShare.Add("shareName", shareName);

            //execute sql insert command with the dictionary
            database.Insert("shares", insertShare);
        }

        public void addNewShare(int serverID, string shareName)
        {
            //store the column and values to insert in a dictionary
            Dictionary<string, string> insertShare = new Dictionary<string, string>();
            insertShare.Add("serverID", serverID.ToString());
            insertShare.Add("shareName", shareName);

            //execute sql insert command with the dictionary
            database.Insert("shares", insertShare);
        }

        public void addNewMapping(int shareID, int userId, int computerID, string letter)
        {
            //store the column and values to insert in a dictionary
            Dictionary<string, string> insertMapping = new Dictionary<string, string>();
            insertMapping.Add("shareID", shareID.ToString());
            insertMapping.Add("userID", userId.ToString());
            insertMapping.Add("computerID", computerID.ToString());
            insertMapping.Add("letter", letter);

            //execute sql insert command with the dictionary
            database.Insert("mappings", insertMapping);
        }
        #endregion

        public bool exists(string table, string column, string value)
        {

        }

        //create new database
        public void Create()
        {

            #region initiaize databases & tables
            //query strings
            string create_TblMaster = @"CREATE TABLE [master](
                [ID] integer NOT NULL,
                [setting] text,
                [value] text,
                PRIMARY KEY[ID])";
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
