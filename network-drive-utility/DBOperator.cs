using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;

namespace network_drive_utility
{
    internal class DBOperator
    {
        internal SQLiteDatabase database;
        #region constructors

        /// <summary>No-Arg constructor
        /// </summary>
        public DBOperator() : this("") { }

        /// <summary>folderPath constructor
        /// </summary>
        /// <param name="folderPath">the folder in which the database exists in</param>
        public DBOperator(string folderPath)
        {
            string fullPath = "\\" + folderPath + "\\network-drive-utility.s3db";
            database = new SQLiteDatabase(fullPath);
            if (!File.Exists(fullPath))
            {
                Create(folderPath);
            }

            //Get transaction log locations
            string logPath = GetSetting("dataDir");
            string fileName = System.Diagnostics.Process.GetCurrentProcess().ProcessName + "_transactLog.txt";

            //Set logger object in sql database object to provide the correct path/file
            if (logPath != "")
                database.Transactlogger.logPath = logPath;
            database.Transactlogger.fileName = fileName;
        }
        #endregion

        /// <summary>Determines in the currentDB is older than 5 minutes old
        /// </summary>
        /// <remarks>Realistically, the program will not run for more than 5 minutes, so this should only need to be called once.</remarks>
        /// <returns>boolean value representing whether the DB is > 5 minutes old or not</returns>
        public bool isNew()
        {
            bool isDBNew = true;

            try
            {
                DateTime createDate = DateTime.Parse(GetSetting("dateCreated"));
                TimeSpan dbAge = DateTime.Now - createDate;

                //if db is older than 5 minutes
                if (dbAge > new TimeSpan(0, 5, 0))
                    isDBNew = false;
            }
            catch (Exception)
            {
                //More than likely this is on a parse error
                isDBNew = false;
            }

            return isDBNew;
        }

        /// <summary>
        /// Constructs the WHERE part of a SQL statement from a dictionary of column checks
        /// </summary>
        /// <param name="columnChecks">Dictionary of columnnames and what to check for</param>
        /// <returns>A SQL-formatted string for the WHERE part of a SQL statement</returns>
        internal string buildWhereClause(Dictionary<string, string> columnChecks)
        {
            List<string> columnList = new List<string>();

            //Build Where Clause
            foreach (KeyValuePair<String, String> row in columnChecks)
            {
                columnList.Add(string.Format("{0} == '{1}'", row.Key, row.Value));
            }

            return string.Join(" AND ", columnList.ToArray());
        }

        #region GET functions

        /// <summary>Gets the setting from the master table
        /// </summary>
        /// <param name="settingName">Key for the table</param>
        /// <returns>string in the setting column, or String.Empty if nothing found</returns>
        internal string GetSetting(string settingName)
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
        internal string[] GetRow(string tableName, string column, string value)
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
        internal string[] GetRow(string tableName, Dictionary<string, string> columnChecks)
        {
            //first part of select statement
            string selectStatement = string.Format("SELECT * from {0} where ", tableName);

            //Append the where criteria to the select statement
            selectStatement += buildWhereClause(columnChecks);

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

        /// <summary>Basic Update Executer, executes an update sql command
        /// </summary>
        /// <remarks>This is only necessary because the main program is not accessing the sqlDatabase class directly.</remarks>
        /// <param name="table">table to update</param>
        /// <param name="setOperations">columns to set</param>
        /// <param name="whereClause">conditions for subset of rows to update</param>
        internal void UpdateTable(string table, Dictionary<string, string> setOperations, string whereClause)
        {
            database.Update(table, setOperations, whereClause);
        }

        //create new database
        private void Create(string folderPath)
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
                [letter] VARCHAR(2),
                [username] text,
                [date] VARCHAR(255),
                FOREIGN KEY (shareID) REFERENCES [shares](shareID),
                FOREIGN KEY (computerID) REFERENCES [computers](computerID),
                FOREIGN KEY (userID) REFERENCES [users](userID),
                PRIMARY KEY (shareID, computerID, userID))";

