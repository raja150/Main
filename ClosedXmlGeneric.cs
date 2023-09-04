using ClosedXML.Excel;
using HeyRed.Mime;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using TranSmart.Core.Attributes;
using TranSmart.Domain.Models.Reports.Search;

namespace TranSmart.API.Services
{
	public static class ClosedXmlGeneric
	{/// <summary>
	 /// A Sheet Export Class to Excel
	 /// NOTE:Class DisplayName and Order Attribute Required
	 /// </summary> 
	 /// <typeparam name="T"></typeparam>
	 /// <param name="sheetName"></param>
	 /// <param name="data"></param>
	 /// <param name="fileName"></param>
		public static void Export<T>(string sheetName, IList<T> data, string filePath)
		{
			var workbook = new XLWorkbook();
			workbook.ToWorkSheet(data, sheetName);

			workbook.SaveAs(filePath);
		}

		public static MemoryStream Export<T>(string sheetName, IList<T> data)
		{
			var workbook = new XLWorkbook();
			workbook.ToWorkSheet(data, sheetName);
			var msA = new MemoryStream();
			workbook.SaveAs(msA);
			msA.Seek(0, SeekOrigin.Begin);
			return msA;
		}
		public static MemoryStream Export(string sheetName, Dictionary<int, string> data)
		{
			var workbook = new XLWorkbook();
			workbook.ToWorkSheet(data, sheetName);
			var msA = new MemoryStream();
			workbook.SaveAs(msA);
			msA.Seek(0, SeekOrigin.Begin);
			return msA;
		}
		/// <summary>
		/// Multiple Sheet Export Class to Excel
		/// NOTE:Class DisplayName and Order Attribute Required
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="data"></param>
		public static void Export<T>(Dictionary<string, IList<T>> data, string filePath)
		{
			var workbook = new XLWorkbook();
			foreach (var item in data)
			{
				workbook.ToWorkSheet(item.Value, item.Key);
			}
			workbook.SaveAs(filePath);
		}
		public static MemoryStream DataExport(string sheetName, Dictionary<string, Dictionary<string, string>> data)
		{
			var workbook = new XLWorkbook();
			workbook.ToWorkSheet(data, sheetName);
			workbook.Protect("1234");
			var msA = new MemoryStream();
			workbook.SaveAs(msA);
			msA.Seek(0, SeekOrigin.Begin);
			return msA;
		}

		public static MemoryStream DataExport(string sheetName, Dictionary<string, Tuple<string, int>> headers, Dictionary<string, object[]> data)
		{
			var workbook = new XLWorkbook();
			workbook.ToWorkSheet(headers, data, sheetName);
			var msA = new MemoryStream();
			workbook.SaveAs(msA);
			msA.Seek(0, SeekOrigin.Begin);
			return msA;
		}

