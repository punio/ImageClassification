using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageClassification
{
	public class ImageInformation
	{
		// 画像ファイル名
		public string FileName { get; set; }
		// その画像の特徴
		public double[] VectorL { get; set; }
		public double[] VectorR { get; set; }
		// 分類
		public ImageClass Class { get; set; }
	}
}