            //create db file & connect to it
            string dataSource = folderPath + "\\network-drive-utility.s3db";
            // SQLiteConnection.CreateFile(dataSource);
            string connectionstring = "Data Source=\\" + dataSource + ";";
            SQLiteConnection dbConnection = new SQLiteConnection(connectionstring);
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
            Setting creationDate = new Setting("dateCreated", DateTime.Now.ToString());
            if (!creationDate.exists)
                creationDate.addToDB();

            Setting dataDir = new Setting("dataDir", folderPath);
            if (!dataDir.exists)
                dataDir.addToDB();

            Setting logging = new Setting("logging", true.ToString());
            if (!logging.exists)
                logging.addToDB();

            //close connection
            dbConnection.Close();
        }


        internal sealed class SQLiteDatabase
        {
            String dbConnection;
            internal LogWriter Transactlogger = new LogWriter();
            private LogWriter ErrorLogger = new LogWriter();

            /// <summary>
            ///     Default Constructor for SQLiteDatabase Class.
            /// </summary>
            public SQLiteDatabase()
            {
                dbConnection = "Data Source=data.s3db";
            }

            /// <summary>
            ///     Single Param Constructor for specifying the DB file.
            /// </summary>
            /// <param name="inputFile">The File containing the DB</param>
            internal SQLiteDatabase(String inputFile)
            {
                dbConnection = String.Format("Data Source={0}", inputFile);
            }

            /// <summary>
            ///     Single Param Constructor for specifying advanced connection options.
            /// </summary>
            /// <param name="connectionOpts">A dictionary containing all desired options and their values</param>
            internal SQLiteDatabase(Dictionary<String, String> connectionOpts)
            {
                String str = "";
                foreach (KeyValuePair<String, String> row in connectionOpts)
                {
                    str += String.Format("{0}={1}; ", row.Key, row.Value);
                }
                str = str.Trim().Substring(0, str.Length - 1);
                dbConnection = str;
            }

            /// <summary>
            ///     Allows the programmer to run a query against the Database.
            /// </summary>
            /// <param name="sql">The SQL to run</param>
            /// <returns>A DataTable containing the result set.</returns>
            internal DataTable GetDataTable(string sql)
            {
                DataTable dt = new DataTable();
                try
                {
                    //write sql command to log
                    Transactlogger.Write(sql);

                    SQLiteConnection cnn = new SQLiteConnection(dbConnection);
                    cnn.Open();
                    SQLiteCommand mycommand = new SQLiteCommand(cnn);
                    mycommand.CommandText = sql;
                    SQLiteDataReader reader = mycommand.ExecuteReader();
                    dt.Load(reader);
                    reader.Close();
                    cnn.Close();
                }
                catch (Exception e)
                {
                    ErrorLogger.Write(e.ToString());
                }

                if (dt.Rows.Count > 1)
                    Transactlogger.Write("Returned " + dt.Rows.Count + " Row(s)");
                return dt;
            }

            /// <summary>
            ///     Allows the programmer to interact with the database for purposes other than a query.
            /// </summary>
            /// <param name="sql">The SQL to be run.</param>
            /// <returns>An Integer containing the number of rows updated.</returns>
            private int ExecuteNonQuery(string sql)
            {
                SQLiteConnection cnn = new SQLiteConnection(dbConnection);
                cnn.Open();
                SQLiteCommand mycommand = new SQLiteCommand(cnn);
                mycommand.CommandText = sql;
                int rowsUpdated = mycommand.ExecuteNonQuery();
                cnn.Close();

                //write sql command to log
                Transactlogger.Write(sql);

                return rowsUpdated;
            }

