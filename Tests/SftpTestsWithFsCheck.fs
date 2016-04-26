module SftpTestsWithFsCheck

    open Akka.FSharp
    open Akkling
    open Akkling.TestKit
    open Xunit
    open FsCheck
    open FsCheck.Xunit
    open FakeSftpClient

    type CommonTypes =
        static member Alphanumeric() = Generators.Alphanumeric() |> Arb.fromGen
        static member RelativeUrl() = Generators.RelativeUrl() |> Arb.fromGen
        static member AbsoluteUrl() = Generators.AbsoluteUrlWithBase("http://remoteserver.com/") |> Arb.fromGen
        static member Url() = Generators.Url() |> Arb.fromGen
        static member UncPath() = Generators.UncPath() |> Arb.fromGen
        static member FileName() = Generators.FileName() |> Arb.fromGen

    type UploadPropertyAttribute() =
        inherit PropertyAttribute(Arbitrary = [| typeof<CommonTypes> |])

    [<UploadProperty>]
    let ``Upload of multiple files with randomly generated paths should generate Completed event`` 
        (localPath : UncPath) (remotePath : RelativeUrl) = testDefault <| fun tck -> 

        let clientFactory = FakeClientFactory()
        let system = System.create "system" <| Configuration.load ()
        let sftp = spawn system "sftp" <| sftpActor clientFactory tck.TestActor

        sftp <! UploadFile (localPath, Url remotePath.Value)
    
        expectMsg tck <| Completed |> ignore

