using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace network_drive_utility
{
    class LogWriter
    {
        // Class Variables
        public string appDataPath { get; set; }
        public string assemblyVersion { get; set; }

        // Constructors
        public LogWriter()
        {
            this.appDataPath = getAppDataPath();
            this.assemblyVersion = getVersion();
        }

        #region Log writing
        const string TIMESTAMP_FORMAT = "MM/dd HH:mm:ss";

        /// <summary>Returns a timestamp in string format.
        /// </summary>
        /// <remarks>Uses the Timestamp Format defined in the Constants of this class.</remarks>
        /// <param name="value">DateTime to change into string</param>
        /// <returns>Timestamp in string format.</returns>
        private string getTimestamp(DateTime value)
        {
            return value.ToString(TIMESTAMP_FORMAT);
        }

        /// <summary>Public method to access the writelog method
        /// </summary>
        /// <param name="message">Message to write to log</param>
        /// <param name="print">Whether to write or not</param>
        public void Write(string message, bool print)
        {
            if (print)
            {
                oWrite(message);
            }
        }

        /// <summary>Public method to access the writelog method
        /// </summary>
        /// <param name="message">Message to write to log</param>
        /// <param name="print">Whether to write or not</param>
        public void Write(string message)
        {
            oWrite(message);
        }

        /// <summary>Appends a new message to the end of the log file.
        /// </summary>
        /// <remarks>If there is no log file available, this will create one. The log files are stored in the current user's Appdata/Roaming folder.</remarks>
        /// <param name="message"></param>
        private void oWrite(string message)
        {
            string logLocation = appDataPath + "_log.txt";

            if (!System.IO.File.Exists(logLocation))
            {
                using (StreamWriter sw = System.IO.File.CreateText(logLocation))
                {
                    sw.WriteLine(getTimestamp(DateTime.Now) + message);
                }
            }
            else
            {
                StreamWriter sWriter = new StreamWriter(logLocation, true);
                sWriter.WriteLine(getTimestamp(DateTime.Now) + "\t" + message);

                sWriter.Close();
            }
        }

        /// <summary> Creates a string header with program name and version. Typically used at the beginning of the program.
        /// </summary>
        /// <returns>string program header for log file.</returns>
        public string header()
        {
            return "Now Running: " + System.Diagnostics.Process.GetCurrentProcess().ProcessName + " v" + assemblyVersion;
        }

        /// <summary>Gets the file path in the current user's AppData/Roaming folder. The filename will be the current process name by default.
        /// </summary>
        /// <returns>string file path in user's AppData/Roaming folder</returns>
        public string getAppDataPath()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            return path;
        }

        /// <summary>Returns the currently runnning program's version number.
        /// </summary>
        /// <returns>String version number of currently running program.</returns>
        public string getVersion()
        {
            return typeof(LogWriter).Assembly.GetName().Version.ToString();
        }

        #endregion
    }
}
