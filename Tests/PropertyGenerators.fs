[<AutoOpen>]
module PropertyGenerators

    open System
    open System.Text.RegularExpressions
    open FsCheck

    let private alphanumericChar = ['a'..'z'] @ ['A'..'Z'] @ ['0'..'9'] |> Gen.elements
    let private alphanumericString = alphanumericChar |> Gen.nonEmptyListOf |> Gen.map (fun x -> String(List.toArray x))

    type Generators =

        static member Version() =
            Arb.generate<byte>
            |> Gen.map int
            |> Gen.four
            |> Gen.map (fun (ma, mi, bu, re) -> Version(ma, mi, bu, re))

        static member Alphanumeric() =
            alphanumericString
            |> Gen.map Alphanumeric

        static member RelativeUrlWithSegmentSize(min : int, max : int) =
            gen {
                let! size = Gen.choose (min, max)
                let! segments = Gen.listOfLength size alphanumericString
                return String.concat "/" segments |> RelativeUrl }

        static member RelativeUrl() =
            Generators.RelativeUrlWithSegmentSize(1, 10)

        static member AbsoluteUrlWithBase(baseUrl : string) =
            Generators.RelativeUrl()
            |> Gen.map (fun url -> sprintf "%s/%s" baseUrl url.Value |> AbsoluteUrl)

        static member AbsoluteUrl() =
            Generators.AbsoluteUrlWithBase("http:/")

        static member Url() =
            gen {
                let! mode = Gen.choose (1, 2)
                let! url = match mode with 
                            | 1 -> Generators.RelativeUrl() |> Gen.map (fun x -> x.Value)
                            | 2 -> Generators.AbsoluteUrl() |> Gen.map (fun x -> x.Value) 
                return Url url }

        static member FileName() =
            gen {
                let! name = Generators.Alphanumeric()
                let! ext = Generators.Alphanumeric()
                return sprintf "%s.%s" name.Value ext.Value |> FileName }

        static member UncPath() =
            gen {
                let! mode = Gen.choose (1, 3)
                let! url = Generators.RelativeUrl()
                let! filename = Generators.FileName()
                let path = sprintf "%s\\%s" <| url.Value.Replace("/", "\\") <| filename.Value
                let path = match mode with
                            | 1 -> path
                            | 2 -> sprintf "C:\\%s" path
                            | 3 -> sprintf "\\\\%s" path
                return UncPath path }

    type GeneratorTypes =
        static member Alphanumeric() = Generators.Alphanumeric() |> Arb.fromGen
        static member AbsoluteUrl() = Generators.AbsoluteUrl() |> Arb.fromGen
        static member RelativeUrl() = Generators.RelativeUrl() |> Arb.fromGen
        static member Url() = Generators.Url() |> Arb.fromGen
        static member UncPath() = Generators.UncPath() |> Arb.fromGen
        static member FileName() = Generators.FileName() |> Arb.fromGen
