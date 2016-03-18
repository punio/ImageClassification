using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageClassification
{
	static class ImageFunctions
	{
		/// <summary>
		/// Bitmap.FromFileだと対象ファイルをロックしてしまうのでそのクローンを使うようにしています
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		public static Bitmap FromFileClone(string fileName)
		{
			var image = Bitmap.FromFile(fileName);
			var result = (Bitmap)image.Clone();
			image.Dispose();
			return result;
		}

		public static Bitmap FlipHorizontal(this Bitmap image)
		{
			var result = new Bitmap(image.Width, image.Height);
			var g = Graphics.FromImage(result);
			g.DrawImage(image, image.Width, 0, -image.Width, image.Height);
			g.Dispose();
			return result;
		}
	}
}
