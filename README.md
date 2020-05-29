# StreamConduit C# .Net Core 2.0
Change direction of stream. Read from a write stream or write to a read stream.

e.g.  FTP download to write-stream converted into read-stream.

            Func<Stream, Task> MyProcess = async (stream) =>
            {
                using (var sftp = new SftpClient("ftpServerName.com", 22, "ftpUserName", "password"))
                {
                    sftp.Connect();
                    await Task.Factory.FromAsync(sftp.BeginDownloadFile(remoteFile, stream), sftp.EndDownloadFile);
                }
            };

            //Executes MyProcess on first read.
            using (Stream readStream = new StreamConduit(MyProcess))
            {            
              byte[] lBuf = new byte[4096];

              int lRead;

              while ((lRead = readStream.Read(lBuf, 0, lBuf.Length)) > 0)
              {
                  Console.Write(Encoding.UTF8.GetString(lBuf,0,lRead));
              }
            }
            

 e.g.  DataLake upload to read-stream converted into write-stream for use.
 
            DataLakeFileClient fc = await FileClient.CreateFileAsync("myfile.txt");

            Func<Stream, Task> MyProcess = async (stream) =>
            {
                await fc.UploadAsync(stream, true); 
            };

            //Executes MyProcess on first write.
            using (Stream writeStream = new StreamConduit(MyProcess))
            {
              writeStream.write(Encoding.UTF8.GetBytes("Hello World!"));
            }
            
            
