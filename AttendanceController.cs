using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;

using Microsoft.AspNetCore.Mvc;

using TranSmart.Core.Result;
using TranSmart.Domain.Models;
using TranSmart.Domain.Models.LM_Attendance.List;
using TranSmart.Domain.Models.LM_Attendance.Response;
using TranSmart.Service.Leave;
using TranSmart.API.Models;
using TranSmart.Domain.Entities.Organization;
using TranSmart.Service.Organization;
using TranSmart.Service.Schedules;
using TranSmart.Domain.Models.Schedules;
using TranSmart.Domain.Entities.Leave;
using TranSmart.Data;
using TranSmart.API.Models.Import;
using TranSmart.API.Extensions;
using Transmart.TS4API;
using TranSmart.Domain.Models.LM_Attendance;
using System.IO;
using ClosedXML.Excel;
using TranSmart.API.Services;
using TranSmart.Core.Util;

namespace TranSmart.API.Controllers.Attendance
{
	[Route("api/LM_Attendance/[controller]")]
	[ApiController]
	public class AttendanceController : BaseController
	{
		private readonly IMapper _mapper;
		private readonly IAttendanceService _service;
		private readonly IEmployeeService _EmpService;
		private readonly ITs4ApiS _apis;
		public AttendanceController(IMapper mapper, IAttendanceService service, IEmployeeService EmpService, ITs4ApiS apis)
		{
			_mapper = mapper;
			_service = service;
			_EmpService = EmpService;
			_apis = apis;
		}

		#region Get
		[HttpGet("Paginate")]
		[ApiAuthorize(Core.Permission.LM_Attendance, Core.Privilege.Read)]
		public async Task<IActionResult> Paginate([FromQuery] BaseSearch baseSearch)
		{
			return Ok(_mapper.Map<Models.Paginate<AttendanceList>>(await _service.GetPaginate(baseSearch)));
		}

		[HttpGet("GetAttendanceData")]
		[ApiAuthorize(Core.Permission.LM_Attendance, Core.Privilege.Read)]
		public async Task<IActionResult> GetAttendanceData([FromQuery] AttendanceSearch baseSeach)
		{
			return Ok(
				_mapper.Map<Models.Paginate<EmployeeAttendance>>(
					await _service.GetAttendanceData(baseSeach)));
		}

		[HttpGet("{id}")]
		[ApiAuthorize(Core.Permission.LM_Attendance, Core.Privilege.Read)]
		public async Task<AttendanceModel> Get(Guid id)
		{
			return _mapper.Map<AttendanceModel>(await _service.GetById(id));
		}

		[HttpGet("GetDetails")]
		[ApiAuthorize(Core.Permission.LM_Attendance, Core.Privilege.Read)]
		public async Task<TranSmart.Domain.Entities.Leave.Attendance> GetDetails(Guid EmpID, DateTime AttDate)
		{
			return await _service.GetAttendanceReport(EmpID, AttDate);
		}

