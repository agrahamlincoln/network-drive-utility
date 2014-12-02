using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace network_drive_utility
{
    sealed class LogWriter
    {
        // Class Variables
        public string logPath { get; set; }
        public string fileName { get; set; }
        public string assemblyVersion { get; set; }

        #region Constructors
        public LogWriter()
        {
            this.logPath = getAppDataPath();
            this.fileName = getProcessName() + "_log.txt";
            this.assemblyVersion = getVersion();
        }

        internal LogWriter(string filepath)
        {
            this.logPath = filepath;
            this.fileName = getProcessName() + "_log.txt";
            this.assemblyVersion = getVersion();
        }

        internal LogWriter(string filepath, string fileName)
        {
            this.logPath = filepath;
            this.fileName = fileName;
            this.assemblyVersion = getVersion();
        }
        #endregion
        #region Information Gathering
        /// <summary>Gets the file path in the current user's AppData/Roaming folder. The filename will be the current process name by default.
        /// </summary>
        /// <returns>string file path in user's AppData/Roaming folder</returns>
        private static string getAppDataPath()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return path;
        }

        /// <summary>Gets the current process name
        /// </summary>
        /// <returns>Process Name for this application</returns>
        private static string getProcessName()
        {
            string process = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            return process;
        }

        /// <summary>Returns the currently runnning program's version number.
        /// </summary>
        /// <returns>String version number of currently running program.</returns>
        private string getVersion()
        {
            return typeof(LogWriter).Assembly.GetName().Version.ToString();
        }

        #endregion

        #region Log writing
        const string TIMESTAMP_FORMAT = "MM/dd HH:mm:ss";

        /// <summary>Returns a timestamp in string format.
        /// </summary>
        /// <remarks>Uses the Timestamp Format defined in the Constants of this class.</remarks>
        /// <param name="value">DateTime to change into string</param>
        /// <returns>Timestamp in string format.</returns>
        private static string getTimestamp(DateTime value)
        {
            return value.ToString(TIMESTAMP_FORMAT);
        }

        /// <summary>Public method to access the writelog method
        /// </summary>
        /// <param name="message">Message to write to log</param>
        /// <param name="print">Whether to write or not</param>
        internal void Write(string message, bool print)
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
        internal void Write(string message)
        {
            oWrite(message);
        }

        /// <summary>Appends a new message to the end of the log file.
        /// </summary>
        /// <remarks>If there is no log file available, this will create one. The log files are stored in the current user's Appdata/Roaming folder.</remarks>
        /// <param name="message"></param>
        private void oWrite(string message)
        {
            string logLocation = logPath + "\\" + fileName;

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
        internal string header()
        {
            return "Now Running: " + System.Diagnostics.Process.GetCurrentProcess().ProcessName + " v" + assemblyVersion;
        }
        #endregion
    }
}
