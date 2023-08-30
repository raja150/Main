using System.Collections.Generic;
using System.IO;
using System.Linq;
using TranSmart.API.Models.Import;

namespace TranSmart.API.Services.Import
{
	public interface IAttendanceImportService : IImportBaseService<AttendanceImportModel>
	{
		MemoryStream Attendance(Dictionary<string, Dictionary<string, string>> DictionaryList);
	}
	public class AttendanceImportService : ImportBaseService<AttendanceImportModel>, IAttendanceImportService
	{
		public AttendanceImportService()
		{

		}
		public MemoryStream Attendance(Dictionary<string, Dictionary<string, string>> DictionaryList)
		{

			return ClosedXmlGeneric.DataExport("Attendance", DictionaryList);
		}
		public override IEnumerable<AttendanceImportModel> ToModel(string path, int sheetNo = 1)
		{
			Dictionary<string, IList<AttendanceImportModel>> data;
			if (System.IO.File.Exists(path))
			{
				data = ClosedXmlGeneric.Import<AttendanceImportModel>(path);
				if (data.Count >= sheetNo)
				{
					IEnumerable<AttendanceImportModel> values = data.ElementAtOrDefault(sheetNo - 1).Value;
					return values.Where(x => x.EmployeeCode != null);
				}
			}
			else { throw new FileNotFoundException(); }
			return new List<AttendanceImportModel>();
		}
		public override IEnumerable<AttendanceImportModel> ToModel(Stream stream, int sheetNo = 1)
		{
			Dictionary<string, IList<AttendanceImportModel>> data;
			data = ClosedXmlGeneric.Import<AttendanceImportModel>(stream);
			if (data.Count >= sheetNo)
			{
				IEnumerable<AttendanceImportModel> values = data.ElementAtOrDefault(sheetNo - 1).Value;
				return values.Where(x => x.EmployeeCode != null);
			}

			return new List<AttendanceImportModel>();
		}
		public override MemoryStream Sample()
		{
			return ClosedXmlGeneric.Export<AttendanceImportModel>("Attendance", new List<AttendanceImportModel>());
		}
		public override bool ValidateHeaders(Stream stream)
		{
			var colimnsList = ClosedXmlGeneric.GetColomnList(typeof(AttendanceImportModel));
			bool valid = true;
			Dictionary<int, string> headers = ClosedXmlGeneric.Header<AttendanceImportModel>(stream);

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
