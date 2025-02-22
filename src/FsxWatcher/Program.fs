open System
open System.IO
open FSharp.Compiler.Interactive.Shell
open System.Threading
open Argu


// Define the CLI arguments
type Command =
    | [<CustomCommandLine("--watch-path")>] WatchPath of string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | WatchPath _ -> "Specify the path to watch for changes"

let args =
    let parser = ArgumentParser.Create<Command>(programName = "FsxWatcher")
    let argv = 
        System.Environment.GetCommandLineArgs()
        |> Array.skipWhile (fun x -> x.StartsWith("-") |> not)
    parser.ParseCommandLine(argv)

let watchPath =
    match args.TryGetResult(WatchPath) with
    | Some path ->
        printfn $"Watching path: {path}"
        path
    | None ->
        let dir = Environment.CurrentDirectory
        printfn $"No watch path specified. Using current directory: {dir}"
        dir

let fsiSession, outStream, errStream = 
    let argv = [| "fsi.exe"; "--noninteractive" |]
    let inStream = new StringReader("")
    let outStream = new StringWriter()
    let errStream = new StringWriter()
    let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
    let session = FsiEvaluationSession.Create(fsiConfig, argv, inStream, outStream, errStream)
    session, outStream, errStream

let onChanged (e: FileSystemEventArgs) =
    if e.FullPath = Path.Combine(__SOURCE_DIRECTORY__, __SOURCE_FILE__) then () else

    let red = "\u001b[31m"
    let reset = "\u001b[0m"
    
    printfn $"Processing event {e.ChangeType} for file {e.FullPath}..."

    let sourceDir = Path.GetDirectoryName(e.FullPath)
    let sourceFile = e.FullPath

    try
        
        let res,diag =
            [
                $"""# silentCd @"{sourceDir}" """
                $"""# 1 @"{sourceFile}" """
                File.ReadAllText(e.FullPath)
            ]
            |> String.concat "\n"
            |> fsiSession.EvalInteractionNonThrowing

        match res with
        | Choice1Of2 _ -> ()
        | Choice2Of2 ex ->
            printfn $"{red}Error: {ex.Message}{reset}"
            for diag in diag do
                printfn $"{red}  {e.FullPath}:{diag.StartLine}:{diag.StartColumn} :: {diag.Message}{reset}"

            // TODO: really?

            printfn "%s" (outStream.ToString())
            printfn "%s" (errStream.ToString())

            outStream.GetStringBuilder().Clear() |> ignore
            errStream.GetStringBuilder().Clear() |> ignore
    with ex ->
        printfn $"{red}Error processing file {e.FullPath}: {ex.Message}{reset}"

do
    let w = new FileSystemWatcher(watchPath, "*.fsx")
    w.IncludeSubdirectories <- true
    w.NotifyFilter <- NotifyFilters.FileName ||| NotifyFilters.LastWrite
    w.Changed.Add(onChanged)
    w.Created.Add(onChanged)
    w.Renamed.Add(onChanged)
    w.EnableRaisingEvents <- true

printfn $"Watching {watchPath} for changes (press Ctrl+C to exit)..."

// Instead of using Console.ReadLine(), wait for Ctrl+C using an event:
let exitEvent = new ManualResetEvent(false)
Console.CancelKeyPress.Add(fun args ->
    printfn "Ctrl+C pressed. Exiting..."
    args.Cancel <- true
    exitEvent.Set() |> ignore)

exitEvent.WaitOne() |> ignore

printfn "FSI watcher closed."
