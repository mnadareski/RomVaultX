using System.Collections.Generic;
using System.Data.Common;

namespace RomVaultX.DB.NewDB
{
    interface iDB
    {

        void ConnectToDB();

        void Begin();
        void Commit();


        void UpdateGotTotal();

        void MakeIndex();
        void DropIndex();


        uint RvDatWrite(RvDat dat);

        void RvDatRead(uint datId, RvDat dat);


        uint RvGameWrite(RvGame game);

        void RvGameRead(int gameId, RvGame game);
        List<RvGame> RvGamesRead(uint DatId);

        void RvRomWrite(RvRom rom);
        List<RvRom> RvRomsRead(uint gameId);

        uint RvFileWrite(RvFile file);
        void RvFileUpdateRom(uint fileId, RvFile file);
        void RvFileUpdateRomAlt(uint fileId, RvFile file);
        void RvFileUpdateZeroRom(uint fileId, RvFile file);

        List<RvGameGridRow> ReadGames(int datId);

        void ClearFoundDATs();
        void RemoveNotFoundDATs();
        int DatDBCount();



        bool SetUpFindAFile();
        uint? FindAFile(RvRom tFile);

        uint? FindDat(string fulldir, string filename, long DatTimeStamp);

        void SetDatFound(uint datId);

        uint FindOrInsertIntoDir(uint parentDirId, string name, string fullName);

        bool FindInFiles(RvFile tFile);

        bool FindInROMs(RvFile tFile);

        bool FindInROMsAlt(RvFile tFile);

        byte[] GetFile(uint fileId);

        DbDataReader CommandReadTreeGetReader();

        void SetTreeExpanded(uint DirId, bool expanded);

        int? GetFirstExpanded(uint DirId);

        void UpdateSelectedFromList(List<uint> todo, int value);
        List<uint> UpdateSelectedGetChildList(List<uint> todo);
    }
}
