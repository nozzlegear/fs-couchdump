#r @"packages/FAKE/tools/FakeLib.dll"
#r @"packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#r @"packages/Microsoft.FSharpLu.Json/lib/net452/Microsoft.FSharpLu.Json.dll"

open Fake
open System
open System.IO
open Microsoft.FSharpLu.Json.Compact

let now() = DateTime.UtcNow.ToString "yyyy-MM-dd.HH:mm:ss"

let log msg = printfn "[%s.UTC]: %s" (now()) msg

let dumpDir = "./dumps/" |> FullName

let script = "./vendor/couchdb-backup.sh" |> FullName

// From SAFE stack: https://github.com/SAFE-Stack/SAFE-BookStore/blob/master/build.fsx
let run' (timeout: TimeSpan) cmd args dir =
    let configureProcess (info: Diagnostics.ProcessStartInfo) =
        info.RedirectStandardOutput <- true
        info.FileName <- cmd
        if not (String.IsNullOrWhiteSpace dir) then
            info.WorkingDirectory <- dir
        info.Arguments <- args

    let success, _ = ExecProcessRedirected configureProcess timeout

    if not success then
        failwithf "Error while running '%s' with args: %s" cmd args

// From SAFE stack: https://github.com/SAFE-Stack/SAFE-BookStore/blob/master/build.fsx
let run = run' System.TimeSpan.MaxValue

// From SAFE stack: https://github.com/SAFE-Stack/SAFE-BookStore/blob/master/build.fsx
let platformTool tool winTool =
    if isWindows then failwith "This tool is not supported in Windows because executables cannot access bash.exe. You must run this script from WSL instead."

    let tool = if isUnix then tool else winTool

    tool
    |> ProcessHelper.tryFindFileOnPath
    |> function Some t -> t | _ -> failwithf "%s not found" tool

let listDatabases () = async {
    use client = new System.Net.WebClient ()
    let address = Uri "http://localhost:5984/_all_dbs"
    let! json = client.AsyncDownloadString address

    return
        deserialize<string list> json
        |> List.filter (fun db -> db.IndexOf "_" <> 0) // Filter out global databases which start with _
}

let outputDate = now()
let zipFilename = dumpDir </> sprintf "%s.zip" outputDate |> FullName
let outputFolder = dumpDir </> outputDate |> FullName
let bash = platformTool "bash" "bash.exe"
let tarsnap = platformTool "tarsnap" "tarsnap.exe"

let list () =
    let databases = listDatabases () |> Async.RunSynchronously

    if databases.IsEmpty then
        log "No databases found."
    else
        databases
        |> Seq.iter (sprintf "Found database %s" >> log)

let backup () =
    log "Running backup command."

    let databases =
        listDatabases ()
        |> Async.RunSynchronously

    if databases.IsEmpty then
        log "Found no databases to backup."
    else
        // Create the folder
        Directory.CreateDirectory outputFolder |> ignore

        // Backup each database
        databases
        |> Seq.iter(fun db ->
            let filename = outputFolder </> db + ".json" |> FullName

            sprintf "Backing up %s to %s" db filename
            |> log

            // Overwrite the file if it already exists
            if File.Exists filename then File.Delete filename

            // -b runs the script in backup mode.
            // -r runs it in restore mode.
            // -q runs it in quiet mode except for errors and warnings but seems to break the script when run from F#
            let args = sprintf "%s -b -H 127.0.0.1 -d \"%s\" -f \"%s\"" script db filename

            run bash args __SOURCE_DIRECTORY__
        )

        // Overwrite the zip file if it already exists
        if File.Exists zipFilename then File.Delete zipFilename

        log "Zipping folder."

        // Zip up the files
        !! (sprintf "%s/*.json" outputFolder)
        |> Zip outputFolder zipFilename

    log "Backup command finished."

let upload () =
    backup()
    log "Running upload commmand."

    let backupName = sprintf "%s-%s" System.Environment.MachineName outputDate

    sprintf "Uploading folder %s as archive %s." outputFolder backupName
    |> log

    //  Tarsnap uses diffing to cache files that have already been uploaded, so upload size won't matter.
    let args = sprintf "-c -f %s %s" backupName outputFolder

    // Run tarsnap
    run tarsnap args __SOURCE_DIRECTORY__
    log "Upload command finished."

let showHelp() =
    printfn "Usage: run.fsx command"
    printfn ""
    printfn "Commands:"
    printfn "   list -> Lists all non-system databases in your CouchDB instance."
    printfn ""
    printfn "   backup -> Run a backup on all non-system databases (the ones listed by the list command), dumping them to a datestamped folder in the dumps directory. Once dumped this command will also zip the folder."
    printfn ""
    printfn "   upload -> Runs the backup command, then uploads everything in the dumps directory to Tarsnap. Use the tarsnap config file at (typically at /etc/tarsnap.conf) to configure which keyfile will be used."
    printfn ""
    printfn "Nozzlegear Software, %i" DateTime.UtcNow.Year


// Entrypoint cannot be used in an F# script, only compiled applications
let main (args: string list) =
    let command =
        match args with
        | "upload"::_ -> upload
        | "backup"::_ -> backup
        | "list"::_ -> list
        | "help"::_ | "-h"::_ | "--help"::_ | _ -> showHelp

    // Execute the requested command
    command()

    10

System.Environment.GetCommandLineArgs()
|> List.ofSeq
|> List.skip 1 // First arg is the name of the script that was run, e.g. "run.fsx" or "fsi.exe"
|> main