# StreamConduit C# .Net Core 2.0
Change direction of stream. Read from a write stream or write to a read stream.

e.g.  FTP only allowing download to write-stream converted into read-stream.

            //Prepare operation for FTP to write to stream
            Func<Stream, Task> MyProcess = async (writeStream) =>
            {
                using (var sftp = new SftpClient("ftpServerName.com", 22, "ftpUserName", "password"))
                {
                    sftp.Connect();
                    await Task.Factory.FromAsync(sftp.BeginDownloadFile(remoteFile, writeStream), sftp.EndDownloadFile);
                }
            };

            //Pass function to StreamConduit and read from FTP while it writes to the stream            
            //On first read MyProcess function will execute and start writing as you start reading.
            using (Stream readStream = new StreamConduit(MyProcess))
            {            
              byte[] lBuf = new byte[4096];

              int lRead;

              while ((lRead = readStream.Read(lBuf, 0, lBuf.Length)) > 0)
              {
                  Console.Write(Encoding.UTF8.GetString(lBuf,0,lRead));
              }
            }
            

 e.g.  DataLake only allowing upload from read-stream converted into write-stream for use.
 
            DataLakeFileClient fc = await FileClient.CreateFileAsync("myfile.txt");

            //Prepare operation where uploading a file which requires a read stream.
            Func<Stream, Task> MyProcess = async (readStream) =>
            {
                await fc.UploadAsync(readStream, true); 
            };

            //Pass function to StreamConduit and use the stream to write. 
            //The upload will read as you write to the stream.            
            using (Stream writeStream = new StreamConduit(MyProcess))
            {
              writeStream.write(Encoding.UTF8.GetBytes("Hello World!"));
            }
            
            