		[HttpGet("AttendanceDashboard")]
		public async Task<AttendanceModel> GetDate(Guid id)
		{
			return _mapper.Map<AttendanceModel>(await _service.GetDate(id));
		}
		[HttpGet("Finalized/{date}")]
		public async Task<ActionResult> Finalized(DateTime date)
		{
			Result<AttendanceSum> result = await _service.Finalized((byte)date.Month, (short)date.Year);
			if (!result.HasError)
			{
				return Ok(result);
			}
			return BadRequest(result);
		}
		[HttpGet("Download/{date}")]
		public async Task<IActionResult> DownloadAttandeance(DateTime date)
		{

			var ms = new MemoryStream();
			var workbook = new XLWorkbook();
			var startDate = new DateTime(date.Year, date.Month, 1);

			var empList = await _EmpService.GetEmps(startDate);
			var org = await _service.GetOrganizations();
			DateTime from, upto;

			from = DateUtil.FromDate(date.Month, date.Year, org.MonthStartDay);
			upto = DateUtil.ToDate(date.Month, date.Year, org.MonthStartDay);

			var workSheet = workbook.Worksheets.Add("Attendance");
			int totalDays = 0;

			//Headers
			string[] headers = { "Employee Code", "Employee Name", "Designation", "Department", "DOJ" };

			ClosedXmlGeneric.AddHeaders(workSheet, headers);
			int count = 6;

			for (DateTime day = from; day <= upto; day = day.AddDays(1))
			{
				totalDays++;
				var cell = workSheet.Cell(1, count++);
				cell.Value = day;
				cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
				cell.Style.Font.Bold = true;
			}
			string[] Attheaders = { "Total Days", "Present", "Absent", "LOP", "Leave", "Off Time", "WFH", "UN-Authorized" };
			foreach (string header in Attheaders)
			{
				var cell = workSheet.Cell(1, count++);
				cell.Value = header;
				cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
				cell.Style.Font.Bold = true;
			}

			var attendanceData = await _service.GetAttendance(from, upto);
			int sheetRowNo = 2;
			foreach (var item in empList)
			{
				workSheet.Cell(sheetRowNo, 1).Value = item.No;
				workSheet.Cell(sheetRowNo, 2).Value = item.Name;
				workSheet.Cell(sheetRowNo, 3).Value = item.Designation.Name;
				workSheet.Cell(sheetRowNo, 4).Value = item.Department.Name;
				workSheet.Cell(sheetRowNo, 5).Value = item.DateOfJoining;
				int headerColumns = headers.Length + 1;

				for (var day = from; day <= upto; day = day.AddDays(1))
				{
					var attendance = attendanceData.FirstOrDefault(x => x.EmployeeId == item.ID && x.AttendanceDate.Date == day.Date);
					var cell = workSheet.Cell(sheetRowNo, headerColumns++);

					if (attendance != null)
					{

						if (attendance.IsHalfDay.HasValue && attendance.IsHalfDay.Value)
						{

							cell.Value = string.Format("{0}\r\n{1}",
											attendance.AttendanceStatus == (int)AttendanceStatus.HalfDayLeave ?
												 "HalfDay-" + attendance.LeaveType.Code :
												Enum.GetName(typeof(AttendanceStatus), attendance.AttendanceStatus),
											attendance.HalfDayType == (int)AttendanceStatus.HalfDayLeave ?
												 "HalfDay-" + attendance.LeaveType.Code : Enum.GetName(typeof(AttendanceStatus), attendance.HalfDayType));
						}
						else
						{
							if (attendance.AttendanceStatus == (int)AttendanceStatus.Leave)
							{
								cell.Value = attendance.LeaveType.Code;
							}
							else
							{
								cell.Value = Enum.GetName(typeof(AttendanceStatus), attendance.AttendanceStatus);
							}
						}
					}
					else
					{
						cell.Value = "N/A";
					}
				}

				for (int i = 0; i < Attheaders.Length; i++)
				{
					var cell = workSheet.Cell(sheetRowNo, headerColumns++);
					switch (i)
					{
						//"Total Days"
						case 0:
							cell.Value = attendanceData.Where(x => x.EmployeeId == item.ID).Count();
							break;
						//"Present"
						case 1:
							cell.Value = attendanceData.Where(x => x.EmployeeId == item.ID)
								.Sum(s => (s.AttendanceStatus is ((int)AttendanceStatus.Present)
											or ((int)AttendanceStatus.WeekOff)
											or ((int)AttendanceStatus.Holiday)
											or ((int)AttendanceStatus.Late)) ? 1 : 0)
								+ attendanceData.Where(x => x.EmployeeId == item.ID)
								.Sum(s => (s.AttendanceStatus == (int)AttendanceStatus.HalfDayPresent) ? 0.5 : 0)
								+ attendanceData.Where(x => x.EmployeeId == item.ID)
								.Sum(s => (s.HalfDayType == (int)AttendanceStatus.HalfDayPresent) ? 0.5 : 0);
							break;
						//"Absent"
						case 2:
							cell.Value = attendanceData.Where(x => x.EmployeeId == item.ID)
								.Sum(s => (s.AttendanceStatus is ((int)AttendanceStatus.Absent)
											or ((int)AttendanceStatus.MaternityLeave)
											or ((int)AttendanceStatus.LongLeave)
											or ((int)AttendanceStatus.Unautherized)) ? 1 : 0)
								+ attendanceData.Where(x => x.EmployeeId == item.ID)
								.Sum(s => (s.AttendanceStatus == (int)AttendanceStatus.HalfDayAbsent) ? 0.5 : 0)
								+ attendanceData.Where(x => x.EmployeeId == item.ID)
								.Sum(s => s.HalfDayType == (int)AttendanceStatus.HalfDayAbsent ? 0.5 : 0);
							break;
						//"LOP"
						case 3:
							cell.Value = attendanceData.Where(x => x.EmployeeId == item.ID)
								.Sum(s => s.AttendanceStatus == (int)AttendanceStatus.Leave && s.LeaveType.PayType == 0 ? 1 : 0)
								+ attendanceData.Where(x => x.EmployeeId == item.ID)
								.Sum(s => s.AttendanceStatus == (int)AttendanceStatus.HalfDayLeave && s.LeaveType.PayType == 0 ? 0.5 : 0)
								+ attendanceData.Where(x => x.EmployeeId == item.ID)
								.Sum(s => s.HalfDayType == (int)AttendanceStatus.HalfDayLeave && s.LeaveType.PayType == 0 ? 0.5 : 0);
							break;
						//"Leave"
						case 4:
							cell.Value = attendanceData.Where(x => x.EmployeeId == item.ID)
								.Sum(s => s.AttendanceStatus == (int)AttendanceStatus.Leave && s.LeaveType.PayType == 1 ? 1 : 0)
								+ attendanceData.Where(x => x.EmployeeId == item.ID)
								.Sum(s => s.AttendanceStatus == (int)AttendanceStatus.HalfDayLeave && s.LeaveType.PayType == 1 ? 0.5 : 0)
								+ attendanceData.Where(x => x.EmployeeId == item.ID)
								.Sum(s => s.HalfDayType == (int)AttendanceStatus.HalfDayLeave && s.LeaveType.PayType == 1 ? 0.5 : 0);
							break;
						//"Off Time"
						case 5:
							cell.Value = attendanceData.Where(x => x.EmployeeId == item.ID)
								.Sum(s => s.AttendanceStatus == (int)AttendanceStatus.Leave && s.LeaveType.PayType == 2 ? 1 : 0)
								+ attendanceData.Where(x => x.EmployeeId == item.ID)
								.Sum(s => s.AttendanceStatus == (int)AttendanceStatus.HalfDayLeave && s.LeaveType.PayType == 2 ? 0.5 : 0)
								+ attendanceData.Where(x => x.EmployeeId == item.ID)
								.Sum(s => s.HalfDayType == (int)AttendanceStatus.HalfDayLeave && s.LeaveType.PayType == 2 ? 0.5 : 0);
							break;
						//"WFH"
						case 6:
							cell.Value = attendanceData.Where(x => x.EmployeeId == item.ID)
								.Sum(s => s.AttendanceStatus == (int)AttendanceStatus.WFH ? 1 : 0)
								+ attendanceData.Where(x => x.EmployeeId == item.ID)
								.Sum(s => (s.AttendanceStatus == (int)AttendanceStatus.HalfDayWFH) ? 0.5 : 0)
								+ attendanceData.Where(x => x.EmployeeId == item.ID)
								.Sum(s => s.HalfDayType == (int)AttendanceStatus.HalfDayWFH ? 0.5 : 0);
							break;
						//"UN-Authorized"
						case 7:
							cell.Value = attendanceData.Where(x => x.EmployeeId == item.ID && x.UADays.HasValue).Sum(s => s.UADays.Value);
							break;
					}
				}
				sheetRowNo++;
			}
			workSheet.Columns().AdjustToContents();
			workbook.SaveAs(ms);
			ms.Seek(0, SeekOrigin.Begin);
			return Ok(ms);
		}

