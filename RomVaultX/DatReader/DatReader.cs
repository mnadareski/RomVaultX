using System;
using System.ComponentModel;
using System.IO;
using System.Xml;

namespace RomVaultX.DatReader
{
    static class DatReader
    {

        private static BackgroundWorker _bgw;
        public static void ReadDat(int DirId, string fullname, long fileTimeStamp,BackgroundWorker bgw)
        {
            _bgw = bgw;

            Console.WriteLine("Reading " + fullname);

            Stream fs;
            int errorCode = IO.FileStream.OpenFileRead(fullname, out fs);
            if (errorCode != 0)
            {
                _bgw.ReportProgress(0, new bgwShowError(fullname, errorCode + ": " + new Win32Exception(errorCode).Message));
                return;
            }



            StreamReader myfile = new StreamReader(fs, Program.Enc);
            string strLine = myfile.ReadLine();
            myfile.Close();
            fs.Close();
            fs.Dispose();

            if (strLine == null)
                return;

            if (strLine.ToLower().IndexOf("xml", StringComparison.Ordinal) >= 0)
            {
                if (!ReadXMLDat(DirId,fullname,fileTimeStamp))
                    return;
            }

            else if (strLine.ToLower().IndexOf("clrmamepro", StringComparison.Ordinal) >= 0 || strLine.ToLower().IndexOf("romvault", StringComparison.Ordinal) >= 0 || strLine.ToLower().IndexOf("game", StringComparison.Ordinal) >= 0)
            {
                if (!DatCmpReader.ReadDat(DirId, fullname, fileTimeStamp))
                    return;
            }
            else if (strLine.ToLower().IndexOf("doscenter", StringComparison.Ordinal) >= 0)
            {
            //    if (!DatDOSReader.ReadDat(datFullName))
            //        return;
            }
            else
            {
                _bgw.ReportProgress(0, new bgwShowError(fullname, "Invalid DAT File"));
                return;
            }
        }




        private static bool ReadXMLDat(int DirId,string fullname,long fileTimeStamp)
        {
            Stream fs;
            int errorCode = IO.FileStream.OpenFileRead(fullname, out fs);
            if (errorCode != 0)
            {
                _bgw.ReportProgress(0, new bgwShowError(fullname, errorCode + ": " + new Win32Exception(errorCode).Message));
                return false;
            }

            XmlDocument doc = new XmlDocument { XmlResolver = null };
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
                return false;

            XmlNode mame = doc.SelectSingleNode("mame");
            if (mame != null)
                return DatXmlReader.ReadMameDat(doc,DirId,fullname,fileTimeStamp);

            if (doc.DocumentElement != null)
            {
                XmlNode head = doc.DocumentElement.SelectSingleNode("header");
                if (head != null)
                    return DatXmlReader.ReadDat(doc,DirId,fullname,fileTimeStamp);
            }

            XmlNodeList headList = doc.SelectNodes("softwarelist");
            //if (headList != null)
            //    return DatMessXmlReader.ReadDat(doc);

            return false;
        }

    }
}
