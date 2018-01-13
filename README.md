# fs-couchdump
An F# script that wraps [couchdb-backup.sh](https://github.com/danielebailo/couchdb-dump) to dump all databases in a CouchDB instance, whereas couchdb-backup.sh only dumps one database at a time. It does *not* currently supported authentication or URLs that aren't `localhost:5984`, but such support should be trivial and I'm more than happy to accept a pull request.

(**Windows users: This script must be run from WSL.**)

# Setup instructions

1. Install Mono: `sudo apt install mono-complete`
2. **Clone this repository to /etc/fs-couchdump**: `git clone https://github.com/nozzlegear/fs-couchdump /etc/fs-couchdmp`
3. Make `run.sh` executable: `sudo chmod u+x run.sh`
4. Make paket.bootstrapper.exe executable: `sudo chmod u+x .paket/paket.bootstrapper.exe`
5. Bootstrap paket: `mono .paket/paket.bootstrapper.exe`
6. If you're using the upload feature:
    1. [Follow these instructions to install tarsnap](https://www.tarsnap.com/pkg-deb.html).
    2. Copy your tarsnap key (preferably a write-only key) to the target machine.
    3. Make sure the tarsnap config file at `/etc/tarsnap.conf` is pointing to your keyfile.
    4. Symlink `cron/backup-couchdb` to the cron folder: `ln -s "$PWD/cron/backup-couchdb" "/etc/cron.d/backup-couchdb"`

If you're on Windows you must also enable the Windows Subsystem for Linux and run the script from bash -- it won't work from CMD or PowerShell.

# Usage

Call the `run.sh` script from bash. You can specify one of three commands: `list`, `backup` or `upload`. The default command is `list`. If you're running this on Windows through WSL, you *may* also need to use `sudo` if you get permission errors, e.g. `sudo ./run.sh command`.

## list (default command)

This command will list all databases in your CouchDB instance; i.e. it will list all databases that would be backed up by the `backup` command.

```
./run.sh list

...
Found the following databases to backup:
database_one
database_two
....
```

## backup

This command will dump all of the databases found in the `list` command to the folder `./dumps/yyyy-mm-dd`. Once done it will zip up all files in that folder to `./dumps/yyyy-mm-dd.zip`.

```
./run.sh backup

...
... INFO: Output file /mnt/c/Users/nozzlegear/source/fs-couchdump/dumps/2017-10-22/database_one.json
  % Total    % Received % Xferd  Average Speed   Time    Time
 Time  Current
                                 Dload  Upload   Total   Spent
 Left  Speed
  0     0    0     0    0     0      0      0 --:--:-- --:--:-- -100  3686    0  3686    0     0   158k      0 --:--:-- --:--:-- --:--:--  163k
... INFO: File may contain Windows carridge returns- converting...
... INFO: Completed successfully.
... INFO: Amending file to make it suitable for Import.
... INFO: Stage 1 - Document filtering
... INFO: Stage 2 - Duplicate curly brace removal
... INFO: Stage 3 - Header Correction
... INFO: Stage 4 - Final document line correction
... INFO: Export completed successfully. File available at: /mnt/c/Users/nozzlegear/source/fs-couchdump/dumps/2017-10-22/database_one.json
...

Creating Zipfile: /mnt/c/Users/nozzlegear/source/fs-couchdump/dumps/2017-10-22.zip (Level: 7)
Adding File database_one.json
Adding File database_two.json
Zip successfully created /mnt/c/Users/nozzlegear/source/fs-couchdump/dumps/2017-10-22.zip
```

## upload

The upload command will run the [backup](#backup) command and then upload the entire backup folder to [tarsnap](https://www.tarsnap.com). You need to add a tarsnap keyfile somewhere on your machine and make sure the tarsnap config file at `/etc/tarsnap.conf` points to the keyfile. The default location is `/root/tarsnap.key`.

For increased security, I recommend [using a write-only key](https://www.tarsnap.com/tips.html#write-only-keys).

# Running this script automatically on a schedule

This project comes with a small cron script to run the backup **and upload** process every day at 10:00 A.M. UTC. To activate the cron job, just copy the `cron/backup-couchdb` file to `/etc/cron`, or even easier, create a symlink so that file is automatically updated whenever you update this repo:

```sh
# Assuming you're in this repository's root folder
ln -s "$PWD/cron/backup-couchdb" "/etc/cron/backup-couchdb"
```

**Make sure `/etc/tarsnap.conf` points to your tarnsap key!**

# Restoring databases

This script does not handle restoring databases, only backing them up. To restore a database you can use the `couchdb-backup.sh` script in the vendor folder:

```sh
# -r runs the script in restore mode
# 127.0.0.1 is your couchdb address
# -d is the name of the database
# -f is the name of the dumped json file
bash vendor/couchdb-backup.sh -r -H 127.0.0.1 -d my-db -f dumpedDB.json
```

Refer to the documentation for `couchdb-backup.sh` at [https://github.com/danielebailo/couchdb-dump](https://github.com/danielebailo/couchdb-dump).