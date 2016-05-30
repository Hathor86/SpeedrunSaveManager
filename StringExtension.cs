using System.IO;

namespace SpeedrunSaveManager
{
    public static class StringExtension
    {
		/// <summary>
		/// Remove invalid character for path
		/// from specified <see cref="string"/>
		/// </summary>
		/// <param name="name"><see cref="string"/>to be cleaned</param>
		/// <returns>A path safe string</returns>
		public static string AsSafePathName(this string name)
		{
			foreach (char c in Path.GetInvalidPathChars())
			{
				name = name.Replace(c, '_');
			}
			return name;
		}
    }
}
