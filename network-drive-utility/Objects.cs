using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace network_drive_utility
{
    /// <summary>
    /// Stores the tables and their data for this instance of the program
    /// Objects of these types will check the database for existence upon creation
    /// ID's will be stored in the objects that reference the ID in each table if found (-1 if not found)
    /// </summary>
    abstract class DBRow : DBOperator
    {
        internal string tblName;            // Name of Table in DB
        internal Dictionary<string, string> dataForDB;  // Dictionary of keys/pairs for DB
        internal Dictionary<string, string> uniqueData; // What key/pairs make this row unique
        internal bool exists = false;       // If the row exists in the DB: default is false

        internal bool checkIfExists()
        {
            bool exist = false;
            string[] rowData = getFromDB();
            if (rowData.Length >= 1)
                //data was returned
                exist = true;

            return exist;
        }

        /// <summary>
        /// Gets the object closest to this from the DB
        /// </summary>
        internal string[] getFromDB()
        {
            return base.GetRow(tblName, uniqueData);
        }

        /// <summary>
        /// Inserts the data into the table
        /// </summary>
        internal void addToDB()
        {
            base.database.Insert(tblName, dataForDB);

            //update object for newly created record
            updateID();
            exists = true;
        }

        /// <summary>
        /// Update Unique Data
        /// </summary>
        internal abstract void updateUniqueData();

        /// <summary>
        /// Update DB Data to be written
        /// </summary>
        internal abstract void updateDataForDB();

        /// <summary>
        /// Update ID from Database
        /// </summary>
        internal abstract void updateID();

        internal int getID()
        {
            int id = -1;

            if (exists)
                if (uniqueData != null)
                    id = Convert.ToInt32(getFromDB()[0]);

            return id;
        }

        /// <summary>
        /// Used in child objects to initialize objects and check against database.
        /// </summary>
        /// <remarks>DO NOT INVOKE THIS METHOD AS A PARENT</remarks>
        internal void construct()
        {
            updateUniqueData();
            exists = checkIfExists();
            updateDataForDB();
            updateID();
        }

    }
    class Setting : DBRow
    {
        internal int id;            // ID of setting from table
        internal string setting;    // Name of setting
        internal string value;      // Setting value

        #region constructors
        /// <summary>
        /// no-arg constructor
        /// </summary>
        internal Setting()
        {
            updateID();
            setting = "";
            value = "";
            updateDataForDB();
        }

        /// <summary>
        /// full-arg constructor
        /// </summary>
        /// <param name="setting">Name of the setting</param>
        /// <param name="value">value of the setting</param>
        internal Setting(string setting, string value)
        {
            this.setting = setting;
            this.value = value;

            construct();
        }
        #endregion

        internal override void updateUniqueData()
        {
            uniqueData = new Dictionary<string, string>();
            uniqueData.Add("setting", setting);
        }

        internal override void updateDataForDB()
        {
            base.dataForDB = new Dictionary<string, string>();
            base.dataForDB.Add("setting", setting);
            base.dataForDB.Add("value", value);
        }

        internal override void updateID()
        {
            id = getID();
        }
    }

    class User : DBRow
    {
        internal int userID;     // ID of user in DB
        internal string username;   // Username

        #region constructors
        /// <summary>
        /// no-arg constructors
        /// </summary>
        internal User()
        {
            updateID();
            this.username = "";
            updateDataForDB();
        }

        /// <summary>
        /// full-arg constructor
        /// </summary>
        /// <param name="username">username, typically Environmnent.username</param>
        internal User(string username)
        {
            this.username = username;

            construct();
        }
        #endregion

        internal override void updateUniqueData()
        {
            uniqueData = new Dictionary<string, string>();
            uniqueData.Add("username", username);
        }

        internal override void updateDataForDB()
        {
            base.dataForDB = new Dictionary<string, string>();
            base.dataForDB.Add("username", username);
        }

        internal override void updateID()
        {
            userID = getID();
        }
    }

    class Computer : DBRow
    {
        internal int computerID;    // ID of the computer in the DB
        internal string hostname;   // Hostname of the computer

        #region constructors
        /// <summary>
        /// no-arg constructor
        /// </summary>
        internal Computer()
        {
            updateID();
            this.hostname = "";
            updateDataForDB();
        }

        /// <summary>
        /// full-arg constructor
        /// </summary>
        /// <param name="hostname">hostname of the computer</param>
        internal Computer(string hostname)
        {
            this.hostname = hostname;

            construct();
        }
        #endregion

        internal override void updateUniqueData()
        {
            uniqueData = new Dictionary<string, string>();
            uniqueData.Add("hostname", hostname);
        }

        internal override void updateDataForDB()
        {
            base.dataForDB = new Dictionary<string, string>();
            base.dataForDB.Add("hostname", hostname);
        }

        internal override void updateID()
        {
            computerID = getID();
        }
    }

    class Server : DBRow
    {
        internal int serverID;      // ID of the server from the DB
        internal string hostname;   // Hostname of the server
        internal bool active;       // If shares should be listed as valid or ignored
        internal string domain;     // Qualified Domain name of the server
        internal DateTime date;     // Date that this server was last seen with a valid share

        #region constructors
        internal Server()
        {
            this.hostname = "";
            this.active = true;
            this.domain = "";
            this.date = DateTime.Now;
        }

        internal Server(string hostname, string domain)
        {
            this.hostname = hostname;
            this.domain = domain;

            construct();

            if (exists)
                this.active = Boolean.Parse(getFromDB()[2]);
            else
                this.active = true;
        }
        #endregion

        #region overrides
        internal override void updateUniqueData()
        {
            uniqueData = new Dictionary<string, string>();
            uniqueData.Add("hostname", hostname);
            uniqueData.Add("domain", domain);
        }

        internal override void updateDataForDB()
        {
            base.dataForDB = new Dictionary<string, string>();
            base.dataForDB.Add("hostname", hostname);
            base.dataForDB.Add("active", Convert.ToInt32(active).ToString());
            base.dataForDB.Add("domain", domain);
            base.dataForDB.Add("date", date.ToString());
        }

        internal override void updateID()
        {
            serverID = getID();
        }
        #endregion

        /// <summary>
        /// Verifies if the server will resolve through DNS
        /// </summary>
        /// <returns>True - the server resolves in DNS
        /// False - the server does not resolve in DNS</returns>
        public bool dnsVerify()
        {
            bool dnsResolves = false;

            try
            {
                this.domain = Utilities.GetDomainName(Utilities.GetFQDN(hostname));
                dnsResolves = true;
            }
            catch (Exception)
            {
                dnsResolves = false;
            }

            return dnsResolves;
        }
    }

    class Share : DBRow
    {
        internal int shareID;       // ID of share from DB
        internal Server server;   // Server that this share is on
        internal string sharename;  // Shared name of the share
        internal bool active;       // If share should be unmapped or stay valid

        #region constructors
        /// <summary>
        /// no-arg constructor
        /// </summary>
        internal Share()
        {
            updateID();
            this.server = new Server();
            this.sharename = "";
            this.active = true;
        }

        internal Share(string shareName, Server server)
        {
            this.server = server;
            this.sharename = shareName;

            construct();

            if (exists)
                this.active = Boolean.Parse(getFromDB()[3]);
            else
                this.active = true;
        }
        #endregion

        internal override void updateUniqueData()
        {
            uniqueData = new Dictionary<string, string>();
            uniqueData.Add("serverID", server.serverID.ToString());
            uniqueData.Add("shareName", sharename);
        }

        internal override void updateDataForDB()
        {
            base.dataForDB = new Dictionary<string, string>();
            base.dataForDB.Add("shareName", sharename);
        }

        internal override void updateID()
        {
            shareID = getID();
        }
    }

    class Mapping : DBRow
    {
        internal Share share;       // Share that is mapped
        internal Computer computer; //Computer the share is mapped on
        internal User user;         // User that has the share mapped
        internal string letter;     // drive letter of mapping
        internal string username;   //username listed under the mapping
        internal DateTime date;     // Date that this mapping was last seen

        #region constructors
        internal Mapping()
        {
            this.share = new Share();
            this.computer = new Computer();
            this.user = new User();
            this.letter = "";
            this.username = "";
            this.date = DateTime.Now;
        }

        internal Mapping(Share share, Computer computer, User user, string letter, string username)
        {
            this.share = share;
            this.computer = computer;
            this.user = user;
            this.letter = letter;
            this.username = username;

            construct();

            this.date = DateTime.Now;
        }
        #endregion

        internal override void updateUniqueData()
        {
            uniqueData = new Dictionary<string, string>();
            uniqueData.Add("shareID", share.shareID.ToString());
            uniqueData.Add("computerID", computer.computerID.ToString());
            uniqueData.Add("userID", user.userID.ToString());
            uniqueData.Add("username", username);
        }

        internal override void updateDataForDB()
        {
            dataForDB = new Dictionary<string, string>();
            dataForDB.Add("shareID", share.shareID.ToString());
            dataForDB.Add("computerID", computer.computerID.ToString());
            dataForDB.Add("userID", user.userID.ToString());
            dataForDB.Add("letter", letter);
            dataForDB.Add("username", username);
            dataForDB.Add("date", date.ToString());
        }

        internal override void updateID()
        {
            //do nothing since there is no mappingID
        }
    }
}
