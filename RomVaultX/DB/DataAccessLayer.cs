/*



File Insert
-----------
RomVault will handle a file Insert, updating the FileId in ROM table.
(This will trigger the RomUpdate tigger.)

File Update
-----------
Assumtion is made that a file record will never be updated. (Just Inserts and Deletes permitted.)

File Delete
-----------
File Delete Trigger is in place that will null the FileId in ROM table when a file is deleted.
(This will trigger the RomUpdate tigger.)
 
 
 
Rom Insert
----------
RomInsert trigger will update the GAME table:
		RomTotal = RomTotal + 1,
        RomGot = RomGot + (IFNULL(New.FileId,0)>0),
        RomNoDump = RomNoDump + (IFNULL(New.status ='nodump' and New.crc is null and New.sha1 is null and New.md5 is null,0))
		

Rom Delete
----------
RomDelete trigger will update the GAME table:
		RomTotal = RomTotal - 1,
        RomGot = RomGot - (IFNULL(New.FileId,0)>0),
        RomNoDump = RomNoDump - (IFNULL(New.status ='nodump' and New.crc is null and New.sha1 is null and New.md5 is null,0))
		
Rom Update
----------
RomUpdate tigger assumes the only change to ROM table will be the FileId field.
		RomGot = RomGot - (IFNULL(Old.FileId,0)>0) + (IFNULL(New.FileId,0)>0)
		
		
Game Insert
-----------
GameInsert trigger will update the DAT table:
		RomTotal   =RomTotal  + New.RomTotal  , 
		RomGot     =RomGot    + New.RomGot    ,
		RomNoDump  =RomNoDump + New.RomNoDump
		  
Game Delete
-----------
GameDelete trigger will update the DAT table:
		RomTotal   =RomTotal  - New.RomTotal  , 
		RomGot     =RomGot    - New.RomGot    ,
		RomNoDump  =RomNoDump - New.RomNoDump

Game Update
-----------
GameUpdate trigger will update the DAT table:
		RomTotal   =RomTotal  - Old.RomTotal  + New.RomTotal ,
		RomGot     =RomGot    - Old.RomGot    + New.RomGot ,
		RomNoDump  =RomNoDump - Old.RomNoDump + New.RomNoDump


*/
