namespace TranSmart.API.Services
{
	public interface IFileServerService
	{
		Task<bool> UploadFile(IFormFile ufile, string fileName, string mainFolder, string subFolder);
		Task<bool> UploadFile(MemoryStream ms, string fileName, string mainFolder, string subFolder);
		Task<MemoryStream> DownloadFile(Guid id, string mainFolder, string subFolder);
		Task<MemoryStream> DownloadFile(string mainFolder, string subFolder, string fileName);
		bool IsFileExists(string mainFolder, string subFolder, string fileName);
		string PhysicalPath(string mainFolder, string subFolder, string fileName);
	}
	public class FileServerService : IFileServerService
	{
		private readonly IConfiguration _configuration;
		private readonly IWebHostEnvironment _env;
		public FileServerService(IConfiguration configuration, IWebHostEnvironment env)
		{
			_configuration = configuration;
			_env = env;
		}
		public async Task<bool> UploadFile(IFormFile ufile, string fileName, string mainFolder, string subFolder)
		{
			if (ufile != null)
			{
				string folderPath = FilePath(mainFolder, subFolder);

				//Verify folder 
				if (!Directory.Exists(folderPath))
				{
					Directory.CreateDirectory(folderPath);
				}
				using (var fileStream = new FileStream(Path.Combine(folderPath, fileName), FileMode.Create))
				{
					await ufile.CopyToAsync(fileStream);
				}
				return true;
			}
			return false;
		}
		public async Task<bool> UploadFile(MemoryStream ms, string fileName, string mainFolder, string subFolder)
		{
			if (ms != null)
			{
				string folderPath = FilePath(mainFolder, subFolder);

				//Verify folder 
				if (!Directory.Exists(folderPath))
				{
					Directory.CreateDirectory(folderPath);
				}
				using (var fileStream = new FileStream(Path.Combine(folderPath, fileName), FileMode.Create))
				{
					ms.Position = 0;
					await ms.CopyToAsync(fileStream);
				}
				return true;
			}
			return false;
		}
		public async Task<MemoryStream> DownloadFile(Guid id, string mainFolder, string subFolder)
		{
			string folderPath = Path.Combine(FilePath(mainFolder, subFolder), id.ToString());

			try
			{
				var ms = new MemoryStream();
				var fileInfo = new FileInfo(folderPath);
				// check if exists
				if (fileInfo.Exists)
				{
					using var stream = new FileStream(folderPath, FileMode.Open, FileAccess.Read);
					await stream.CopyToAsync(ms);
					return ms;
				}
				return ms;
			}
			catch (Exception)
			{
				throw;
			}
		}
		public async Task<MemoryStream> DownloadFile(string mainFolder, string subFolder, string fileName)
		{
			string folderPath = Path.Combine(_env.ContentRootPath, mainFolder, subFolder, fileName);

			try
			{
				var ms = new MemoryStream();
				var fileInfo = new FileInfo(folderPath);
				// check if exists
				if (fileInfo.Exists)
				{
					using var stream = new FileStream(folderPath, FileMode.Open, FileAccess.Read);
					await stream.CopyToAsync(ms);
					return ms;
				}
				return ms;
			}
			catch (Exception)
			{
				throw;
			}
		}
		public bool IsFileExists(string mainFolder, string subFolder, string fileName)
		{
			string folderPath = Path.Combine(_env.ContentRootPath, mainFolder, subFolder, fileName);

			var fileInfo = new FileInfo(folderPath);
			// check if exists
			if (fileInfo.Exists)
			{
				return true;
			}
			return false;
		}
		public string PhysicalPath(string mainFolder, string subFolder, string fileName)
		{
			return Path.Combine(_env.ContentRootPath, mainFolder, subFolder, fileName);
		}
		private string FilePath(string mainFolder, string subFolder)
		{
			return Path.Combine(mainFolder, subFolder);
		}
	}
}
