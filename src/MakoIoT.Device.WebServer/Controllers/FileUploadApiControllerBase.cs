using System;
using System.IO;
using System.Net;
using MakoIoT.Device.Services.Interface;
using MakoIoT.Device.Services.Server.WebServer;
using MakoIoT.Device.WebServer.Helpers;

namespace MakoIoT.Device.WebServer.Controllers
{
    public abstract class FileUploadApiControllerBase
    {
        protected readonly ILog Logger;

        protected FileUploadApiControllerBase(ILog logger)
        {
            Logger = logger;
        }

        protected void PostFile(WebServerEventArgs e, string filePath)
        {
            try
            {
                ParseFormMultipart(e.Context.Request.Headers["content-type"], e.Context.Request.InputStream, filePath);
            }
            catch (Exception exception)
            {
                Logger.Error(exception);
                MakoWebServer.OutputHttpCode(e.Context.Response, HttpStatusCode.InternalServerError);
                return;
            }

            MakoWebServer.OutputHttpCode(e.Context.Response, HttpStatusCode.OK);
        }

        private void ParseFormMultipart(string contentType, Stream requestStream, string filePath)
        {
            var boundary = $"--{contentType.Substring(contentType.IndexOf("boundary=") + 9)}";
            var finalBoundary = $"{boundary}--";

            var reader = new RequestStreamReader(requestStream);
            var line = "";
            while (line != finalBoundary)
            {
                if (line == boundary)
                {
                    line = reader.ReadLine();
                    if (line.ToLower().StartsWith("content-disposition:"))
                    {
                        var dispositionItems = line.Split(';');
                        var fieldName = dispositionItems.Length > 1
                            ? dispositionItems[1].Split('=')[1].Trim('\"', ' ')
                            : "";

                        line = reader.ReadLine();

                        if (line.ToLower().StartsWith("content-type:"))
                        {
                            //skip empty line
                            reader.ReadLine();

                            //process file
                            line = SaveFile(reader, boundary, filePath);
                        }
                    }
                }
                else
                {
                    line = reader.ReadLine();
                }
            }
        }

        private string SaveFile(StreamReader reader, string boundary, string fileName)
        {
            //skip empty lines
            string line;
            do
            {
                line = reader.ReadLine();

            } while (line == "");

            if (line == null || line.StartsWith(boundary))
                return line;

            using var writer = new StreamWriter(File.OpenWrite(fileName));
            while (line != null && !line.StartsWith(boundary))
            {
                writer.WriteLine(line);
                line = reader.ReadLine();
            }
            writer.Close();
            Logger.Trace($"File {fileName} saved");

            return line;
        }
    }
}
