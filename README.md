RomVaultX
=========

[![Build status](https://ci.appveyor.com/api/projects/status/gq1kh9sh60ta6ony/branch/master?svg=true)](https://ci.appveyor.com/project/mnadareski/romvaultx/branch/master)

Next generation rom collecting based on [RomVault](https://github.com/gjefferyes/RomVault) and [romba](https://github.com/uwedeportivo/romba/). Uses the intuitive interface from the former and the depot-style file storage from the latter.

## Features

**Datfile Support** - Currently supports ClrMamePro, Logiqx XML, MAME Listxml, and MAME Software List formats fully, with preliminary support for OfflineList and RomCenter (INI) formats.

**Archive Support** - Currently supports zipfiles for input fully, with preliminary support for 7z. All virtual archives created are in the [TorrentZip](http://www.romvault.com/trrntzip_explained.doc) format. Preliminary support for CHD files also exists.

**Configurable** - Depot locations, database filename, and virtual drive letter are all manually configurable in the .config file. If you want to add more depot locations (currently only fully supported for scanning, not rebuilding), please add them in the format (where X is a sequential number):

```
	<add key="DepotX" value="RomRoot" />
	<add key="SizeX" value="-1" />
```

## Requirements

RomVaultX is currently Windows-only (though there is the possibility that it can be cross platform). Here are a list of software requirements:

- Visual Studio 2017 redistributables
- [Dokan](https://dokan-dev.github.io/) - There is a FUSE wrapper for Dokan that may make it possible for this to be used on Macintosh and Linux systems, though this has not been tested as of yet
- Create required folders alongside the RomVaultX executable
	- DATRoot
	- ROMRoot
	- ToSort


## Recommendations

It is highly recommended that you put the database on an SSD, as there can be massive performance drops or even hard crashes (of both RomVaultX and your computer!) if the database gets too large.

## Known Issues

RomVaultX is by no means a release-grade product, despite our best efforts. Here is a list of known issues (some of which are referenced in the TODO):

- CHDs are inside of zipfiles because unzipped files are not supported on the virtual drive
- SuperDATs don't produce nested folders
- 7zip cannot open the virtual zips at all, either directly or from the file manager. WinRAR cannot open the files directly but can from the file manager. Teracopy has issues copying files from the virtual drive. However, built-in Windows extract, open archive, and copy all work as it should. Emulators are hit and miss with this, depending on their methods
- Virtual drive size is not shown properly. This is deliberate because the original code *did* show it properly, but caused a memory leak which could easily exceed 32GB of RAM used for a moderately big database


## Current TODO (branch only):

Due to the current status of the software, there are a lot of things to work on, so this list is non-exhaustive:

- **Full CHD support** - Currently, CHD files can be matched into the database and rebuilt to the depot, but upon a full depot rescan, they do not properly show up again in their respective DATs
- **Header support** - I personally do not feel the need to add this, but there are enough people who want this that it should be mentioned here
- **Full SuperDAT support** - Currently, SuperDATs are treated similar to how they are in ClrMamePro, where the folder structure is flattened out and the folder names are prepended to the set names with a special character. This is not ideal, as SuperDATs imply a folder structure to them.
- **Uncompressed virtual drive support** - This item either could mean a mode where RomVaultX stores all files as uncompressed in the depot and all of the virtual drive sets are uncompressed as well, OR just the second half of that where you can have "uncompressed" sets in your virtual drive
- **Better / Full Depot support** - romba has many different unique features that allow it to work very well with these large sets of data in the depots. Only the most basic storage mechanisms are mimicked by RomVaultX at this point. The features that would need additional implementation are:
	- Rebuild to a specific depot
	- Set individual depot file size limits
	- `purge-backup` and `purge-delete` support (for removing files no longer needed in the depots)
	- Depot merging (without rescan)
- **More configurable options** - There are only a few configurable options right now, and most of them have to be set by hand. On the list is allowing these configuration values to be set via the UI and to be changed in real-time. Otherwise, here are some additional options that need to be added:
	- Customizable locations for ToSort and DatRoot (possibly having multiple ToSort directories)