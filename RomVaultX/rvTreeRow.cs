using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.Runtime.InteropServices;
using RomVaultX.DB;

namespace RomVaultX
{
    public class RvTreeRow
    {
        public uint DirId;
        public string dirName;
        public string dirFullName;
        public bool Expanded;

        public uint? DatId;
        public string datName;
        public string description;

        public int RomTotal;
        public int RomGot;
        public int RomNoDump;

        public string TreeBranches;

        public bool MultiDatDir;

        public Rectangle RTree;
        public Rectangle RExpand;
        public Rectangle RIcon;
        public Rectangle RText;

        private static readonly SQLiteCommand CmdReadTree;
        private static readonly SQLiteCommand CmdSetTreeExpanded;

        static RvTreeRow()
        {
            CmdReadTree = new SQLiteCommand(
         @"
                    SELECT 
                        dir.DirId as DirId,
                        dir.name as dirname,
                        dir.fullname,
                        dir.expanded,
                        dir.RomTotal as dirRomTotal,
                        dir.RomGot as dirRomGot,
                        dir.RomNoDump as dirNoDump,
                        dat.DatId,
                        dat.name as datname,
                        dat.description,
                        dat.RomTotal,
                        dat.RomGot,
                        dat.RomNoDump
                    FROM dir LEFT JOIN dat ON dir.DirId=dat.DirId
                    ORDER BY dir.Fullname,dat.Filename", DataAccessLayer.DBConnection);

            CmdSetTreeExpanded = new SQLiteCommand(
            @"
                    UPDATE dir SET expanded=@expanded WHERE DirId=@dirId", DataAccessLayer.DBConnection);
            CmdSetTreeExpanded.Parameters.Add(new SQLiteParameter("expanded"));
            CmdSetTreeExpanded.Parameters.Add(new SQLiteParameter("dirId"));
        }
        public static List<RvTreeRow> ReadTreeFromDB()
        {
            List<RvTreeRow> rows = new List<RvTreeRow>();

            using (SQLiteDataReader dr = CmdReadTree.ExecuteReader())
            {
                bool multiDatDirFound = false;

                string skipUntil = "";

                RvTreeRow lastTree = null;
                while (dr.Read())
                {
                    // a single DAT in a directory is just displayed in the tree at the same level as the directory
                    RvTreeRow pTree = new RvTreeRow
                    {
                        DirId = Convert.ToUInt32(dr["DirId"]),
                        dirName = dr["dirname"].ToString(),
                        dirFullName = dr["fullname"].ToString(),
                        Expanded = Convert.ToBoolean(dr["expanded"]),
                        DatId = dr["DatId"] == DBNull.Value ? null : (uint?)Convert.ToUInt32(dr["DatId"]),
                        datName = dr["datname"] == DBNull.Value ? null : dr["datname"].ToString(),
                        description = dr["description"] == DBNull.Value ? null : dr["description"].ToString(),
                        RomTotal = dr["RomTotal"] == DBNull.Value ? Convert.ToInt32(dr["dirRomTotal"]) : Convert.ToInt32(dr["RomTotal"]),
                        RomGot = dr["RomGot"] == DBNull.Value ? Convert.ToInt32(dr["dirRomGot"]) : Convert.ToInt32(dr["RomGot"]),
                        RomNoDump = dr["RomNoDump"] == DBNull.Value ? Convert.ToInt32(dr["dirNoDump"]) : Convert.ToInt32(dr["RomNoDump"]),
                    };

                    if (!string.IsNullOrEmpty(skipUntil))
                    {
                        if (pTree.dirFullName.Length >= skipUntil.Length)
                        {
                            if (pTree.dirFullName.Substring(0, skipUntil.Length) == skipUntil)
                                continue;
                        }
                    }
                    if (!pTree.Expanded)
                    {
                        skipUntil = pTree.dirFullName;
                        pTree.DatId = null;
                        pTree.datName = null;
                        pTree.description = null;
                        pTree.RomTotal = Convert.ToInt32(dr["dirRomTotal"]);
                        pTree.RomGot = Convert.ToInt32(dr["dirRomGot"]);
                        pTree.RomNoDump = Convert.ToInt32(dr["dirNoDump"]);
                    }
                    rows.Add(pTree);

                    if (lastTree != null)
                    {
                        // if multiple DAT's are in the same directory then we should add another level in the tree to display the directory
                        bool thisMultiDatDirFound = (lastTree.DirId == pTree.DirId);
                        if (thisMultiDatDirFound && !multiDatDirFound)
                        {
                            // found a new multidat
                            RvTreeRow dirTree = new RvTreeRow
                            {
                                DirId = lastTree.DirId,
                                dirName = lastTree.dirName,
                                dirFullName = lastTree.dirFullName,
                                Expanded = lastTree.Expanded,
                                DatId = null,
                                datName = null,
                                RomTotal = Convert.ToInt32(dr["dirRomTotal"]),
                                RomGot = Convert.ToInt32(dr["dirRomGot"]),
                                RomNoDump = Convert.ToInt32(dr["dirNoDump"])
                            };
                            rows.Insert(rows.Count - 2, dirTree);
                            lastTree.MultiDatDir = true;
                        }
                        if (thisMultiDatDirFound)
                            pTree.MultiDatDir = true;

                        multiDatDirFound = thisMultiDatDirFound;
                    }


                    lastTree = pTree;
                }
            }

            return rows;
        }
        public static void SetTreeExpanded(uint DirId, bool expanded)
        {
            CmdSetTreeExpanded.Parameters["dirId"].Value = DirId;
            CmdSetTreeExpanded.Parameters["expanded"].Value = expanded;
            CmdSetTreeExpanded.ExecuteNonQuery();
        }
        public static void SetTreeExpandedChildren(uint DirId)
        {
            SQLiteCommand getStatus = new SQLiteCommand(@"SELECT expanded FROM dir WHERE ParentDirId=@DirId ORDER BY fullname LIMIT 1",DataAccessLayer.DBConnection);
            getStatus.Parameters.Add(new SQLiteParameter("DirId"));
            getStatus.Parameters["DirId"].Value = DirId;

            Object res = getStatus.ExecuteScalar();

            int value;
            if (res != null && res != DBNull.Value)
                value = 1 - Convert.ToInt32(res);
            else
                return;

            List<uint> todo = new List<uint>();
            todo.Add(DirId);

            while (todo.Count > 0)
            {
                string inJoin = string.Join(",", todo);
                todo.Clear();
                SQLiteCommand SetStatus = new SQLiteCommand(@"UPDATE dir SET expanded=" + value + " WHERE ParentDirId in (" + inJoin + ")", DataAccessLayer.DBConnection);
                SetStatus.ExecuteNonQuery();


                SQLiteCommand GetChild = new SQLiteCommand(@"select DirId from dir where ParentDirId in (" + inJoin + ")", DataAccessLayer.DBConnection);
                SQLiteDataReader dr = GetChild.ExecuteReader();
                while (dr.Read())
                {
                    uint id = Convert.ToUInt32(dr["DirId"]);
                    todo.Add(id);
                }
                dr.Close();
            }
        }
    }

}
