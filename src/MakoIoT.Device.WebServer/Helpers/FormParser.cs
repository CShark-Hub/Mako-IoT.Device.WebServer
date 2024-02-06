using System.Collections;
using static MakoIoT.Device.WebServer.Controllers.WebControllerBase;
using System.IO;
using System.Text;
using System.Web;
using MakoIoT.Device.WebServer.Extensions;

namespace MakoIoT.Device.WebServer.Helpers
{
    public delegate string FileUploadDelegate(string fieldName, string fileName, StreamReader contentsReader, string boundary);

    public static class FormParser
    {
        public static void ParseFormUrlencoded(long contentLength, Stream requestStream, Hashtable form)
        {
            byte[] buffer = new byte[contentLength];
            requestStream.Read(buffer, 0, buffer.Length);
            var items = Encoding.UTF8.GetString(buffer, 0, buffer.Length).Split('&');
            foreach (var item in items)
            {
                var i = item.Split('=');
                form.AddOrUpdate(i[0], i.Length > 1 ? HttpUtility.UrlDecode(i[1]) : "");
            }
        }

        public static void ParseFormMultipart(string contentType, long contentLength, Stream requestStream, FileUploadDelegate fileUploadDelegate, Hashtable form)
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
                            if (fileUploadDelegate != null)
                            {
                                string fileName = dispositionItems.Length > 2
                                    ? dispositionItems[2].Split('=')[1].Trim('\"', ' ')
                                    : "";

                                line = fileUploadDelegate(fieldName, fileName, reader, boundary);
                            }
                        }
                        else
                        {
                            var fieldValue = "";
                            line = reader.ReadLine();
                            while (!line.StartsWith(boundary))
                            {
                                fieldValue += $"{line}\r\n";
                                line = reader.ReadLine();
                            }

                            fieldValue = fieldValue.TrimEnd('\r', '\n');
                            form.AddOrUpdate(fieldName, fieldValue);
                        }
                    }
                }
                else
                {
                    line = reader.ReadLine();
                }
            }
        }
    }
}
