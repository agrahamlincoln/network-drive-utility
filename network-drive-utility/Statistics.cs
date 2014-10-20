using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace network_drive_utility
{
    /// <summary>The MetaData class stores information about the network-drive-utility program. Information may include:
    /// Number of Fileshares Found, Number of Fileshares Unmapped, 
    /// </summary>
    [XmlRoot("Statistics")]
    internal struct Statistics
    {
        [XmlElement("FilesharesFound")]
        public int FilesharesFound { get; set; }
        [XmlElement("FilesharesUnmapped")]
        public int FilesharesUnmapped { get; set; }
    }
}
