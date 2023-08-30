using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TranSmart.API.Models.Import;

namespace TranSmart.API.Services.Import
{
    public interface IAttendanceService : IImportBaseService<AttendanceLogsImportModel>
    {
        MemoryStream Attendance(Dictionary<string, Dictionary<string, string>> DictionaryList);
    }
    public class AttendanceService : ImportBaseService<AttendanceLogsImportModel>, IAttendanceService
    {
        public AttendanceService()
        {

        }
        public MemoryStream Attendance(Dictionary<string, Dictionary<string, string>> DictionaryList)
        {

            return ClosedXmlGeneric.DataExport("Attendance", DictionaryList);
        }
        public override IEnumerable<AttendanceLogsImportModel> ToModel(string path, int sheetNo = 1)
        {
            Dictionary<string, IList<AttendanceLogsImportModel>> data;
            if (System.IO.File.Exists(path))
            {
                data = ClosedXmlGeneric.Import<AttendanceLogsImportModel>(path);
                if (data.Count >= sheetNo)
                {
                    IEnumerable<AttendanceLogsImportModel> values = data.ElementAtOrDefault(sheetNo - 1).Value;
                    return values.Where(x => x.EmployeeCode != null);
                }
            }
            else { throw new FileNotFoundException(); }
            return new List<AttendanceLogsImportModel>();
        }
        public override IEnumerable<AttendanceLogsImportModel> ToModel(Stream stream, int sheetNo = 1)
        {
            Dictionary<string, IList<AttendanceLogsImportModel>> data;
            data = ClosedXmlGeneric.Import<AttendanceLogsImportModel>(stream);
            if (data.Count >= sheetNo)
            {
                IEnumerable<AttendanceLogsImportModel> values = data.ElementAtOrDefault(sheetNo - 1).Value;
                return values.Where(x => x.EmployeeCode != null);
            }

            return new List<AttendanceLogsImportModel>();
        } 
        public override MemoryStream Sample()
        {
            return ClosedXmlGeneric.Export<AttendanceLogsImportModel>("AttendanceLogsImport", new List<AttendanceLogsImportModel>());
        }
        public override bool ValidateHeaders(Stream stream)
        {
            var colimnsList = ClosedXmlGeneric.GetColomnList(typeof(AttendanceLogsImportModel));
            bool valid = true;
            Dictionary<int, string> headers = ClosedXmlGeneric.Header<AttendanceLogsImportModel>(stream);

            for (int i = 0; i < colimnsList.Count; i++)
            {
                if (!headers.ContainsValue(colimnsList[i].Attribute.GetName()))
                {
                    valid = false;
                    break;
                }
            }

            return valid;
        }
    }
}
