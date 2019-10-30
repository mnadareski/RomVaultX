using System;
using System.ComponentModel;
using System.IO;
using System.Xml;
using RomVaultX.DB;
using FileStream = RVIO.FileStream;

namespace RomVaultX.DatReader
{
    internal static class DatReader
    {
        private static BackgroundWorker _bgw;

        public static bool ReadDat(string fullname, BackgroundWorker bgw, out RvDat rvDat)
        {
            _bgw = bgw;

            rvDat = null;

            Console.WriteLine("Reading " + fullname);

            int errorCode = FileStream.OpenFileRead(fullname, out Stream fs);
            if (errorCode != 0)
            {
                _bgw.ReportProgress(0, new bgwShowError(fullname, errorCode + ": " + new Win32Exception(errorCode).Message));
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
            {
                return ReadXMLDat(fullname, out rvDat);
            }

            // ClrMamePro DATs
            else if (strLine.IndexOf("clrmamepro", StringComparison.OrdinalIgnoreCase) >= 0
                || strLine.IndexOf("romvault", StringComparison.OrdinalIgnoreCase) >= 0
                || strLine.IndexOf("game", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return DatCmpReader.ReadDat(fullname, out rvDat);
            }

            // DOSCenter DATs
            else if (strLine.IndexOf("doscenter", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return DatDOSReader.ReadDat(fullname, out rvDat);
            }

            // RomCenter DATs
            else if (strLine.IndexOf("[", StringComparison.OrdinalIgnoreCase) >= 0
                && strLine.IndexOf("]", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return DatRcReader.ReadDat(fullname, out rvDat);
            }

            // Unknown file / DAT type
            else
            {
                _bgw.ReportProgress(0, new bgwShowError(fullname, "Invalid DAT File"));
                return false;
            }
        }


        private static bool ReadXMLDat(string fullname, out RvDat rvDat)
        {
            rvDat = null;
            int errorCode = FileStream.OpenFileRead(fullname, out Stream fs);
            if (errorCode != 0)
            {
                _bgw.ReportProgress(0, new bgwShowError(fullname, errorCode + ": " + new Win32Exception(errorCode).Message));
                return false;
            }

            XmlDocument doc = new XmlDocument {XmlResolver = null};
            try
            {
                doc.Load(fs);
            }
            catch (Exception e)
            {
                fs.Close();
                fs.Dispose();
                _bgw.ReportProgress(0, new bgwShowError(fullname, string.Format("Error Occured Reading Dat:\r\n{0}\r\n", e.Message)));
                return false;
            }
            fs.Close();
            fs.Dispose();

            if (doc.DocumentElement == null)
            {
                return false;
            }

            XmlNode mame = doc.SelectSingleNode("mame");
            if (mame != null)
            {
                return DatXmlReader.ReadMameDat(doc, fullname, out rvDat);
            }

            XmlNode head = doc.DocumentElement?.SelectSingleNode("header");
            if (head != null)
            {
                return DatXmlReader.ReadDat(doc, fullname, out rvDat);
            }

            XmlNodeList headList = doc.SelectNodes("softwarelist");
            if (headList != null)
            {
                return DatMessXmlReader.ReadDat(doc, fullname, out rvDat);
            }

            return false;
        }
    }
}