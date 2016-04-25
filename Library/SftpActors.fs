[<AutoOpen>]
module SftpActors
    open System
    open System.IO
    open Akka
    open Akka.FSharp
    open SftpClient
    open Utils

    type SftpCommand =
        | ListDirectory of Url
        | UploadFile of UncPath * Url
        | DownloadFile of UncPath * Url
        | Cancel of string

    type SftpCommandResult =
        | Completed
        | Cancelled
        | Error of string

    [<Literal>]
    let private ConnectionTimeoutInSeconds = 10.

    let sftpActor (clientFactory : IClientFactory) (notify: Actor.IActorRef) (mailbox: Actor<_>) =

        let setReceiveTimeout () =
            mailbox.Context.SetReceiveTimeout(Nullable<TimeSpan>(TimeSpan.FromSeconds(ConnectionTimeoutInSeconds)))
            DateTimeOffset.Now

        let resetReceiveTimeout () =
            mailbox.Context.SetReceiveTimeout(Nullable())

        let connect () =
            let connection = clientFactory.CreateSftpClient()
            connection.Connect()
            connection

        let disconnect (connection : ISftpClient) =
            resetReceiveTimeout ()
            connection.Disconnect()
            connection.Dispose()

        let fileStreamProvider = clientFactory.CreateFileStreamProvider()

        let rec disconnected () = 
            actor {
                let! (message : obj) = mailbox.Receive ()
                match message with
                | :? SftpCommand as command -> 
                    match command with
                    | Cancel _ -> 
                        printfn "Sftp: No active operation to cancel"
                        return! disconnected ()
                    | _ -> 
                        mailbox.Stash ()
                        let connection = connect ()
                        mailbox.UnstashAll ()
                        return! connected (connection, DateTimeOffset.Now)

                | _ ->
                    cprintfn ConsoleColor.Yellow "Sftp: invalid operation in disconnected state: %A" message

                return! disconnected ()
            } 
        and connected (connection : ISftpClient, idleFromTime : DateTimeOffset) = 
            actor {
                let! (message : obj) = mailbox.Receive ()
                resetReceiveTimeout ()
                match message with
                | :? SftpCommand as command -> 
                    match command with
                    | ListDirectory remotePath -> 
                        let result = 
                            try
                                connection.ListDirectory(remotePath.Value, noProgressCallback) 
                                |> List.ofSeq
                                |> Some
                            with
                            | ex -> 
                                mailbox.Self <! Seq.empty
                                None
                        mailbox.Sender() <! result

                    | UploadFile (localPath, remotePath) -> 
                        let asyncCallback (ar : IAsyncResult) = 
                            try
                                connection.EndUploadFile(ar)
                                mailbox.Self <! if clientFactory.CreateSftpAsyncResult(ar).IsCanceled then Cancelled else Completed
                            with 
                            | ex -> 
                                mailbox.Self <! Error ex.Message
                        ensureParentDirectoryExists connection (remotePath.Value)
                        let stream = fileStreamProvider.OpenRead(localPath.Value)
                        let asyncResult = connection.BeginUploadFile(stream, remotePath.Value, AsyncCallback asyncCallback, null, noProgressCallback)
                        return! transferring (connection, asyncResult, stream)

                    | DownloadFile (localPath, remotePath) -> 
                        let asyncCallback (ar : IAsyncResult) = 
                            try
                                connection.EndDownloadFile(ar)
                                mailbox.Self <! if clientFactory.CreateSftpAsyncResult(ar).IsCanceled then Cancelled else Completed
                            with 
                            | ex -> 
                                mailbox.Self <! Error ex.Message
                        let stream = fileStreamProvider.OpenWrite(localPath.Value)
                        let asyncResult = connection.BeginDownloadFile(remotePath.Value, stream, AsyncCallback asyncCallback, null, noProgressCallback)
                        return! transferring (connection, asyncResult, stream)

                    | Cancel _ -> 
                        printfn "Sftp: No active operation to cancel"
                        return! connected (connection, DateTimeOffset.Now)

                | :? Actor.ReceiveTimeout ->
                    if (DateTimeOffset.Now - idleFromTime > TimeSpan.FromSeconds(ConnectionTimeoutInSeconds)) then
                        disconnect connection
                        return! disconnected ()

                | _ ->
                    cprintfn ConsoleColor.Yellow "Sftp: invalid operation in connected state: %A" message

                return! connected (connection, DateTimeOffset.Now)
            } 
        and transferring (connection : ISftpClient, asyncResult : IAsyncResult, stream : Stream) =
            actor {
                let! (message : obj) = mailbox.Receive ()
                match message with
                | :? SftpCommand as command ->
                    match command with
                    | Cancel _ -> 
                        clientFactory.CreateSftpAsyncResult(asyncResult).IsCanceled <- true
                    | _ -> mailbox.Stash ()

                | :? SftpCommandResult as result ->
                    notify <! result
                    match result with
                    | Completed _ ->
                        stream.Close()
                        mailbox.UnstashAll ()
                        return! connected (connection, setReceiveTimeout ())
                    | Cancelled ->
                        stream.Close()
                        mailbox.UnstashAll ()
                        return! connected (connection, setReceiveTimeout ())
                    | Error error -> 
                        disconnect (connection)
                        stream.Close()
                        mailbox.UnstashAll ()
                        return! disconnected ()

                return! transferring (connection, asyncResult, stream)
            }

        disconnected ()
