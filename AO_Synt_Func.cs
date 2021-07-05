using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AO_Lib
{
	public class AO_Cell
	{
		private double _GammaInDeg;
		private double _GammaOutDeg;
		private double _CellSizeMM;
		private double _PiezoLengthMM;
		private double _PiezoWidthMM;
		private double _PiezoOffsetMM;
		private double _CutAngleDeg;

		public double GammaInDeg
		{
			get => _GammaInDeg;
			set
			{
				if (value < 0 || value > 180) throw new ArgumentOutOfRangeException("Невозможно установить значение угла наклона входной грани равным " + value.ToString() +
						". Пожалуйста, введите значение от 0 до 180.");
				else _GammaInDeg = value;

			}
		}
		public double GammaOutDeg
		{
			get => _GammaOutDeg;
			set
			{
				if (value < 0 || value > 180) throw new ArgumentOutOfRangeException("Невозможно установить значение угла наклона входной грани равным " + value.ToString() +
						". Пожалуйста, введите значение от 0 до 180.");
				else _GammaOutDeg = value;

			}
		}
		public double CellSizeMM {
			get => _CellSizeMM;
			set
			{
				if (value < 0) throw new ArgumentOutOfRangeException("Невозможно установить значение размера акустической грани равным " + value.ToString() +
						". Пожалуйста, введите значение больше 0.");
				else _CellSizeMM = value;

			}
		}
		public double PiezoLengthMM
		{
			get => _PiezoLengthMM;
			set
			{
				if (value < 0) throw new ArgumentOutOfRangeException("Невозможно установить значение длины пьезоэлемента равным " + value.ToString() +
						". Пожалуйста, введите значение больше 0.");
				else _PiezoLengthMM = value;

			}
		}
		public double PiezoWidthMM
		{
			get => _PiezoWidthMM;
			set
			{
				if (value < 0) throw new ArgumentOutOfRangeException("Невозможно установить значение ширины пьезоэлемента равным " + value.ToString() +
						". Пожалуйста, введите значение больше 0.");
				else _PiezoWidthMM = value;

			}
		}
		public double PiezoOffsetMM
		{
			get => _PiezoOffsetMM;
			set
			{
				if (value < 0) throw new ArgumentOutOfRangeException("Невозможно установить значение отступа пьезоэлемента от края равным " + value.ToString() +
						". Пожалуйста, введите значение больше 0.");
				else _PiezoOffsetMM = value;

			}
		}
		public double CutAngleDeg
		{
			get => _CutAngleDeg;
			set
			{
				if (value < 0 || value > 180) throw new ArgumentOutOfRangeException("Невозможно установить значение угла среза равным " + value.ToString() +
						". Пожалуйста, введите значение от 0 до 180.");
				else _CutAngleDeg = value;

			}
		}

		/// <summary>
		/// Инициализация экземляра описания акустооптической ячейки
		/// </summary>
		/// <param name="GammaInDeg"> Угол наклона входной грани </param>
		/// <param name="GammaOutDeg"> Угол наклона выходной грани </param>
		/// <param name="CellSizeMM"> Размер акустической грани </param>
		/// <param name="PiezoLengthMM"> Длина пьезоэлемента </param>
		/// <param name="PiezoWidthMM"> Ширина пьезоэлемента </param>
		/// <param name="PiezoOffsetMM"> Отступ пьезоэлемента от края </param>
		/// <param name="CutAngleDeg"> Угол среза </param>
		public AO_Cell(double GammaInDeg, double GammaOutDeg, double CellSizeMM, double PiezoLengthMM, double PiezoWidthMM, double PiezoOffsetMM, double CutAngleDeg)
		{
			this.GammaInDeg = GammaInDeg;
			this.GammaOutDeg = GammaOutDeg;
			this.CellSizeMM = CellSizeMM;
			this.PiezoLengthMM = PiezoLengthMM;
			this.PiezoWidthMM = PiezoWidthMM;
			this.PiezoOffsetMM = PiezoOffsetMM;
			this.CutAngleDeg = CutAngleDeg;
		}

		public CellData ToCellDataStruct()
		{
			CellData CellData_result;
			CellData_result.GammaInDeg = this.GammaInDeg;
			CellData_result.GammaOutDeg = this.GammaOutDeg;
			CellData_result.CellSizeMM = this.CellSizeMM;
			CellData_result.PiezoLengthMM = this.PiezoLengthMM;
			CellData_result.PiezoWidthMM = this.PiezoWidthMM;
			CellData_result.PiezoOffsetMM = this.PiezoOffsetMM;
			CellData_result.CutAngleDeg = this.CutAngleDeg;
			return CellData_result;
		}

		/// <summary>
		/// Функция-обертка для решения обратной задачи: расчета массива частот для синтеза аппаратной функции определенной формы
		/// </summary>
		/// <param name="WLs">Массив длин волн [мкм] </param>
		/// <param name="Magnitudes">Массив амплитуд, соответственно предыдущему. Предполагается нормированным на 1, т.е. максимальное значение в массиве - 1.</param>
		/// <param name="precision">Количество частот в выходном массиве, т.е. точность, с которой будет воспроизведена аппаратная функция</param>
		/// <param name="period">Временная частота повтора сигнала [cек] </param>
		public unsafe FreqMass CalculateFreqs_via_AP(double[] WLs, double[] Magnitudes, int precision, double period)
        {
			//Количество ДВ должно быть равным кол-ву амплитуд
			if (WLs.Length != Magnitudes.Length) throw new Exception("Количество длин волн не соответствует количеству амлитуд");

			FreqMass returnable_Frequency_data;
			WLMass WL_data = new WLMass(WLs, Magnitudes);

			var rf_table = FreqMass.Create_EmptyRFStruct(period, precision);
			var wl_table = WL_data.ToSpectrumDataStruct();
			var cell_table = this.ToCellDataStruct();

			//и в функцию его
			int err = findsignal(&cell_table, &rf_table, &wl_table);
			returnable_Frequency_data = new FreqMass(rf_table);

			return returnable_Frequency_data;
        }
		/// <summary>
		/// Функция-обертка для решения прямой задачи: расчет формы аппаратной функции по массиву частот.<br/>
		/// Примечание: считается, что УЗ частоты подаются с одинаковой временной частотой, равной частному от количества частот, деленных на период повторения сигнала period<br/>
		/// Примечание 2: значения по умолчанию выставлены таким образом, что аппаратная функция будет построена в интервале 0.4 - 0.7 мкм с шагом в 0.001 мкм.
		/// </summary>
		/// <param name="Freqs">Массив частот для расчета аппаратной функции [МГц] </param>
		/// <param name="Magnitudes">Массив амплитуд, соответственно предыдущему. Предполагается нормированным на 1, т.е. максимальное значение в массиве - 1.</param>
		/// <param name="period">Временная частота повтора сигнала [cек] </param>
		/// <param name="RFPower">Максимальная мощность сигнала [Вт]. </param>
		/// <param name="WL_Start">Стартовая ДВ, начиная с которой будет вычисляться аппаратная функция [мкм]. Значение по умолчанию = 0.4 мкм. </param>
		/// <param name="WL_count">Количество ДВ, для которых будет построена аппаратная функция. Значение по умолчанию = 301. </param>
		/// <param name="WL_step">Шаг по ДВ. С такой точностью по спектру будет построена аппаратная функция [мкм]. Значение по умолчанию = 0.001 мкм. </param>
		public unsafe WLMass CalculateAP_via_Freqs(double[] Freqs, double[] Magnitudes, double period, double RFPower,  double WL_Start = 0.400, int WL_count = 301, double WL_step = 0.001)
        {
			//Количество частот должно быть равным кол-ву амплитуд
			if (Freqs.Length != Magnitudes.Length) throw new Exception("Количество длин волн не соответствует количеству амлитуд");
			WLMass returnable_WL_data;
			FreqMass Freq_data = new FreqMass(Freqs, Magnitudes, period, RFPower);
			var rf_table = Freq_data.ToRFDataStruct();
			var wl_table = WLMass.Create_EmptyWLStruct(WL_Start, WL_count, WL_step, 1);
			var cell_table = this.ToCellDataStruct();

			int err = findspectrum(&cell_table, &rf_table, &wl_table);
			returnable_WL_data = new WLMass(wl_table,true);

			return returnable_WL_data;
		}

		#region LIB FUNCTIONS
		[DllImport("specsynth.dll", CallingConvention = CallingConvention.Cdecl)]
		public static unsafe extern int findsignal(CellData* CellTable, RF_Data* RFTable, SPECTRUM_Data* SpectrumTable);
		[DllImport("specsynth.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static unsafe extern int findsweep(CellData* CellTable, RF_Data* RFTable, SPECTRUM_Data* SpectrumTable);
		[DllImport("specsynth.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
		public static unsafe extern int findspectrum(CellData* CellTable, RF_Data* RFTable, SPECTRUM_Data* SpectrumTable);
		#endregion
		#region MATH FUNCTIONS

		/// <summary>
		/// Вспомогательная функция. Определение количества знаков после запятой у decimal числа. 
		/// </summary>
		/// <param name="n">Число, количество знаков после запятой у которого нужно определить</param>
		/// <returns></returns>
		public static int GetDecimalPlaces(decimal n)
		{
			n = Math.Abs(n); //make sure it is positive.
			n -= (int)n;     //remove the integer part of the number.
			var decimalPlaces = 0;
			while (n > 0)
			{
				decimalPlaces++;
				n *= 10;
				n -= (int)n;
			}
			return decimalPlaces;
		}
		#endregion
		#region STRUCTS

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct CellData
		{
			public double GammaInDeg;
			public double GammaOutDeg;
			public double CellSizeMM;
			public double PiezoLengthMM;
			public double PiezoOffsetMM;
			public double PiezoWidthMM;
			public double CutAngleDeg;
		};
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public unsafe struct RF_Data
		{
			public double RFPower;
			public double RFPeriod;
			public int RFPoints; //именно int! Дело в том, что c-шный код содержит long, но в С long занимает 4 байта!!! Так что здесь юзаем int
			public double* FreqMHz;
			public double* Ampl;
		};
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public unsafe struct SPECTRUM_Data
		{
			public double AirAngleDeg;
			public int OptPolar;
			public int WavelengthCount;
			public double* WavelengthArray;
			public double* EffArray;
		};
		#endregion
		#region Additional classes
		
		public class FreqMass
		{
			private const double RFPower_init = 0;

			private double _RFPower;
			private double _Period;
			private int _Count;
			private double[] _Freqs;
			private double[] _Magnitudes;

			private double[] _Times;
			private double _Timestep;
			
			public double RFPower { get => _RFPower; }
			public double Period { get => _Period; }
			public int Count { get => _Count; }
			public double[] Freqs { get => _Freqs; }
			public double[] Magnitudes { get => _Magnitudes; }
			public double[] Times { get => _Times; }
			public double Timestep { get => _Timestep; }

			/// <summary>
			/// Инициализатор класса, содержащего информацию о массиве частот, подаваемых на АО ячейку для синтеза аппаратной функции определенной формы.
			/// </summary>
			/// <param name="Freqs">Массив частот для расчета аппаратной функции [МГц] </param>
			/// <param name="Magnitudes">Массив амплитуд, соответственно предыдущему. Предполагается нормированным на 1, т.е. максимальное значение в массиве - 1.</param>
			/// <param name="period">Временная частота повтора сигнала [cек] </param>
			/// <param name="RFPower">Максимальная мощность сигнала [Вт]. По умолчанию = 0 Вт </param>
			public FreqMass(double[] Freqs, double[] Magnitudes, double period, double RFPower = RFPower_init)
			{				
				_Freqs = Freqs;
				_Magnitudes = Magnitudes;
				_Times = new double[_Freqs.Count()];

				_RFPower = RFPower;
				_Count = _Freqs.Count();
				_Timestep = period / _Count;
				_Period = period;

				//заполнение массивов
				for (int i = 0; i < _Count; i++) _Times[i] = i * _Timestep;			
			}
			/// <summary>
			/// Инициализатор класса, содержащего информацию о массиве частот, подаваемых на АО ячейку для синтеза аппаратной функции определенной формы.
			/// </summary>
			/// <param name="InitialData">Структура, содержащая исходные данные о наборе частот для синтеза определенной аппаратной функции пропускания.</param>
			public FreqMass(RF_Data InitialData)
			{
                unsafe
				{
					_Freqs = new double[InitialData.RFPoints];
					_Magnitudes = new double[InitialData.RFPoints];
					_Times = new double[InitialData.RFPoints];

					_RFPower = InitialData.RFPower;
					_Count = InitialData.RFPoints;
					_Period = InitialData.RFPeriod;
					_Timestep = InitialData.RFPeriod / InitialData.RFPoints;	
			
					//заполнение массивов
					Marshal.Copy((IntPtr)InitialData.FreqMHz, _Freqs, 0, _Count);
					Marshal.Copy((IntPtr)InitialData.Ampl, _Magnitudes, 0, _Count);

					int decimal_places_toround = GetDecimalPlaces((decimal)_Timestep) + 1; //определение количества знаков, до которого надо округлять.
					for (int i=0; i<InitialData.RFPoints; i++)
                    {
						_Times[i] = Math.Round(_Timestep * i, decimal_places_toround); 
					}
				}
			}
			/// <summary>
			/// Конвертер экземпляра класса в структуру, понятную исходной библиотеке
			/// </summary>
			public RF_Data ToRFDataStruct()
            {
				RF_Data result;
				result.RFPower = _RFPower; /* игнорируется в обратной задаче */
				result.RFPeriod = _Period;
				result.RFPoints = this.Count;
				unsafe
				{
					result.FreqMHz = (double*)Marshal.AllocHGlobal((int)(result.RFPoints * sizeof(double)));
					Marshal.Copy(Freqs, 0, (IntPtr)result.FreqMHz, Freqs.Count());

					result.Ampl = (double*)Marshal.AllocHGlobal((int)(result.RFPoints * sizeof(double)));
					Marshal.Copy(Magnitudes, 0, (IntPtr)result.Ampl, Freqs.Count());
				}
				return result;
			}
			/// <summary>
			/// Создает пустую структуру для решения обратной задачи. В ней содержатся только данные о повторе сигнала и о количестве частот, на основании которых будет построена аппаратная функция. 
			/// </summary>
			/// <param name="period">Временная частота повтора сигнала</param>
			/// <param name="num_of_pts">Количество частот, с помощью которых будет синтезироваться аппаратная функция пропускания</param>
			public static RF_Data Create_EmptyRFStruct(double period, int num_of_pts)
            {
				/*
			Готовим данные о массиве частот (сколько нам нужно частот и т.д.)
			(см. спецификацию файла rfdata.csv из инструкции к программе FINDSIGNAL)
			*/

				RF_Data FreqTable;
				FreqTable.RFPower = RFPower_init; /* игнорируется в обратной задаче */
				FreqTable.RFPeriod = period;
				FreqTable.RFPoints = (int)num_of_pts;
				unsafe
				{
					FreqTable.FreqMHz = (double*)Marshal.AllocHGlobal((int)(FreqTable.RFPoints * sizeof(double)));
					FreqTable.Ampl = (double*)Marshal.AllocHGlobal((int)(FreqTable.RFPoints * sizeof(double)));
				}
				return FreqTable;
			}
		}

		
		public class WLMass
		{
			public const double AirAngleDeg_init = 0; //угол падения по умолчанию
			public const int OptPolar_init = 0; //признак e-поляризации. По умолчанию 0, т.е. свет падает o-поляризованным. 

			private double _AirAngleDeg;
			private int _OptPolar;
			private double[] _WLs;
			private double[] _Magnitudes;
			private int _Count;

			public double AirAngleDeg { get => _AirAngleDeg; }
			public int OptPolar { get => _OptPolar; }
			public double[] WLs { get => _WLs; }
			public double[] Magnitudes { get => _Magnitudes; }
			public int Count { get => _Count; }

			/// <summary>
			/// Инициализатор класса, содержащего информацию об аппаратной функции пропускания.
			/// </summary>
			/// <param name="WLs">Массив длин волн [мкм]</param>
			/// <param name="Magnitudes">Массив амплитуд указанных длин волн соответственно. Нормирован на 1, т.е. максимальное значение - 1.</param>
			/// <param name="OptPolar">Признак e-поляризации. 1 соответствует e-поляризации. 0 - о-поляризации. По умолчанию 0.</param>
			/// <param name="AirAngleDeg">Угол падения излучения на входную грань. [гр.] По умолчанию 0 гр. </param>
			public WLMass(double[] WLs, double[] Magnitudes, int OptPolar = OptPolar_init, double AirAngleDeg = AirAngleDeg_init)
			{
				_WLs = WLs;
				_Magnitudes = Magnitudes;
				_Count = _WLs.Count();

				_AirAngleDeg = AirAngleDeg;
				_OptPolar = OptPolar;

			}
			/// <summary>
			/// Инициализатор класса, содержащего информацию об аппаратной функции пропускания.
			/// </summary>
			/// <param name="InitialData">Структура, содержащая исходные данные об аппаратной функции пропускания</param>
			/// <param name="Change_polarity">Параметр, предназначенный для фикса бага в библиотеке.<br/>
			/// Если функция вызвана для конвертации данных из библиотеки после решения прямой задачи, то необходимо изменить полярязацию выходного излучения, т.е. присвоить значение true. По умолчанию - false.</param>
			public WLMass(SPECTRUM_Data InitialData,bool Change_polarity = false)
			{
				
				_Count = InitialData.WavelengthCount;
				_AirAngleDeg = InitialData.AirAngleDeg;
				_OptPolar = Change_polarity? (1 - InitialData.OptPolar) : (InitialData.OptPolar);
				_WLs = new double[_Count];
				_Magnitudes = new double[_Count];
                //data copying
                unsafe
				{
					Marshal.Copy((IntPtr)InitialData.WavelengthArray, _WLs, 0, _WLs.Count());
					Marshal.Copy((IntPtr)InitialData.EffArray, _Magnitudes, 0, _Magnitudes.Count());
				}


			}
			/// <summary>
			/// Конвертер экземпляра класса в структуру, понятную исходной библиотеке
			/// </summary>
			public SPECTRUM_Data ToSpectrumDataStruct()
			{
			
				SPECTRUM_Data result;
				result.AirAngleDeg = _AirAngleDeg; /* игнорируется */
				result.OptPolar = _OptPolar; /* игнорируется */
				result.WavelengthCount = _Count;
				unsafe
				{
					result.WavelengthArray = (double*)Marshal.AllocHGlobal(_Count * sizeof(double));
					Marshal.Copy(WLs, 0, (IntPtr)result.WavelengthArray, _Count);
					result.EffArray = (double*)Marshal.AllocHGlobal(_Count * sizeof(double));
					Marshal.Copy(Magnitudes, 0, (IntPtr)result.EffArray, _Count);
				}

				return result;
			}
			/// <summary>
			/// Создает почти пустую структуру для решения прямой задачи. В ней содержатся только длины волн, для которых будет построена аппаратная функция. 
			/// </summary>
			/// <param name="WL_start">Длина волны, с которой начинается отчет [мкм]</param>
			/// <param name="WL_count">Количество длин волн для построения</param>
			/// <param name="WL_precision">Шаг по длине волны, с которым будет построена аппаратная функция [мкм]</param>
			/// <param name="OptPolar">Признак e-поляризации. 1 соответствует e-поляризации. 0 - о-поляризации. По умолчанию 0.</param>
			/// <param name="AirAngleDeg">Угол падения на входную грань [гр.]</param>
			public static SPECTRUM_Data Create_EmptyWLStruct(double WL_start, int WL_count, double WL_precision, int OptPolar = OptPolar_init, double AirAngleDeg = AirAngleDeg_init)
			{
			    /*
				Готовим таблицу со спектром, который мы хотим получить
				(см. спецификацию файла spectrum.csv из инструкции к программе FINDSIGNAL)
				*/
				SPECTRUM_Data Spectrum;
				Spectrum.AirAngleDeg = AirAngleDeg; 
				Spectrum.OptPolar = OptPolar; 
				Spectrum.WavelengthCount = WL_count;
                unsafe
				{
					Spectrum.WavelengthArray = (double*)Marshal.AllocHGlobal(Spectrum.WavelengthCount * sizeof(double));
					Spectrum.EffArray = (double*)Marshal.AllocHGlobal(Spectrum.WavelengthCount * sizeof(double));

					int decimal_places_toround = GetDecimalPlaces((decimal)WL_precision) + 1; //определение количества знаков, до которого надо округлять.																						   //заполнение массива ДВ
					for (int i = 0; i < WL_count; i++) Spectrum.WavelengthArray[i] = Math.Round(WL_start + i * WL_precision, decimal_places_toround);
				}
				return Spectrum;
			}
		}

		#endregion

	}

}
