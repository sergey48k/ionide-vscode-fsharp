namespace Ionide.VSCode.FSharp

open Fable.Core
open JsInterop
open Fable.Import

open DTO

module Webhooks =
    let port = (int LanguageService.port) + 1

    let mutable errorCallback = fun (a : ParseResult) -> printfn "ERROR CALLBACK: %A" a
    let mutable workspaceCallback = fun (a : Choice<ProjectResult,ProjectLoadingResult,(string * ErrorData)>) -> printfn "WORKSPACE CALLBACK: %A" a
    let app = express.Invoke()
    let middleware (req:express.Request) (res:express.Response) (next: unit -> unit) =
      req.body <- ""
      req.setEncoding("utf8")
      req.on("data", fun (chunk : string) -> req.body <- unbox req.body + chunk) |> ignore
      req.on("end", fun _ -> next ()) |> ignore

    app?``use`` $ (middleware |> unbox ) |> ignore

    app.all
      ( U2.Case1 "/notifyWorkspace",
        fun (req:express.Request) (res:express.Response) _ ->
            let r = req.body
            match r?Kind |> unbox with
            | "project" ->
                r |> unbox<ProjectResult> |> LanguageService.deserializeProjectResult |> Choice1Of3 |> workspaceCallback
            | "projectLoading" ->
                r |> unbox<ProjectLoadingResult> |> Choice2Of3 |> workspaceCallback
            | "error" ->
                r?Data |> LanguageService.parseError |> Choice3Of3 |> workspaceCallback
            | _ ->
                ()
            (res.send "OK") |> box )
    |> ignore

    app.all
      ( U2.Case1 "/notifyErrors",
        fun (req:express.Request) (res:express.Response) _ ->
            let n = req.body |> unbox |> ofJson
            if unbox n?Kind = "errors" then
                n |> unbox |> errorCallback
            (res.send "OK") |> box )
    |> ignore

    let activate () =
        app.listen(port, (fun _ -> printfn "Webhooks listening at port: %d" port) |> unbox)
        |> ignore