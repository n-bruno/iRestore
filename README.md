# iRestore

This program recovers files from the FAT filesystem.

Limitations
-----------

This utility was created in two days, and it shows:

1. The program will not recover files from a fragmented disk.
2. The program has not been tested on FAT12 and FAT32 disk images.
3. You can not load mounted disk images.
4. Only the data on the DIMG file is changed. In order to get the recovered files, extract files with 7zip.
5. Hardly any bug testing has been done. The program may corrupt your image.

Do not use this utlity for serious file recovering. Backup your files.

How to use
----------
1. File > Load and select a DIMG file to load an image file
2. Press the Recover
3. Press save.

Notes
-----

1. Please report any bugs.
2. This program is distributed under the GPL-3.0 license. Please understand the terms of use before using the source code.
3. The utility should run on Windows, Linux, and Mac. For Linux and Mac, please use Mono to run the exe.