		[HttpGet("IsPunchIn")]
		public async Task<IActionResult> GetPunchIn()
		{
			var item = await _service.GetPunchIn(LOGIN_USER_EMPId);
			if (item != null)
			{
				return Ok(_mapper.Map<AttendanceModel>(item));
			}
			return NoContent();
		}

		[HttpGet("IsPunchedEmployee")]
		public async Task<IActionResult> IsPunchedEmployee()
		{
			var result = await _service.IsPunchEmployee(LOGIN_USER_EMPId);
			return Ok(result);
		}

		[HttpGet("Movement")]
		public async Task<IActionResult> GetMovementDetails([FromQuery] DateTime date, [FromQuery] byte loginType, [FromQuery] Guid employeeId)
		{
			if (loginType == (byte)LoginType.Biometric)
			{
				var bioData = await _service.GetBiometricMovement(date, employeeId);
				return Ok(bioData);
			}

			var manualData = await _service.GetManualAttendanceMovement(date, employeeId);
			return Ok(manualData);
		}

		[HttpGet("EmployeeSummary")]
		[ApiAuthorize(Core.Permission.SS_Attendance, Core.Privilege.Read)]
		public async Task<IActionResult> EmployeeSummary([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
		{
			return Ok(await _service.EmployeeSummary(LOGIN_USER_EMPId, fromDate, toDate));
		}
		[HttpGet("TeamSummary")]
		public async Task<IActionResult> TeamSummary([FromQuery] Guid? employeeId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
		{
			return Ok(await _service.TeamSummary(employeeId, fromDate, toDate, LOGIN_USER_EMPId));
		}
		#endregion

		#region LogsImport

		[HttpPost("ImportCDAttLogs")]
		public async Task<ActionResult> ImportCodingLogs()
		{
			var res = await _apis.GetCodingAttLogs(DateTime.Now);
			List<ManualAttLogs> _list = _mapper.Map<List<ManualAttLogs>>(res);
			return Ok(await _service.ManualLogsImport(_list));
		}

		[HttpPost("ImportMTAttLogs")]
		public async Task<ActionResult> ImportTranscriptionLogs()
		{
			var res = await _apis.GetTranscriptionAttLogs(DateTime.Now);
			List<ManualAttLogs> _list = _mapper.Map<List<ManualAttLogs>>(res);
			return Ok(await _service.ManualLogsImport(_list));
		}
		[HttpPost("ImportMTANDCodingLogs")]
		public async Task<ActionResult> ImportTranscriptionLogs(List<ManualAttLogs> list)
		{
			return Ok(await _service.ManualLogsImport(list));
		}
		[HttpPost("ImportBiometricLogs")]
		public async Task<ActionResult> ImportBiometricLogs(List<BiometricAttLogsRequest> list)
		{
			List<BiometricAttLogs> _list = _mapper.Map<List<BiometricAttLogs>>(list);
			return Ok(await _service.BiometricLogsImport(_list));
		}


		#endregion
		// Daily Event For Attendance Calculation
		[HttpPut("RunAttendance")]
		public async Task<ActionResult> RunAttendance(DateTime AttDate)
		{
			return Ok(await _service.CalculateAttendance(AttDate));
		}

		// Manual update from client APP
		[HttpPut("UpdateAttendance")]
		[ApiAuthorize(Core.Permission.LM_Attendance, Core.Privilege.Update)]
		public async Task<IActionResult> UpdateAttendance(AttendanceDetails item)
		{
			var list = new List<AttendanceDetails>
			{
				item
			};
			Result<TranSmart.Domain.Entities.Leave.Attendance> result = await _service.AttendanceUpdate(list, LOGIN_USER_EMPId);
			if (!result.HasError)
			{
				return Ok(result.ReturnValue);
			}
			return BadRequest(result);
		}

		[HttpPost("Time")]
		public async Task<IActionResult> PostTimings(AttendanceModel model)
		{
			model.EmployeeId = LOGIN_USER_EMPId;
			model.AttendanceDate = DateTime.Now;
			model.InTime = DateTime.Now;
			model.WorkTime = 0;
			model.OutTime = null;

			Result<TranSmart.Domain.Entities.Leave.Attendance> result = await _service.AddNewTimings(_mapper.Map<TranSmart.Domain.Entities.Leave.Attendance>(model));
			if (result.IsSuccess)
			{
				return Ok(_mapper.Map<AttendanceModel>(result.ReturnValue));
			}
			else
			{
				return BadRequest(result);
			}
		}

		[HttpPut("Time")]
		public async Task<IActionResult> PutTimings()
		{
			var employeeId = LOGIN_USER_EMPId;
			Result<TranSmart.Domain.Entities.Leave.Attendance> result = await _service.UpdateTimings(employeeId);
			if (result.IsSuccess)
			{
				return Ok(_mapper.Map<AttendanceModel>(result.ReturnValue));
			}
			else
			{
				return BadRequest(result);
			}
		}

		[HttpPut("RePunchIn")]
		public async Task<IActionResult> RePunchIn()
		{
			var empId = LOGIN_USER_EMPId;
			Result<TranSmart.Domain.Entities.Leave.Attendance> result = await _service.RePunchIn(empId);
			if (result.IsSuccess)
			{
				return Ok(_mapper.Map<AttendanceModel>(result.ReturnValue));
			}
			else
			{
				return BadRequest(result);
			}
		}

		[HttpPost]
		[ApiAuthorize(Core.Permission.LM_Attendance, Core.Privilege.Create)]
		public async Task<IActionResult> Post(AttendanceModel model)

		{
			TranSmart.Domain.Entities.Leave.Attendance entity = _mapper.Map<TranSmart.Domain.Entities.Leave.Attendance>(model);
			entity.EmployeeId = LOGIN_USER_EMPId;
			entity.InTime = DateTime.Now;
			Result<TranSmart.Domain.Entities.Leave.Attendance> result = await _service.AddAsync(_mapper.Map<TranSmart.Domain.Entities.Leave.Attendance>(entity));
			if (result.IsSuccess)
			{
				return Ok(_mapper.Map<AttendanceModel>(result.ReturnValue));

			}
			else
			{
				return BadRequest(result);
			}
		}

		[HttpPut]
		[ApiAuthorize(Core.Permission.LM_Attendance, Core.Privilege.Update)]
		public async Task<IActionResult> Put(AttendanceModel model)
		{
			Result<TranSmart.Domain.Entities.Leave.Attendance> result = await _service.UpdateAsync(_mapper.Map<TranSmart.Domain.Entities.Leave.Attendance>(model));
			if (result.IsSuccess)
			{
				return Ok(_mapper.Map<AttendanceModel>(result.ReturnValue));

			}
			else
			{
				return BadRequest(result);
			}
		}
	}
}
