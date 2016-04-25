module FakeSftpClient

    open System
    open System.IO

    type FakeAsyncResult() =
        interface IAsyncResult with
            member this.AsyncState = null
            member this.AsyncWaitHandle = null
            member this.CompletedSynchronously = true
            member this.IsCompleted = true

    type FakeSftpAsyncResult(ar : IAsyncResult) =
        interface IAsyncResult with
            member this.AsyncState = ar.AsyncState
            member this.AsyncWaitHandle = ar.AsyncWaitHandle
            member this.CompletedSynchronously = ar.CompletedSynchronously
            member this.IsCompleted = ar.IsCompleted
        interface ISftpAsyncResult with
            member this.AsyncResult = this :> IAsyncResult
            member this.IsCanceled with get() = false and set(value) = ()

    let private getAsyncResult(ac : AsyncCallback) =
        let result = FakeAsyncResult() :> IAsyncResult
        let asyncCallback = async {
            System.Threading.Thread.Sleep(100)
            ac.Invoke(result)
        }
        Async.Start(asyncCallback)
        result

    type FakeSftpClient() =
        interface ISftpClient with
            member this.Connect() = ()
            member this.Disconnect() = ()
            member this.DownloadFile(path, stream, progress) = ()
            member this.BeginDownloadFile(path, stream, ac, state, progress) = getAsyncResult(ac)
            member this.EndDownloadFile(ar) = ()
            member this.UploadFile(path, stream, progress) = ()
            member this.BeginUploadFile(stream, path, ac, state, progress) = getAsyncResult(ac)
            member this.EndUploadFile(ar) = ()
            member this.RenameFile(sourcePath, destinationPath) = ()
            member this.DeleteFile(path) = ()
            member this.CreateDirectory(path) = ()
            member this.DeleteDirectory(path) = ()
            member this.ListDirectory(path, progress) = Seq.empty
            member this.DirectoryExists(path) = true
            member this.Dispose() = ()
        interface System.IDisposable with 
            member this.Dispose() = ()

    type FakeSshCommand() =
        interface ISshCommand with
            member this.Execute() = String.Empty
            member this.BeginExecute(ac) = getAsyncResult(ac)
            member this.EndExecute(ar) = String.Empty
            member this.CancelAsync() = ()

    type FakeSshClient() =
        interface ISshClient with
            member this.Connect() = ()
            member this.Disconnect() = ()
            member this.CreateCommand(cmd) = FakeSshCommand() :> ISshCommand
            member this.Dispose() = ()
        interface System.IDisposable with 
            member this.Dispose() = ()

    type FakeFileStreamProvider() =
        interface IFileStreamProvider with
            member this.OpenRead(path : string) = new MemoryStream() :> Stream
            member this.OpenWrite(path : string) = new MemoryStream() :> Stream

    type FakeClientFactory() =
        interface IClientFactory with
            member this.CreateSftpClient() = new FakeSftpClient() :> ISftpClient
            member this.CreateSshClient() = new FakeSshClient() :> ISshClient
            member this.CreateFileStreamProvider() = new FakeFileStreamProvider() :> IFileStreamProvider
            member this.CreateSftpAsyncResult(ar : IAsyncResult) = FakeSftpAsyncResult(ar) :> ISftpAsyncResult
