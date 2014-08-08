using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace network_drive_utility
{
    /// <summary>This class is used to store utility methods that have no direct link to this application.
    /// </summary>
    /// <remarks>Includes: Error-Logging methods, Alphabet enumeration</remarks>
    /// <remarks>Excludes: Anything that could not be dropped into another application without modification</remarks>
    class Utilities
    {
        const string TIMESTAMP_FORMAT = "MM/dd HH:mm:ss ffff";

        /// <summary>Returns a timestamp in string format.
        /// </summary>
        /// <remarks>Uses the Timestamp Format defined in the Constants of this class.</remarks>
        /// <param name="value">DateTime to change into string</param>
        /// <returns>Timestamp in string format.</returns>
        private static string getTimestamp(DateTime value)
        {
            return value.ToString(TIMESTAMP_FORMAT);
        }

        /// <summary>Appends a new message to the end of the log file.
        /// </summary>
        /// <remarks>If there is no log file available, this will create one. The log files are stored in the current user's Appdata/Roaming folder.</remarks>
        /// <param name="message"></param>
        public static void writeLog(string message)
        {
            string logLocation = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + System.AppDomain.CurrentDomain.FriendlyName;

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
                sWriter.WriteLine(getTimestamp(DateTime.Now) + " " + message);

                sWriter.Close();
            }
        }

        /// <summary>Matches two strings, ignoring case.
        /// </summary>
        /// <param name="str1">String to compare</param>
        /// <param name="str2">String to compare</param>
        /// <returns>Boolean value of whether strings match or not.</returns>
        public static bool matchString_IgnoreCase(string str1, string str2)
        {
            bool isMatch = false;
            if (str1.Equals(str2, StringComparison.OrdinalIgnoreCase))
            {
                isMatch = true;
            }
            return isMatch;
        }

        /// <summary>Generates a List of all 26 Uppercase letters in the alphabet.
        /// </summary>
        /// <returns>char List of all letters in the alphabet (Uppercase)</returns>
        public static List<char> getAlphabetUppercase()
        {
            // Allocate space for alphabet
            List<char> alphabet = new List<char>(26);

            // increment from ASCII values for A-Z
            for (int i = 65; i < 91; i++)
            {
                // Add uppercase letters to possible drive letters
                alphabet.Add(Convert.ToChar(i));
            }
            return alphabet;
        }

        /// <summary>Parses the first single letterfrom a string
        /// </summary>
        /// <param name="str">string to parse</param>
        /// <returns>The first alphabetical character in the string</returns>
        public static char parseSingleLetter(string str)
        {
            Regex singleLetter = new Regex("^([A-z])");

            //validate driveLetter string parameter
            if (singleLetter.IsMatch(str))
            {
                //get first character only
                str = singleLetter.Match(str).ToString();
            }
            else
            {
                throw new ArgumentException("Error: No letters found in this string.");
            }

            return Convert.ToChar(str);
        }

        /// <summary>Reads an entire file.
        /// </summary>
        /// <param name="fullPath">Full UNC Path of the file</param>
        /// <returns>string of the entire file</returns>
        public static string readFile(string fullPath)
        {
            string str;
            //Read json from file on network
            StreamReader file = new StreamReader(fullPath);
            str = file.ReadToEnd();
            file.Close();
            return str;
        }

        /// <summary>Generic List Deserializer
        /// </summary>
        /// <typeparam name="T">Any type of serializable object stored in a list</typeparam>
        /// <param name="xmlString">string of XML objects to deserialize</param>
        /// <returns>List of Objects deserialized from the string</returns>
        public static List<T> Deserialize<T>(string xmlString)
        {
            List<T> result;

            XmlSerializer deserializer = new XmlSerializer(typeof(T));
            using (TextReader textReader = new StringReader(xmlString))
            {
                result = (List<T>)deserializer.Deserialize(textReader);
            }

            return result;
        }

        /// <summary>Serializes a list of objects and writes the serialized list to a XML file specified
        /// </summary>
        /// <typeparam name="T">Any type of serializable object stored in a list</typeparam>
        /// <param name="objList">List of serializable objects</param>
        /// <param name="filePath">Full Path of XML file to write to</param>
        public static void SerializeToFile<T>(List<T> objList, string filePath)
        {
            XmlSerializer serializer = new XmlSerializer(objList.GetType());
            using (StreamWriter sw = new StreamWriter(filePath))
            {
                serializer.Serialize(sw, objList);
            }
        }
    }
}
