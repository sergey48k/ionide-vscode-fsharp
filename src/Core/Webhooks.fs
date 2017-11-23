namespace Ionide.VSCode.FSharp

open System
open Fable.Core
open Fable.Core.JsInterop
open Fable.Import
open Fable.Import.vscode
open Fable.Import.Node
open Ionide.VSCode.Helpers
open Fable.Import.ws
open Fable.Import.Express

open DTO
open Ionide.VSCode.Helpers

module Webhooks =
    let port = (int LanguageService.port) + 1

    let mutable errorCallback = fun (_ : ParseResult) -> ()
    let mutable workspaceCallback = fun (_ : Choice<ProjectResult,ProjectLoadingResult,(string * ErrorData)>) -> ()

    let app = express.Invoke()

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
            let n = req.body
            if unbox n?Kind = "errors" then
                n |> unbox |> errorCallback
            (res.send "OK") |> box )
    |> ignore

    let activate () =
        app.listen(port, (fun _ -> printfn "Webhooks listening at port: %d" port) |> unbox)