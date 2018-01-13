#r @"packages/FAKE/tools/FakeLib.dll"
#r @"packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#r @"packages/Microsoft.FSharpLu.Json/lib/net452/Microsoft.FSharpLu.Json.dll"

open Fake
open System
open System.IO
open Microsoft.FSharpLu.Json.Compact
open System.Net

let dumpDir = "./dumps/" |> FullName

let script = "./vendor/couchdb-backup.sh" |> FullName

// From SAFE stack: https://github.com/SAFE-Stack/SAFE-BookStore/blob/master/build.fsx
let run' timeout cmd args dir =
    if execProcess (fun info ->
        info.FileName <- cmd
        if not (String.IsNullOrWhiteSpace dir) then
            info.WorkingDirectory <- dir
        info.Arguments <- args
    ) timeout |> not then
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

    return deserialize<string list> json
}

let date = System.DateTime.Now.ToString("yyyy-MM-dd")
let zipFilename = dumpDir </> sprintf "%s.zip" date |> FullName
let outputFolder = dumpDir </> date |> FullName
let bash = platformTool "bash" "bash.exe"

Target "Backup" (fun _ ->
    let databases = listDatabases () |> Async.RunSynchronously

    // Create the folder
    Directory.CreateDirectory outputFolder |> ignore

    // Backup each database
    databases |> Seq.iter(fun db ->
        let filename = outputFolder </> db + ".json" |> FullName

        // Overwrite the file if it already exists
        if File.Exists filename then File.Delete filename

        // -b runs the script in backup mode.
        // -r runs it in restore mode.
        // -q runs it in quiet mode except for errors and warnings
        let args = sprintf "%s -b -q -H 127.0.0.1 -d \"%s\" -f \"%s\"" script db filename

        run bash args __SOURCE_DIRECTORY__ )

    // Overwrite the zip file if it already exists
    if File.Exists zipFilename then File.Delete zipFilename

    // Zip up the files
    !! (sprintf "%s/*.json" outputFolder)
    |> Zip outputFolder zipFilename
)

Target "List" (fun _ ->
    let databases = listDatabases () |> Async.RunSynchronously

    printfn "Found the following databases to backup:"

    databases |> Seq.iter (printfn "%s"))

let tarsnap = platformTool "tarsnap" "tarsnap.exe"

Target "Upload" (fun _ ->
    let backupName =
        DateTime.UtcNow.ToString "o"
        |> sprintf "%s-%s" System.Environment.MachineName

    printfn "Archiving backup folder %s as backup %s. Tarsnap uses diffing to cache files that have already been uploaded, so upload size won't matter." outputFolder backupName

    let args = sprintf "-c -f %s %s" backupName outputFolder

    // Run tarsnap
    run tarsnap args __SOURCE_DIRECTORY__
)

// Defines the order and dependencies of targets.
"List"
  ==> "Backup"
  ==> "Upload"

RunTargetOrDefault "List"