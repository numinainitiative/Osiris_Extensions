using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Playnite.Native;

namespace Playnite.Common;

public class Resources
{
	public static void ExtractResource(string path, string name, string type, string destination)
	{
		IntPtr hModule = Kernel32.LoadLibraryEx(path, IntPtr.Zero, 2u);
		IntPtr hResInfo = Kernel32.FindResource(hModule, name, type);
		uint num = Kernel32.SizeofResource(hModule, hResInfo);
		IntPtr source = Kernel32.LoadResource(hModule, hResInfo);
		byte[] array = new byte[num];
		Marshal.Copy(source, array, 0, (int)num);
		using MemoryStream memoryStream = new MemoryStream(array);
		File.WriteAllBytes(destination, memoryStream.ToArray());
	}

	public static long GetUriPackFileSize(string packUri)
	{
		using Stream stream = Application.GetResourceStream(new Uri(packUri)).Stream;
		return stream.Length;
	}

	public static string ReadFileFromResource(string resource)
	{
		using Stream stream = Assembly.GetCallingAssembly().GetManifestResourceStream(resource);
		return new StreamReader(stream).ReadToEnd();
	}

	public static string GetIndirectResourceString(string fullName, string packageName, string resource)
	{
		Uri uri = new Uri(resource);
		string empty = string.Empty;
		empty = (resource.StartsWith("ms-resource://") ? ("@{" + fullName + "? " + resource + "}") : ((!resource.Contains('/')) ? ("@{" + fullName + "? ms-resource://" + packageName + "/resources/" + uri.Segments.Last() + "}") : ("@{" + fullName + "? ms-resource://" + packageName + "/" + resource.Replace("ms-resource:", "").Trim('/') + "}")));
		StringBuilder stringBuilder = new StringBuilder(1024);
		if (Shlwapi.SHLoadIndirectString(empty, stringBuilder, stringBuilder.Capacity, IntPtr.Zero) == 0)
		{
			return stringBuilder.ToString();
		}
		empty = "@{" + fullName + "? ms-resource://" + packageName + "/" + uri.Segments.Last() + "}";
		if (Shlwapi.SHLoadIndirectString(empty, stringBuilder, stringBuilder.Capacity, IntPtr.Zero) == 0)
		{
			return stringBuilder.ToString();
		}
		return string.Empty;
	}
}
