module SftpTests

    open Akka.FSharp
    open Akkling
    open Akkling.TestKit
    open Xunit
    open FakeSftpClient

    [<Fact>]
    let ``Successful upload should generate Completed event`` () = testDefault <| fun tck -> 

        let clientFactory = FakeClientFactory()
        let system = System.create "system" <| Configuration.load ()
        let sftp = spawn system "sftp" <| sftpActor clientFactory tck.TestActor

        let localPath = @"C:\Temp\t.txt"
        let remotePath = "/152818/no/open/test/12345.txt"
        sftp <! UploadFile (UncPath localPath, Url remotePath)
    
        expectMsg tck <| Completed |> ignore