            /// <summary>
            ///     Allows the programmer to retrieve single items from the DB.
            /// </summary>
            /// <param name="sql">The query to run.</param>
            /// <returns>A string.</returns>
            private string ExecuteScalar(string sql)
            {
                SQLiteConnection cnn = new SQLiteConnection(dbConnection);
                cnn.Open();
                SQLiteCommand mycommand = new SQLiteCommand(cnn);
                mycommand.CommandText = sql;
                object value = mycommand.ExecuteScalar();
                cnn.Close();

                //write sql command to log
                Transactlogger.Write(sql);

                if (value != null)
                {
                    return value.ToString();
                }
                return "";
            }

            /// <summary>
            ///     Allows the programmer to easily update rows in the DB.
            /// </summary>
            /// <param name="tableName">The table to update.</param>
            /// <param name="data">A dictionary containing Column names and their new values.</param>
            /// <param name="where">The where clause for the update statement.</param>
            /// <returns>A boolean true or false to signify success or failure.</returns>
            internal bool Update(String tableName, Dictionary<String, String> data, String where)
            {
                String vals = "";
                Boolean returnCode = true;
                if (data.Count >= 1)
                {
                    foreach (KeyValuePair<String, String> val in data)
                    {
                        vals += String.Format(" {0} = '{1}',", val.Key.ToString(), val.Value.ToString());
                    }
                    vals = vals.Substring(0, vals.Length - 1);
                }
                try
                {
                    this.ExecuteNonQuery(String.Format("update {0} set {1} where {2};", tableName, vals, where));
                }
                catch (Exception e)
                {
                    ErrorLogger.Write(e.ToString());
                    returnCode = false;
                }
                return returnCode;
            }

            /// <summary>
            ///     Allows the programmer to easily delete rows from the DB.
            /// </summary>
            /// <param name="tableName">The table from which to delete.</param>
            /// <param name="where">The where clause for the delete.</param>
            /// <returns>A boolean true or false to signify success or failure.</returns>
            internal bool Delete(String tableName, String where)
            {
                Boolean returnCode = true;
                try
                {
                    this.ExecuteNonQuery(String.Format("delete from {0} where {1};", tableName, where));
                }
                catch (Exception fail)
                {
                    ErrorLogger.Write(fail.Message);
                    returnCode = false;
                }
                return returnCode;
            }

            /// <summary>
            ///     Allows the programmer to easily insert into the DB
            /// </summary>
            /// <param name="tableName">The table into which we insert the data.</param>
            /// <param name="data">A dictionary containing the column names and data for the insert.</param>
            /// <returns>A boolean true or false to signify success or failure.</returns>
            internal bool Insert(String tableName, Dictionary<String, String> data)
            {
                String columns = "";
                String values = "";
                Boolean returnCode = true;
                foreach (KeyValuePair<String, String> val in data)
                {
                    columns += String.Format(" {0},", val.Key.ToString());
                    values += String.Format(" '{0}',", val.Value);
                }
                columns = columns.Substring(0, columns.Length - 1);
                values = values.Substring(0, values.Length - 1);
                try
                {
                    this.ExecuteNonQuery(String.Format("insert into {0}({1}) values({2});", tableName, columns, values));
                }
                catch (Exception fail)
                {
                    ErrorLogger.Write(fail.Message);
                    returnCode = false;
                }
                return returnCode;
            }

            /// <summary>
            ///     Allows the programmer to easily delete all data from the DB.
            /// </summary>
            /// <returns>A boolean true or false to signify success or failure.</returns>
            private bool ClearDB()
            {
                DataTable tables;
                try
                {
                    tables = this.GetDataTable("select NAME from SQLITE_MASTER where type='table' order by NAME;");
                    foreach (DataRow table in tables.Rows)
                    {
                        this.ClearTable(table["NAME"].ToString());
                    }
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            /// <summary>
            ///     Allows the user to easily clear all data from a specific table.
            /// </summary>
            /// <param name="table">The name of the table to clear.</param>
            /// <returns>A boolean true or false to signify success or failure.</returns>
            private bool ClearTable(String table)
            {
                try
                {
                    this.ExecuteNonQuery(String.Format("delete from {0};", table));

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
