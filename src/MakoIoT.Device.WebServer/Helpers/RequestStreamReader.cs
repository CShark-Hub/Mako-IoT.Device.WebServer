﻿
using System.IO;

namespace MakoIoT.Device.WebServer.Helpers
{
    public class RequestStreamReader : StreamReader
    {
        public RequestStreamReader(Stream stream) : base(stream)
        {
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(false);
        }
    }
}
