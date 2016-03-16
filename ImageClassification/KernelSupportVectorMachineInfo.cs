using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Accord.MachineLearning.VectorMachines;

namespace ImageClassification
{
	/// <summary>
	/// 学習結果の表示用
	/// </summary>
	public class KernelSupportVectorMachineInfo
	{
		public KernelSupportVectorMachine Machine { get; set; }
		public ImageClass Class1 { get; set; }
		public ImageClass Class2 { get; set; }

		public override string ToString() => $"{this.Class1} vs {this.Class2} ({this.Machine.Threshold})";
	}
}
