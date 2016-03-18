using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using Accord.Imaging;
using Accord.MachineLearning;
using Accord.MachineLearning.VectorMachines;
using Accord.MachineLearning.VectorMachines.Learning;
using Accord.Statistics.Kernels;
using ImageClassification.Annotations;
using Reactive.Bindings;

namespace ImageClassification
{
	/// <summary>
	/// MainWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
			this.Loaded += MainWindow_Loaded;
			this.PreviewDragOver += MainWindow_PreviewDragOver;
			this.Drop += MainWindow_Drop;
			this.DataContext = this;    // ViewModel作るの面倒だったので・・・
		}

		private void MainWindow_Loaded(object sender, RoutedEventArgs e)
		{
			#region 学習結果のロード
			// ロードで前回の学習結果がそのまま使えると思っているんだけど・・・なんか違う気がする
			_machineFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, VectorMachineFile);
			_surfBowFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SurfBowFile);

			if (File.Exists(_machineFile))
			{
				_ksvm = MulticlassSupportVectorMachine.Load(_machineFile);
			}
			if (File.Exists(_surfBowFile))
			{
				_surfBow = BagOfVisualWords.Load(_surfBowFile);
			}
			#endregion
		}

		#region Drag & Drop で識別ファイルを受け付けよう
		private void MainWindow_PreviewDragOver(object sender, DragEventArgs e)
		{
			// ドロップ使用とするものがファイルの時のみ受け付ける。
			e.Effects = e.Data.GetData(DataFormats.FileDrop) != null ? DragDropEffects.Copy : DragDropEffects.None;
			e.Handled = true;
		}

		private void MainWindow_Drop(object sender, DragEventArgs e)
		{
			var files = e.Data.GetData(DataFormats.FileDrop) as string[];
			if ((files?.Length ?? 0) < 1) return;
			DropFileName.Value = files[0];
			DropResult.Value = $"{Compute(DropFileName.Value)} じゃね？";
		}
		#endregion

		private void Button_Click(object sender, RoutedEventArgs e) => Task.Run(() =>
		{
			try
			{
				#region 学習用ファイルの検索対象フォルダにあるフォルダをグループにします
				TrainDataList.Clear();
				TestDataList.Clear();
				TrainDataClass.Clear();
				var rand = new Random();
				var classCount = 0;
				foreach (var dir in Directory.GetDirectories(TrainDataPath))
				{
					TrainDataClass.Add(Path.GetFileName(dir));
					foreach (var file in Directory.EnumerateFiles(dir))
					{
						if (rand.NextDouble() > 0.9)
						{
							// 学習用ファイルの中から1割くらいをテスト用データとしておく
							TestDataList.Add(new ImageInformation { FileName = file, Class = classCount });
						}
						else
						{
							TrainDataList.Add(new ImageInformation { FileName = file, Class = classCount });
						}
					}
					classCount++;
				}
				#endregion

				this.ComputeBagOfWords();
				this.CreateVectorMachine();
				this.SelfTest();
			}
			catch (Exception exp)
			{
				Message.Value = $"Error. {exp.Message}";
			}
		});

		#region GUIに出すよう
		public ReactiveProperty<string> Message { get; } = new ReactiveProperty<string>();
		public ReactiveCollection<TestResult> TestResult { get; } = new ReactiveCollection<TestResult>();
		public ReactiveProperty<string> DropFileName { get; } = new ReactiveProperty<string>();
		public ReactiveProperty<string> DropResult { get; } = new ReactiveProperty<string>();
		#endregion

		const string TrainDataPath = @"F:\Images";
		private List<string> TrainDataClass { get; } = new List<string>();
		private List<ImageInformation> TrainDataList { get; } = new List<ImageInformation>();
		private List<ImageInformation> TestDataList { get; } = new List<ImageInformation>();

		#region Accord.NET
		const int NumberOfWords = 200;  // 画像から抽出する特徴点の数・・・だとおもう
		private MulticlassSupportVectorMachine _ksvm;
		private BagOfVisualWords _surfBow;
		private const string VectorMachineFile = "Ksvm.dat";
		private string _machineFile;
		private const string SurfBowFile = "SurfBow.dat";
		private string _surfBowFile;

		/// <summary>
		/// BagOfVisualWordsの作成？
		/// </summary>
		private void ComputeBagOfWords()
		{
			Message.Value = "ComputeBagOfWords";

			if (this._surfBow == null)
			{
				// BinarySplitっていうアルゴリズムを使って特徴点を抽出するみたいだよ
				var binarySplit = new BinarySplit(NumberOfWords);
				this._surfBow = new BagOfVisualWords(binarySplit);
			}

			// 学習用画像をリストにしておく
			var trainImages = TrainDataList.Select(t => ImageFunctions.FromFileClone(t.FileName)).ToArray();
			// 左右逆にした画像を入れておくと効果があるらしい（要検証）のでそれも準備しておく
			var trainRImages = TrainDataList.Select(t => ImageFunctions.FromFileClone(t.FileName).FlipHorizontal()).ToArray();

			// がんばってもらう
			Message.Value = "_surfBow.Compute";
			this._surfBow.Compute(trainImages.Concat(trainRImages).ToArray());
			// 結果を保存（・・・しているのか謎）
			_surfBow.Save(_surfBowFile);

			// 一応後始末
			foreach (var i in trainImages) i.Dispose();
			foreach (var i in trainRImages) i.Dispose();

			// 出来上がった抽出君でそれぞれの画像の特徴点を求めておく（これを学習させる）
			// 並列で読み込ませたかったけどGetFeatureVectorがスレッドセーフではなさそうなので頑張って1ファイルずつ求めます
			Message.Value = "Get Vectors";
			var counter = 0;
			TrainDataList.ForEach(t =>
			{
				var image = ImageFunctions.FromFileClone(t.FileName);
				t.VectorL = this._surfBow.GetFeatureVector(image);

				var rimage = image.FlipHorizontal();
				t.VectorR = this._surfBow.GetFeatureVector(rimage);

				image.Dispose();
				rimage.Dispose();

				counter++;
				if ((counter % 100) == 0) Message.Value = $"Get Vectors {counter * 100.0 / TrainDataList.Count}%";   // 進捗出さないと不安すぎる
			});
		}

		/// <summary>
		/// Vector Machineを作る？？
		/// </summary>
		private void CreateVectorMachine()
		{
			Message.Value = "CreateVectorMachine";
			var classes = TrainDataList.Select(t => t.Class).Distinct().Count();    // 分類数
			var kernel = GetKernel();   // 学習手法？

			if (_ksvm == null) _ksvm = new MulticlassSupportVectorMachine(0, kernel, classes);

			// 入力（抽出君が作ったVector）と出力（分類）を渡して学習してもらう（配列のIndexでみているっぽい）
			var input1 = TrainDataList.Select(t => t.VectorL);
			var input2 = TrainDataList.Select(t => t.VectorR);
			var output1 = TrainDataList.Select(t => (int)t.Class);
			var output2 = TrainDataList.Select(t => (int)t.Class);
			var teacher = new MulticlassSupportVectorLearning(_ksvm, input1.Concat(input2).ToArray(), output1.Concat(output2).ToArray());

			teacher.Algorithm = (svm, classInputs, classOutputs, i, j) => new SequentialMinimalOptimization(svm, classInputs, classOutputs)
			{
				UseComplexityHeuristic = true
			};

			// 学習開始
			Message.Value = "学習開始";
			teacher.Run();
			Message.Value = "学習終了";

			// 一応保存
			_ksvm.Save(_machineFile);
		}

		/// <summary>
		/// 学習手法？を返します
		/// これがキモっぽい
		/// </summary>
		/// <returns></returns>
		private IKernel GetKernel()
		{
			//return new Gaussian();
			//return new Linear();
			//return new Polynomial();
			return new ChiSquare();
			//return new HistogramIntersection(1, 1);
		}

		/// <summary>
		/// 学習用ファイルの一部(学習には使っていない)でテスト
		/// </summary>
		private void SelfTest()
		{
			Message.Value = "テスト中";
			var hit = 0;
			var count = 0;
			this.TestResult.ClearOnScheduler();
			TestDataList.ForEach(t =>
			{
				var data = new TestResult { FileName = t.FileName, 正解 = TrainDataClass[t.Class] };
				data.結果 = Compute(t.FileName);
				count++;
				if (data.正解 == data.結果) hit++;
				TestResult.AddOnScheduler(data);
			});

			Message.Value = $"{count}中 {hit}正解.  正解率 {hit * 100.0 / count}%";
		}
		#endregion

		/// <summary>
		/// 学習させた子を使ってファイルを分類してみる
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		private string Compute(string fileName)
		{
			var image = ImageFunctions.FromFileClone(fileName);
			var result = TrainDataClass[_ksvm.Compute(this._surfBow.GetFeatureVector(image))];
			image.Dispose();
			return result;
		}
	}
}