		public static Dictionary<int, string> Header<T>(Stream stream) where T : new()
		{
			var headers = new Dictionary<int, string>();
			var workbook = new XLWorkbook(stream);
			var genericType = typeof(T);
			var columnList = GetColomnList(genericType);
			var workSheet = workbook.Worksheets.FirstOrDefault();
			if (workSheet != null)
			{
				var rowList = workSheet.Rows().ToList();
				var row = rowList[0];

				for (int x = 0; x < columnList.Count; x++)
				{
					var cell = row.Cell(x + 1);
					headers.Add(x, cell.Value.ToString().Trim());

				}
			}

			return headers;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="filePath"></param>
		/// <returns></returns>
		public static Dictionary<string, IList<T>> Import<T>(string filePath) where T : new()
		{
			var sheetList = new Dictionary<string, IList<T>>();
			var workbook = new XLWorkbook(filePath);
			foreach (var item in workbook.Worksheets)
			{
				sheetList.Add(item.Name, ToEntity<T>(item));
			}
			return sheetList;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="stream"></param>
		/// <returns></returns>
		public static Dictionary<string, IList<T>> Import<T>(Stream stream) where T : new()
		{
			var sheetList = new Dictionary<string, IList<T>>();
			var workbook = new XLWorkbook(stream);
			foreach (var item in workbook.Worksheets)
			{
				sheetList.Add(item.Name, ToEntity<T>(item));
			}
			return sheetList;
		}

		public static Dictionary<int, Dictionary<string, string>> Import(Stream stream)
		{
			var sheetList = new Dictionary<int, Dictionary<string, string>>();
			var workbook = new XLWorkbook(stream);

			foreach (var item in workbook.Worksheets)
			{
				int rowNo = 1;
				foreach (IXLRow row in item.Rows())
				{
					var columns = new Dictionary<string, string>();
					int cellNo = 1;
					if (rowNo != 1)
					{
						if (!row.IsEmpty())
						{
							foreach (IXLCell cell in row.Cells(false))
							{
								columns.Add(item.Row(1).Cell(cellNo).Value.ToString(), cell.Value.ToString());
								cellNo++;
							}
							sheetList.Add(rowNo - 1, columns);
						}
					}
					rowNo++;
				}
			}
			return sheetList;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="workBook"></param>
		/// <param name="data"></param>
		/// <param name="sheetName"></param>
		/// <returns></returns>
		private static IXLWorksheet ToWorkSheet<T>(this XLWorkbook workBook, IList<T> data, string sheetName = "Sheet1")
		{
			var genericType = typeof(T);
			var workSheet = workBook.Worksheets.Add(sheetName);
			//ColumnProperty Info
			var columnList = GetColomnList(genericType);

			for (int i = 0; i < columnList.Count; i++)
			{
				var column = columnList[i];
				var cell = workSheet.Cell(1, i + 1);

				cell.Value = column.Attribute.Name;
				cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
				cell.Style.Font.Bold = true;
				if (column.Attribute.Required)
				{
					cell.Style.Font.FontColor = XLColor.Red;
				}
			}

			// Create Rows 
			for (int rowIndex = 0; rowIndex < data.Count; rowIndex++)
			{
				var row = data[rowIndex];

				for (int columnIndex = 0; columnIndex < columnList.Count; columnIndex++)
				{
					var column = columnList[columnIndex];
					var cell = workSheet.Cell(rowIndex + 2, columnIndex + 1);
					cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

					var dd = Convert.ChangeType(DataType.GetObjectType(column.PropertyType, cell),
												DataType.GetPropType(column.PropertyType, cell));

					cell.Value = dd != null ? dd.ToString() : "";
				}
			}
			workSheet.Columns().AdjustToContents();
			return workSheet;
		}
		private static IXLWorksheet ToWorkSheet(this XLWorkbook workBook, Dictionary<string, Dictionary<string, string>> data, string sheetName = "Sheet1")
		{

			var workSheet = workBook.Worksheets.Add(sheetName);
			int RowNo = 1;
			foreach (KeyValuePair<string, Dictionary<string, string>> rowItem in data)
			{
				int ColumnNo = 1;
				foreach (KeyValuePair<string, string> item in rowItem.Value)
				{
					if (RowNo == 1)//If row is First row then adding columns headers with columns value
					{
						//Column header
						var cell = workSheet.Cell(RowNo, ColumnNo);
						cell.Value = item.Key;
						cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
						cell.Style.Font.Bold = true;

						//Column Value
						var FirstRowCell = workSheet.Cell(RowNo + 1, ColumnNo);
						FirstRowCell.Value = item.Value;
						FirstRowCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
						ColumnNo++;
					}
					else//more employees at a time then else will execute
					{
						var cell = workSheet.Cell(RowNo + 1, ColumnNo);
						cell.Value = item.Value;
						cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
						ColumnNo++;
					}
				}
				RowNo++;

			}
			workSheet.Columns().AdjustToContents();
			return workSheet;
		}

		private static IXLWorksheet ToWorkSheet(this XLWorkbook workBook, Dictionary<int, string> data, string sheetName = "Sheet1")
		{

			var workSheet = workBook.Worksheets.Add(sheetName);
			//ColumnProperty Info

			int RowNo = 0;
			int ColumnNo = 0;
			foreach (KeyValuePair<int, string> item in data.OrderBy(x => x.Key))
			{
				if (RowNo == 0)//for Column headers
				{
					var cell = workSheet.Cell(1, ColumnNo + 1);

					cell.Value = item.Value;
					cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
					cell.Style.Font.Bold = true;
					ColumnNo++;
				}
			}

			workSheet.Columns().AdjustToContents();
			return workSheet;
		}

		private static IXLWorksheet ToWorkSheet(this XLWorkbook workBook, Dictionary<string, Tuple<string, int>> headers, Dictionary<string, object[]> data, string sheetName = "Sheet1")
		{
			var workSheet = workBook.Worksheets.Add(sheetName);

			foreach (KeyValuePair<string, Tuple<string, int>> item in headers)
			{
				var cell = workSheet.Cell(1, item.Value.Item2 + 1);
				cell.Value = item.Value.Item1;
				cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
				cell.Style.Font.Bold = true;
			}
			int RowNo = 2;
			foreach (KeyValuePair<string, object[]> rowItem in data)
			{
				int ColumnNo = 1;
				foreach (object item in rowItem.Value)
				{
					var cell = workSheet.Cell(RowNo, ColumnNo);
					cell.Value = item == null ? "" : item.ToString();
					cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
					ColumnNo++;
				}
				RowNo++;
			}
			workSheet.Columns().AdjustToContents();
			return workSheet;
		}
		private static List<T> ToEntity<T>(this IXLWorksheet workSheet) where T : new()
		{
			var genericType = typeof(T);
			var columnList = GetColomnList(genericType);

			var data = new List<T>();
			var rowList = workSheet.Rows().ToList();

			for (int i = 1; i < rowList.Count; i++)
			{
				var row = rowList[i];
				var instance = new T();

				for (int x = 0; x < columnList.Count; x++)
				{
					var propertyName = columnList[x].PropertyName;
					var property = genericType.GetProperty(propertyName);
					var cell = row.Cell(x + 1);
					var obj = Convert.ChangeType(DataType.GetObjectType(property.PropertyType, cell),
												 DataType.GetPropType(property.PropertyType, cell));

					property.SetValue(instance, obj, null);
				}
				data.Add(instance);
			}
			return data;
		}
		private static IEnumerable<PropertyInfo> GetAllProperties(Type t)
		{
			while (t != typeof(object))
			{
				foreach (var prop in t.GetProperties())
					yield return prop;
				t = t.BaseType;
			}
		}
		public static List<ExcelHelperAttribute> GetColomnList(Type GenericType, bool All = false)
		{
			var props = GetAllProperties(GenericType)
				.Where(property => property.GetCustomAttribute<DataImportAttribute>() != null)
				.Select(property =>
				{
					return new ExcelHelperAttribute
					{
						Attribute = property.GetCustomAttribute<DataImportAttribute>(),
						PropertyName = property.Name,
						PropertyType = property.PropertyType
					};
				})
				.OrderBy(p => p.Attribute.GetOrder());

			if (All)
			{
				return props.ToList();
			}
			return props.Where(x => !x.Attribute.ForError).ToList();
		}
		public static void AddHeaders(this IXLWorksheet workSheet, string[] headers)
		{
			for (int i = 0; i < headers.Length; i++)
			{
				var cel = workSheet.Cell(1, i + 1);
				cel.Value = headers[i];
				cel.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
				cel.Style.Font.Bold = true;
			}

		}
		public static void AddReportHeaders(this IXLWorksheet workSheet, List<Columns> headers)
		{
			for (int i = 0; i < headers.Count; i++)
			{
				var cel = workSheet.Cell(1, i + 1);
				cel.Value = headers[i].Label;
				cel.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
				cel.Style.Font.Bold = true;
			}
		}
		public static MemoryStream ReportXlStream(this XLWorkbook workbook, IXLWorksheet worksheet)
		{
			var stream = new MemoryStream();
			workbook.SaveAs(stream);
			worksheet.Columns().AdjustToContents();
			workbook.SaveAs(stream);
			stream.Seek(0, SeekOrigin.Begin);
			return stream;
		}
	}

	public static class WebApiExtensions
	{
		public static FileStreamResult Deliver(this XLWorkbook workbook, string fileName)
		{
			var memoryStream = new MemoryStream();
			workbook.SaveAs(memoryStream);
			memoryStream.Seek(0, SeekOrigin.Begin);

			var a = new FileStreamResult(memoryStream, MimeTypesMap.GetMimeType(Path.GetExtension(fileName)))
			{
				FileDownloadName = fileName
			};
			return a;
		}
	}

	public static class DataType
	{
		public static Type GetPropType(Type type, IXLCell cell)
		{
			if (type.Name == "String") { return typeof(string); }
			if (type.Name == "Boolean") { return typeof(bool); }
			if (type.Name == "Decimal") { return typeof(decimal); }
			if (type.Name == "Int32") { return typeof(int); }
			if (type.Name == "Int16") { return typeof(short); }
			if (type.Name == "Byte") { return typeof(byte); }
			if (type.Name == "DateTime") { return typeof(DateTime); }
			if (type.Name == "Nullable`1")
			{
				if (cell.Value.IsText) { return typeof(string); }
				if (cell.Value.IsBoolean) { return typeof(bool); }
				if (cell.Value.IsDateTime) { return typeof(DateTime); }
				if (cell.Value.IsBlank) { return typeof(string); }
				if (cell.Value.IsNumber)
				{
					//Getting the Nullable exact datatype
					var nullType = type.FullName.Split("System.Nullable`1[[")[1].Split(",");
					if (nullType.Any())
					{
						var dataType = nullType[0];
						return dataType switch
						{
							"System.Int32" => typeof(int),
							"System.Int16" => typeof(short),
							"System.Byte" => typeof(byte),
							"System.Decimal" => typeof(decimal),
							"System.Boolean" => typeof(bool),
							"System.DateTime" => typeof(DateTime),
							"System.String" => typeof(string),
							_ => throw new ArgumentException("Invalid type"),
						};
					}
				}
			}
			return type;
		}
		public static object GetObjectValue(Type type, IXLCell cell)
		{

			if (cell.Value.IsText) { return cell.Value.ToString(); }
			if (cell.Value.IsBoolean) { return cell.Value.GetBoolean(); }
			if (cell.Value.IsNumber) { return cell.Value.GetNumber(); }
			if (cell.Value.IsDateTime) { return cell.Value.GetDateTime(); }

			if (type.Name == "Nullable`1")
			{
				var nullType = type.FullName.Split("System.Nullable`1[[")[1].Split(",");
				if (nullType.Any())
				{
					var dataType = nullType[0];
					var celVal = cell.Value.ToString().Trim();
					//Converting Nullable cell value to their exact DataType
					return dataType switch
					{
						"System.Int32" => int.Parse(celVal),
						"System.Int16" => short.Parse(celVal),
						"System.Byte" => byte.Parse(celVal),
						"System.Decimal" => decimal.Parse(celVal),
						"System.Boolean" => bool.Parse(celVal),
						"System.DateTime" => DateTime.Parse(celVal),
						"System.String" => celVal,
						_ => throw new ArgumentException("Invalid type"),
					};
				}
			}
			throw new ArgumentException("Invalid type");
		}
		public static object GetObjectType(Type type, IXLCell cell)
		{
			if (!cell.Value.IsBlank)
				return type.Name switch
				{
					"String" => cell.Value.ToString(),
					"Int32" => cell.Value.GetNumber(),
					"DateTime" => cell.Value.GetDateTime(),
					"Decimal" => cell.Value.GetNumber(),
					"Boolean" => cell.Value.GetBoolean(),
					"Byte" => cell.Value.GetNumber(),
					"Int16" => cell.Value.GetNumber(),
					"Nullable`1" => GetObjectValue(type, cell),
					_ => throw new ArgumentException("Invalid type"),
				};
			return type.Name switch
			{
				"String" => null,
				"Int32" => 0,
				"DateTime" => DateTime.MinValue,
				"Decimal" => 0,
				"Boolean" => false,
				"Nullable" => null,
				"Nullable`1" => null,
				"Byte" => 0,
				"Int16" => 0,
				_ => throw new ArgumentException("Invalid type"),
			};
		}
	}
}

