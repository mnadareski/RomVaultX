using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Drawing;
using Microsoft.Data.Sqlite;

namespace RomVaultX
{
    public class RvTreeRow
    {
        public static SqliteCommand CommandGetFirstExpanded
        {
            get
            {
                if (_commandGetFirstExpanded == null)
                {
                    _commandGetFirstExpanded = new SqliteCommand(@"
                        SELECT
                            expanded
                        FROM DIR
                        WHERE
                            ParentDirId = @DirId
                        ORDER BY fullname
                        LIMIT 1",
                    Program.db.Connection);

                    _commandGetFirstExpanded.Parameters.Add(new SqliteParameter("DirId", SqliteType.Integer));
                }

                return _commandGetFirstExpanded;
            }
        }
        private static SqliteCommand? _commandGetFirstExpanded;

        public static SqliteCommand CommandReadTree
        {
            get
            {
                if (_commandReadTree == null)
                {
                    _commandReadTree = new SqliteCommand(@"
                        SELECT 
                            DIR.DirId as DirId,
                            DIR.name as dirname,
                            DIR.fullname,
                            DIR.expanded,
                            DIR.RomTotal as dirRomTotal,
                            DIR.RomGot as dirRomGot,
                            DIR.RomNoDump as dirNoDump,
                            DAT.DatId,
                            DAT.name as datname,
                            DAT.description,
                            DAT.RomTotal,
                            DAT.RomGot,
                            DAT.RomNoDump
                        FROM DIR
                        LEFT JOIN DAT
                        ON
                            DIR.DirId = DAT.DirId
                        ORDER BY DIR.fullname, DAT.Filename",
                    Program.db.Connection);
                }

                return _commandReadTree;
            }
        }
        private static SqliteCommand? _commandReadTree;

        public uint DirId;
        public string? dirName;
        public string? dirFullName;
        public bool Expanded;

        public uint? DatId;
        public string? datName;
        public string? description;

        public int RomTotal;
        public int RomGot;
        public int RomNoDump;

        public string? TreeBranches;

        public bool MultiDatDir;

        public Rectangle RTree;
        public Rectangle RExpand;
        public Rectangle RIcon;
        public Rectangle RText;

        public static List<RvTreeRow> ReadTreeFromDB()
        {
            List<RvTreeRow> rows = [];
            using (DbDataReader dr = CommandReadTree.ExecuteReader())
            {
                bool multiDatDirFound = false;

                string skipUntil = "";

                RvTreeRow? lastTree = null;
                while (dr.Read())
                {
                    // a single DAT in a directory is just displayed in the tree at the same level as the directory
                    var pTree = new RvTreeRow
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
                        RomNoDump = dr["RomNoDump"] == DBNull.Value ? Convert.ToInt32(dr["dirNoDump"]) : Convert.ToInt32(dr["RomNoDump"])
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
                        bool thisMultiDatDirFound = lastTree.DirId == pTree.DirId;
                        if (thisMultiDatDirFound && !multiDatDirFound)
                        {
                            // found a new multidat
                            var dirTree = new RvTreeRow
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

        public static void SetTreeExpandedChildren(uint DirId)
        {
            int? value = GetFirstExpanded(DirId);
            if (value == null)
                return;

            value = 1 - value;

            List<uint> todo = [DirId];
            while (todo.Count > 0)
            {
                UpdateSelectedFromList(todo, (int)value);
                todo = UpdateSelectedGetChildList(todo);
            }
        }

        private static int? GetFirstExpanded(uint DirId)
        {
            CommandGetFirstExpanded.Parameters["DirId"].Value = DirId;

            var res = CommandGetFirstExpanded.ExecuteScalar();
            if (res == null || res == DBNull.Value)
                return null;

            return Convert.ToInt32(res);
        }

        private static void UpdateSelectedFromList(List<uint> todo, int value)
        {
            string todoList = string.Join(",", todo);
            using DbCommand SetStatus = new SqliteCommand(@"UPDATE DIR SET expanded = " + value + " WHERE ParentDirId in (" + todoList + ")", Program.db.Connection);
            SetStatus.ExecuteNonQuery();
        }

        private static List<uint> UpdateSelectedGetChildList(List<uint> todo)
        {
            string todoList = string.Join(",", todo);
            List<uint> retList = [];

            using DbCommand GetChild = new SqliteCommand(@"SELECT DirId FROM DIR WHERE ParentDirId in (" + todoList + ")", Program.db.Connection);
            using DbDataReader dr = GetChild.ExecuteReader();
            while (dr.Read())
            {
                uint id = Convert.ToUInt32(dr["DirId"]);
                retList.Add(id);
            }

            dr.Close();
            return retList;
        }
    }
}