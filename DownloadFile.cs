using HeyRed.Mime;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace TranSmart.API.Extensions
{
    public class DownloadFile : FileStreamResult
    {
        public DownloadFile(MemoryStream data, string filename) :
            base(data, MimeTypesMap.GetMimeType(Path.GetExtension(filename)))
        {
            data.Seek(0, SeekOrigin.Begin);
            base.FileDownloadName = filename;
        }
    }

}
