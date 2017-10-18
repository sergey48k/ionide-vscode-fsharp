namespace Ionide.VSCode.FSharp

open System
open Fable.Core
open Fable.Import
open Fable.Import.vscode
open Fable.Import.Node
open Fable.Core.JsInterop
open DTO
open Ionide.VSCode.Helpers

module ReferenceCodeLens =
    let refresh = EventEmitter<int>()
    let mutable private version = 0

    let interestingSymbolPositions (symbols: Symbols[]): DTO.Range[] =
        symbols |> Array.collect(fun syms ->
            let interestingNested = syms.Nested |> Array.choose (fun sym ->
                if sym.GlyphChar <> "Fc"
                   && sym.GlyphChar <> "M"
                   && sym.GlyphChar <> "F"
                   && sym.GlyphChar <> "P"
                   || sym.IsAbstract
                   || sym.EnclosingEntity = "I"  // interface
                   || sym.EnclosingEntity = "R"  // record
                   || sym.EnclosingEntity = "D"  // DU
                   || sym.EnclosingEntity = "En" // enum
                   || sym.EnclosingEntity = "E"  // exception
                then None
                else Some sym.BodyRange)

            if syms.Declaration.GlyphChar <> "Fc" then
                interestingNested
            else
                interestingNested |> Array.append [|syms.Declaration.BodyRange|])

    let private createProvider () =
        let symbolsToCodeLens (doc : TextDocument) (symbols: Symbols[]) : CodeLens[] =
            interestingSymbolPositions symbols
                |> Array.map (CodeRange.fromDTO >> CodeLens)

        { new CodeLensProvider with
            member __.provideCodeLenses(doc, _) =
                promise {
                    let text = doc.getText()
                    let! result = LanguageService.declarations doc.fileName text version
                    let data =
                        if isNotNull result then
                            let res = symbolsToCodeLens doc result.Data
                            res
                        else [||]
                    return ResizeArray data
                }
                |> U2.Case2

            member __.resolveCodeLens(codeLens, _) =
                let load () =
                    promise {
                        let! (signaturesResult : SymbolUseResult) =
                            LanguageService.symbolUseProject
                                window.activeTextEditor.document.fileName
                                (int codeLens.range.start.line + 1)
                                (int codeLens.range.start.character + 1)
                        let cmd = createEmpty<Command>
                        cmd.title <- if isNotNull signaturesResult then sprintf "%d references" signaturesResult.Data.Uses.Length else ""
                        cmd.command <- "editor.action.showReferences"
                        let fn = Uri.file window.activeTextEditor.document.fileName
                        let pos = codeLens.range.start


                        let locs =
                            if isNotNull signaturesResult then
                                signaturesResult.Data.Uses |> Array.map (fun u ->
                                    let r = CodeRange.fromSymbolUse u
                                    Location(Uri.file u.FileName, U2.Case1 r ))
                            else [||]
                        cmd.arguments <- Some (ResizeArray [| box fn; box pos; box locs |])
                        codeLens.command <- cmd
                        return codeLens

                    }

                if int window.activeTextEditor.document.version > version then
                    Promise.create (fun resolve error ->
                        let mutable disp : Disposable option = None
                        let d = refresh.event.Invoke(unbox (fun n ->
                            resolve ()
                            disp |> Option.iter (fun n -> n.dispose () |> ignore)))
                        disp <- Some d

                    )
                    |> Promise.bind (fun _ ->
                        load ()
                    )
                    |> U2.Case2

                else
                    load () |> U2.Case2

            member __.onDidChangeCodeLenses = EventEmitter().event
        }

    let activate selector (context: ExtensionContext) =
        refresh.event.Invoke(fun n -> (version <- n ) |> unbox) |> context.subscriptions.Add
        languages.registerCodeLensProvider(selector, createProvider()) |> context.subscriptions.Add
        ()
