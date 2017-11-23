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

    let mutable errorCallback = fun (res : ParseResult) -> ()

    let app = express.Invoke()