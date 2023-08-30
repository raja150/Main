using Microsoft.AspNetCore.Mvc;
using System.Drawing;
using System.Drawing.Imaging;
using TranSmart.API.Extensions;
using TranSmart.API.Services;
using TranSmart.Core.Result;
using TranSmart.Domain.Entities;
using TranSmart.Domain.Models;
using TranSmart.Service;

namespace TranSmart.API.Controllers
{
	[Route("api/[controller]")]
	[ApiController]

	public class ImageController : BaseController
	{
		private readonly IMapper _mapper;
		private readonly IImageService _service;
		private string rootPath = "images";
		private readonly string originalFolder = "original";
		private readonly string resizeFolder = "avatars";
		private readonly IFileServerService _fileServerService;
		private readonly IWebHostEnvironment _env;

		public ImageController(IMapper mapper, IImageService service, IFileServerService fileServerService, IWebHostEnvironment env)
		{
			_mapper = mapper;
			_service = service;
			_fileServerService = fileServerService;
			_env = env;
			rootPath = Path.Combine(env.ContentRootPath ,"images");
		}

		[HttpPost]
		public async Task<IActionResult> AddandUpdateImages([FromForm] ImageModel model)
		{
			var result = new Result<EmpImage>();
			if (model.File == null)
			{
				result.AddMessageItem(new MessageItem("Only .jpg file is acceptable "));
				return BadRequest(result);
			}
			EmpImage entity = _mapper.Map<EmpImage>(model);

			//copy upload file content into memory stream
			var image = Image.FromStream(model.File.OpenReadStream());
			MemoryStream ms = new();
			model.File.CopyTo(ms);

			//Copy original image byte array into entity
			entity.ImageData = ms.ToArray();

			//Resize uploaded image
			var resized = new Bitmap(image, new Size(64, 64));
			using MemoryStream imageStream = new();
			resized.Save(imageStream, ImageFormat.Jpeg);

			//Copy resized image byte array into entity
			entity.ResizeImageData = imageStream.ToArray();//convert into byte array

			//Add entity
			result = await _service.AddandUpdate(entity);
			if (result.HasError) return BadRequest(result);

			//Copy original image into folders
			await _fileServerService.UploadFile(model.File, string.Concat(result.ReturnValue.EmployeeId, ".jpg"), rootPath, originalFolder);
			//Copy resized image into folders
			await _fileServerService.UploadFile(imageStream, string.Concat(result.ReturnValue.EmployeeId, ".jpg"), rootPath, resizeFolder);

			return Ok(_mapper.Map<EmpImage>(result.ReturnValue));
		}

		[HttpGet]
		[ApiAuthorize(Core.Permission._Role, Core.Privilege.Update)]
		public async Task<IActionResult> AddImgToLocalPath()
		{
			var img = await _service.GetImg();
			EmpImage entity = new();
			foreach (var item in img)
			{
				var ms = new MemoryStream(item.ImageData);

				var image = Image.FromStream(ms);
				var resized = new Bitmap(image, new Size(64, 64));
				using MemoryStream imageStream = new();
				resized.Save(imageStream, ImageFormat.Jpeg);

				//convert into byte array
				entity.ResizeImageData = imageStream.ToArray();
				//Copy Original image in folder with ".jpg" extension
				await _fileServerService.UploadFile(ms, string.Concat(item.EmployeeId.ToString(), ".jpg"), rootPath, originalFolder);
				//Copy Resized image in folder with ".jpg" extension
				await _fileServerService.UploadFile(imageStream, string.Concat(item.EmployeeId.ToString(), ".jpg"), rootPath, resizeFolder);
			}
			return Ok();
		}
	}
}
