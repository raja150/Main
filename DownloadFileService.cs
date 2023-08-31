namespace TranSmart.API.Services
{
	public static class DownloadFileService
	{
		public static async Task<bool> UploadFile(IFormFile ufile, string fileName, string folder, string path)
		{
			if (ufile != null)
			{
				string folderPath = Path.Combine(Directory.GetCurrentDirectory(), path, folder);

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
		public static async Task<bool> UploadFile(MemoryStream ms, string fileName, string folder, string path)
		{
			if (ms != null)
			{
				string folderPath = Path.Combine(Directory.GetCurrentDirectory(), path, folder);

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
		public static async Task<MemoryStream> DownloadFile(Guid id, string folder, string path)
		{
			string filePath = Path.Combine(Directory.GetCurrentDirectory(), path, folder, id.ToString());
			try
			{
				MemoryStream ms = await ReadFile(filePath);
				return ms;
			}
			catch (Exception)
			{
				throw;
			}
		}
		public static async Task<MemoryStream> ReadFile(string filePath)
		{
			var ms = new MemoryStream();
			var fileInfo = new FileInfo(filePath);
			try
			{
				// check if exists
				if (fileInfo.Exists)
				{
					using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
					await stream.CopyToAsync(ms);
					return ms;
				}
			}
			catch (Exception ex)
			{
				throw new IOException(ex.Message);
			}
			return null;
		}

		public static async Task<bool> FileImage(Stream ms, string path, string folder, string fileName)
		{
			if (ms != null)
			{
				string fullpath = Path.Combine(Directory.GetCurrentDirectory(), path, folder, fileName);
				using (var fileStream = new FileStream(fullpath, FileMode.Create))
				{
					ms.Position = 0;
					await ms.CopyToAsync(fileStream);
				}
				return true;
			}
			return false;
		}

	}
}
