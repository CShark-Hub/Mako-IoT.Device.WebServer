using System.IO;
using System.Net;
using System.Text;
using System.Collections;
using System;
using MakoIoT.Device.Services.FileStorage;
using MakoIoT.Device.Services.Interface;
using MakoIoT.Device.WebServer.Extensions;
using MakoIoT.Device.WebServer.Helpers;

namespace MakoIoT.Device.WebServer.Controllers
{
    public abstract class WebControllerBase
    {
        protected readonly ILog Logger;
        protected readonly string BaseFile;
        protected Hashtable HtmlParams;
        protected readonly Hashtable Form = new();

        private Hashtable _paramsInstances;
        private int _sourceLength = 0;

        protected WebControllerBase(string baseFile, ILog logger)
        {
            Logger = logger;
            BaseFile = baseFile;
            ParseParams();
        }

        protected virtual void Render(HttpListenerResponse response, bool copyFormToParams = false)
        {
            if (copyFormToParams)
            {
                foreach (var formKey in Form.Keys)
                {
                    HtmlParams.AddOrUpdate(formKey, Form[formKey]);
                }
            }

            //compute response length
            var totalLength = _sourceLength;
            foreach (string key in _paramsInstances.Keys)
            {
                totalLength += (int)_paramsInstances[key] * ((string)HtmlParams[key]).Length;
            }

            response.ContentType = "text/html; charset=utf-8";
            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentLength64 = totalLength;

            using var reader = new StreamReader(new StaticLengthStream(File.OpenRead(BaseFile)));
            var writer = new StreamWriter(response.OutputStream);

            int transferredLength = 0;

            string line = reader.ReadLine();
            while (line != null)
            {
                line = ReplaceParams(line);

                transferredLength += line.Length;

                writer.Write(line);
                line = reader.ReadLine();
            }

            Logger.Trace($"Http response totalLength={totalLength}, transferredLength={transferredLength}");

            writer.Flush();
            reader.Close();
        }

        protected void ParseParams()
        {
            HtmlParams = new Hashtable();
            _paramsInstances = new Hashtable();

            using var reader = new StreamReader(new StaticLengthStream(File.OpenRead(BaseFile)));
            string line = reader.ReadLine();

            while (line != null)
            {
                _sourceLength += line.Length;

                if (line.IndexOf('{') > -1)
                {
                    var sp = line.Split('{', '}');
                    for (int i = 1; i < sp.Length; i += 2)
                    {
                        _sourceLength -= sp[i].Length + 2;
                        AddParam(sp[i]);
                    }
                }

                line = reader.ReadLine();
            }
            reader.Close();

        }

        private void AddParam(string key)
        {
            if (HtmlParams.Contains(key))
            {
                _paramsInstances[key] = (int)_paramsInstances[key] + 1;
            }
            else
            {
                HtmlParams.Add(key, string.Empty);
                _paramsInstances.Add(key, 1);
            }
        }

        private string ReplaceParams(string s)
        {
            if (s.IndexOf('{') == -1)
                return s;

            var builder = new StringBuilder();
            var sp = s.Split('{', '}');
            for (int i = 0; i < sp.Length; i++)
            {
                builder.Append(i % 2 == 0 ? sp[i] : (string)HtmlParams[sp[i]]);
            }

            return builder.ToString();
        }

        protected void ParseForm(HttpListenerRequest request, FileUploadDelegate fileUploadDelegate = null)
        {
            ParseForm(request.Headers["content-type"], request.ContentLength64, request.InputStream, fileUploadDelegate);

        }

        protected virtual void ParseForm(string contentType, long contentLength, Stream requestStream, FileUploadDelegate fileUploadDelegate = null)
        {
            if (contentType == "application/x-www-form-urlencoded")
            {
                FormParser.ParseFormUrlencoded(contentLength, requestStream, Form);
                return;
            }

            if (contentType.StartsWith("multipart/form-data"))
            {
                FormParser.ParseFormMultipart(contentType, contentLength, requestStream, fileUploadDelegate, Form);
                return;
            }

            throw new NotSupportedException("Content Type not supported");
        }

        
    }
}
