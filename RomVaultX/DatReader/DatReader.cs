using System;
using System.ComponentModel;
using System.Xml;

using RomVaultX.DB;

using Alphaleonis.Win32.Filesystem;

using Stream = System.IO.Stream;
using StreamReader = System.IO.StreamReader;

namespace RomVaultX.DatReader
{
    // TODO: Implement other DAT formats
    static class DatReader
    {
        private static BackgroundWorker _bgw;

        /// <summary>
        /// Wrap reading a generic DAT file
        /// </summary>
        /// <param name="fullname">Full path to the input DAT</param>
        /// <param name="bgw">BackgroundWorker representing the thread to use</param>
        /// <param name="rvDat">Output RvDat created based on the input file</param>
        /// <returns>True if the file was sucessfully read, false otherwise</returns>
        public static bool ReadDat(string fullname, BackgroundWorker bgw, out RvDat rvDat)
        {
            // Set the internal background worker
            _bgw = bgw;

            // Create a null DAT for output to start
            rvDat = null;

            Console.WriteLine("Reading " + fullname);

            // Attempt to read the file and check for errors
            Stream fs;
            try
            {
                _bgw.ReportProgress(0, new bgwShowEvent(fullname, "Reading"));
                fs = File.OpenRead(fullname);
            }
            catch (Exception ex)
            {
                _bgw.ReportProgress(0, new bgwShowEvent(fullname, ex.Message));
                return false;
            }

            // If the file could be read, read the first line
            StreamReader myfile = new StreamReader(fs, Program.Enc);
            string strLine = myfile.ReadLine();
            myfile.Close();
            fs.Close();
            fs.Dispose();

            // If there's no first line, we don't have a readable file
            if (strLine == null)
                return false;

            // XML-based DATs
            if (strLine.IndexOf("xml", StringComparison.OrdinalIgnoreCase) >= 0)
                return ReadXMLDat(fullname, out rvDat);

            // ClrMamePro DATs
            else if (strLine.IndexOf("clrmamepro", StringComparison.OrdinalIgnoreCase) >= 0
                || strLine.IndexOf("romvault", StringComparison.OrdinalIgnoreCase) >= 0
                || strLine.IndexOf("game", StringComparison.OrdinalIgnoreCase) >= 0)
                return DatCmpReader.ReadDat(fullname, out rvDat);

            // DOSCenter DATs
            else if (strLine.IndexOf("doscenter", StringComparison.OrdinalIgnoreCase) >= 0)
                return DatDOSReader.ReadDat(fullname, out rvDat);

            // RomCenter DATs
            else if (strLine.IndexOf("[", StringComparison.OrdinalIgnoreCase) >= 0
                && strLine.IndexOf("]", StringComparison.OrdinalIgnoreCase) >= 0)
                return DatRcReader.ReadDat(fullname, out rvDat);

            // Unknown file / DAT type
            else
            {
                _bgw.ReportProgress(0, new bgwShowEvent(fullname, "Invalid DAT File"));
                return false;
            }
        }

        /// <summary>
        /// Internal method to read the correct type of XML dat
        /// </summary>
        /// <param name="fullname">Full path to the input DAT</param>
        /// <param name="rvDat">Output RvDat created based on the input file</param>
        /// <returns>True if the file was sucessfully read, false otherwise</returns>
        private static bool ReadXMLDat(string fullname, out RvDat rvDat)
        {
            // Create a null DAT for output to start
            rvDat = null;

            // Attempt to read the file and check for errors
            Stream fs;
            try
            {
                fs = File.OpenRead(fullname);
            }
            catch (Exception ex)
            {
                _bgw.ReportProgress(0, new bgwShowEvent(fullname, ex.Message));
                return false;
            }

            // If the file could be read, try to load it into an XmlDocument
            XmlDocument doc = new XmlDocument { XmlResolver = null };
            try
            {
                doc.Load(fs);
            }
            catch (Exception e)
            {
                fs.Close();
                fs.Dispose();
                _bgw.ReportProgress(0, new bgwShowEvent(fullname, string.Format("Error Occured Reading Dat:\r\n{0}\r\n", e.Message)));
                return false;
            }

            fs.Close();
            fs.Dispose();

            // If there's no document element, return false
            if (doc.DocumentElement == null)
                return false;

            // If there's a node called "mame", we assume it's a MAME DAT
            XmlNode mame = doc.SelectSingleNode("mame");
            if (mame != null)
                return DatXmlReader.ReadMameDat(doc, fullname, out rvDat);

            // If there's a node called "header", we assume it's a standard XML DAT
            XmlNode head = doc.DocumentElement?.SelectSingleNode("header");
            if (head != null)
                return DatXmlReader.ReadDat(doc, fullname, out rvDat);

            // If there's a node called "softwarelist", we assume it's a software list XML DAT
            XmlNodeList headList = doc.SelectNodes("softwarelist");
            if (headList != null)
                return DatMessXmlReader.ReadDat(doc, fullname, out rvDat);

            return false;
        }
    }
}
