using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Drawing;
#if X86
using FT_HANDLE = System.UInt32;
#endif
#if X64
using FT_HANDLE = System.UInt64;
#endif
#if X86
using UIntXX = System.UInt32;
#elif X64
using UIntXX = System.UInt64;
#endif
using System.Linq;
//using aom; 

namespace AO_Lib
{
    public static partial class AO_Devices
    {
        abstract public class AO_Filter : IDisposable
        {
            public abstract FilterTypes FilterType { get; }

            //Все о фильтре: дескриптор(имя), полное и краткое имя dev файла и управляющая dll
            
            protected abstract string _FilterName { set; get; }
            public virtual string FilterName { get { return (_FilterName); } }
            protected abstract string _FilterSerial { set; get; }
            public virtual string FilterSerial { get { return (_FilterSerial); } }
            protected abstract string FilterCfgName { set; get; }
            protected abstract string FilterCfgPath { set; get; }
            protected abstract string DllName { set; get; }

            protected abstract float[] HZs { set; get; }
            protected abstract float[] WLs { set; get; }
            protected abstract float[] Intensity { set; get; }
            //
            protected abstract bool AOF_Loaded_without_fails { set; get; }
            protected abstract bool sAOF_isPowered { set; get; }
            public virtual bool isPowered { get { return sAOF_isPowered; } }
            //базовые поля для получения диапазонов по перестройке
            public abstract float WL_Max { get; }
            public abstract float WL_Min { get; }
            public abstract float HZ_Max { get; }
            public abstract float HZ_Min { get; }
            public virtual float Intensity_Max { get { return Intensity.Max(); } }
            public virtual float Intensity_Min { get { return Intensity.Min(); } }
            protected virtual float sHZ_Current { set; get; }
            protected virtual float sWL_Current { set; get; }
            public float WL_Current { get { return sWL_Current; } }
            public float HZ_Current { get { return sHZ_Current; } }


            //все о свипе
            public virtual bool SweepAvailable => false;
            protected abstract bool sAO_Sweep_On { set; get; }
            public bool is_inSweepMode { get { return sAO_Sweep_On; } }

            public virtual float AO_ExchangeRate_Min { get { return 500; } } //[Гц]
            public virtual float AO_ExchangeRate_Max { get { return 4500; } } //[Гц]
            public virtual float AO_ProgrammMode_step { get { return 500; } } //[кГц/шаг]
            public virtual float AO_TimeDeviation_Min { get { return 5; } }   // [мс]     
            public virtual float AO_TimeDeviation_Max { get { return 40; } } // [мс]
            public virtual float AO_FreqDeviation_Min { get { return 0.5f; } } // [МГц]
            public virtual float AO_FreqDeviation_Max { get { return 10/*5.0f*/; } }// [МГц]

            public virtual bool Bit_inverse_needed { get { return false; } }
            protected virtual bool sBit_inverse_needed { set; get; }

            //все о программируемой перестройке
            protected abstract bool sAO_ProgrammMode_Ready { set; get; }
            public bool is_Programmed { get { return sAO_ProgrammMode_Ready; } }


            //поля для реализации специальной задержки между перестройками
            public virtual int MS_delay { get; protected set; }// [мс]
            public virtual int MS_delay_default { get { return 20; } }// [мс]
            public virtual int MS_delay_min { get { return 1; } }// [мс]
            public virtual int MS_delay_max { get { return 2000; } }// [мс]
            protected virtual System.Timers.Timer InnerTimer { set; get; }
            protected virtual float datavalue_2set { set; get; }
            protected virtual bool IsReady2set { set; get; }
            protected virtual bool WasLastSetting { set; get; }
            protected virtual Action<float> ActionOfSetting { set; get; }

            //события
            public delegate void SetNotifier(AO_Filter sender,float WL_now,float HZ_now);
            public abstract event SetNotifier onSetWl;
            public abstract event SetNotifier onSetHz;

            //очистка
            private bool disposed = false;

            //функционал
            protected AO_Filter()
            {
                //InitTimer(MS_delay_default);
            }

            ~AO_Filter()
            {
                Dispose(false);
            }
            public virtual void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!disposed)
                {
                    if (disposing)
                    {
                        InnerTimer.Dispose();                       
                    }
                    PowerOff();
                    disposed = true;
                }
            }

            public void InitTimer(int ms_delay)
            {
                // Create a timer with a two second interval.
                if ((ms_delay < MS_delay_min) || (ms_delay > MS_delay_max)) MS_delay = MS_delay_default;
                else MS_delay = ms_delay;
                InnerTimer?.Dispose();
                InnerTimer = null;
                InnerTimer = new System.Timers.Timer(ms_delay);
                // Hook up the Elapsed event for the timer. 
                InnerTimer.Elapsed += OnElapsedEvent;
                InnerTimer.AutoReset = true;
                InnerTimer.Stop();

                IsReady2set = true;
                WasLastSetting = false;
            }
            public void DeinitTimer()
            {
                InnerTimer.Dispose();
                InnerTimer = null;
            }
            
            protected virtual void OnElapsedEvent(Object source, System.Timers.ElapsedEventArgs e)
            {
                /*  Console.WriteLine("The Elapsed event was raised at {0:HH:mm:ss.fff}",
                                    e.SignalTime);*/
                IsReady2set = true;
                InnerTimer.Stop();
                if (!WasLastSetting)
                {
                    ActionOfSetting(datavalue_2set);
                    IsReady2set = false;
                    InnerTimer.Start();
                }
                else
                {
                    IsReady2set = true;
                }
                WasLastSetting = true;
            }
            //перестройка ДВ пропускания
            public virtual int Set_Wl(float pWL)
            {
                if ((pWL > WL_Max) || (pWL < WL_Min))
                    throw new Exception(String.Format("Unable to set this wavelenght. Please, enter the wavelenght value in {0} - {1} nm range.", WL_Min, WL_Max));
                else if (InnerTimer != null)
                {
                    if (!IsReady2set)
                    {
                        datavalue_2set = pWL;
                        WasLastSetting = false;
                        ActionOfSetting = new Action<float>((x) => { Set_Wl(x); });
                        if (!InnerTimer.Enabled) InnerTimer.Start();
                        throw new Exception(String.Format("Too fast setting! Setting timeout is {0} ms. Wavelenght of {1} nm will be set automatically after timeout.", MS_delay, pWL));
                    }
                    else
                    {
                        InnerTimer.Start();
                        IsReady2set = false;
                        WasLastSetting = true;
                        return 0;
                    }
                }
                else return 0;
            }
            public virtual int Set_Hz(float freq)
            {
                sAO_Sweep_On = false;

                if ((freq > HZ_Max) || (freq < HZ_Min))
                    throw new Exception(String.Format("Unable to set this freq. Please, enter the ultrasound frequency value in {0} - {1} MHz range.", HZ_Min, HZ_Max));
                else if (InnerTimer != null)
                {
                    if (!IsReady2set)
                    {
                        datavalue_2set = freq;
                        WasLastSetting = false;
                        ActionOfSetting = new Action<float>((x) => { Set_Hz(x); });
                        if (!InnerTimer.Enabled) InnerTimer.Start();
                        throw new Exception(String.Format("Too fast setting! Setting timeout is {0} ms. Ultrasound frequency of {1} MHz will be set automatically after timeout.", MS_delay, freq));
                    }
                    else
                    {
                        InnerTimer.Start();
                        IsReady2set = false;
                        WasLastSetting = true;
                        return 0;
                    }
                }
                else return 0;
            }

            public abstract int Set_Sweep_on(float MHz_start, float Sweep_range_MHz, double Period/*[мс с точностью до двух знаков]*/, bool OnRepeat);
           /* {
                sAO_Sweep_On = true;

                byte[] Own_UsbBuf = new byte[5000];
                int count = 0;
                int Multiplier = 0;
                int[] HZMass = STC_Filter.Calculate_sweep_params_012020(MHz_start, Sweep_range_MHz, Period, true, ref Multiplier);
                Own_UsbBuf = STC_Filter.Create_byteMass_byKnownParams_012020(HZMass, Multiplier);
                return 0;
            }*/
            public abstract int Set_Sweep_off();

            public abstract string Ask_required_dev_file();
            public virtual string Ask_loaded_dev_file() { return FilterCfgName; }
            public virtual int Read_dev_file(string path)
            {
                // throw new Exception("Ur in lib now 8"); 
                try
                {
                    var Data_from_dev = Helper.Files.Read_txt(path);
                    

                    sBit_inverse_needed = Data_from_dev[0].Contains("true") ? true : false;
                    if (Data_from_dev[0].Contains("true") || Data_from_dev[0].Contains("false")) Data_from_dev.RemoveAt(0);
                    FilterCfgPath = path;
                    FilterCfgName = System.IO.Path.GetFileName(path);
                    float[] pWLs, pHZs, pCoefs;
                    Helper.Files.Get_WLData_byKnownCountofNumbers(3, Data_from_dev.ToArray(), out pWLs, out pHZs, out pCoefs);

                    float[] pData = new float[pWLs.Length];
                    pWLs.CopyTo(pData, 0);
                    int RealLength = pWLs.Length - 1;
                    if (pWLs[0] - pWLs[RealLength] > 0) //если так, то меняет порядок
                    {
                        WLs = new float[pWLs.Length];
                        HZs = new float[pWLs.Length]; ;
                        Intensity = new float[pWLs.Length];
                        for (int i = 0; i < pWLs.Length; i++)
                        {
                            WLs[i] = pWLs[RealLength - i];
                            HZs[i] = pHZs[RealLength - i];
                            Intensity[i] = pCoefs[RealLength - i];
                        }
                    }
                    else
                    {
                        WLs = pWLs;
                        HZs = pHZs;
                        Intensity = pCoefs;
                    }
                    pWLs = WLs;
                    pHZs = HZs;
                    pCoefs = Intensity;
                    Helper.Math.Interpolate_curv(ref pWLs, ref pHZs);
                    Helper.Math.Interpolate_curv(ref pData, ref pCoefs);

                    WLs = pWLs;
                    HZs = pHZs;
                    Intensity = pCoefs;

                }
                catch
                {
                    return (int)FTDIController_lib.FT_STATUS.FT_OTHER_ERROR;
                }
                return (int)FTDIController_lib.FT_STATUS.FT_OK;
            }

            protected abstract int Init_device(uint number);
            protected abstract int Deinit_device();

            public abstract int PowerOn();
            public abstract int PowerOff();
            public abstract string Implement_Error(int pCode_of_error);

            
           

            public virtual float Get_HZ_via_WL(float pWL)
            {
                int num = WLs.Length;
                int rem_pos = -1;
                for (int i = 0; i < num - 1; i++)
                {
                    if ((WLs[i+1] >= pWL) && (WLs[i] <= pWL)) { rem_pos = i; break; }
                }
                if (rem_pos != -1)
                {
                    if (pWL - WLs[rem_pos] < 0.0001f) return HZs[rem_pos];
                    else if (WLs[rem_pos + 1] - pWL < 0.0001f) return HZs[rem_pos + 1];
                    else
                    {
                        return (float)Helper.Math.Interpolate_value(WLs[rem_pos], HZs[rem_pos], WLs[rem_pos + 1], HZs[rem_pos + 1], pWL);
                    }
                }
                else
                {
                    if(WLs[WLs.Length - 1]> WLs[0])
                    {
                        if (pWL > WLs[WLs.Length - 1]) return HZs[WLs.Length - 1];
                        else return HZs[0];
                    }
                    else
                    {
                        if (pWL < WLs[WLs.Length - 1]) return HZs[WLs.Length - 1];
                        else return HZs[0];
                    }
                }
            }

            public virtual float Get_WL_via_HZ(float pHZ)
            {
                int num = HZs.Length;
                int rem_pos = -1;
                for (int i = 0; i < num - 1; i++)
                {
                    if ((HZs[i] >= pHZ) && (HZs[i + 1] <= pHZ)) { rem_pos = i; break; }
                }
                if (rem_pos != -1)
                {
                    if (pHZ == HZs[rem_pos]) return WLs[rem_pos];
                    else if (pHZ == HZs[rem_pos + 1]) return WLs[rem_pos + 1];
                    else
                    {
                        return (float)Helper.Math.Interpolate_value(HZs[rem_pos], WLs[rem_pos], HZs[rem_pos + 1], WLs[rem_pos + 1], pHZ);
                    }
                }
                else
                {
                    return WLs[0];
                }

            }
            protected virtual float Get_Intensity_via_WL(int pWL)
            {
                float distance = (pWL - WL_Min);
                if ((distance < (WLs.Length)) && (distance >= 0))
                {
                    int a = (int)distance;
                    if ((distance - a) < 1e6f) { return Intensity[a]; }
                    else { return (float)Helper.Math.Interpolate_value(WLs[a], Intensity[a], WLs[a + 1], Intensity[a + 1], pWL); }
                }
                else
                {
                    if (distance < 0) return Intensity[0];
                    else return Intensity[Intensity.Length - 1];
                }
            }
            protected virtual float Get_Intensity_via_HZ(float pHZ)
            {
                int num = HZs.Length;
                int rem_pos = -1;
                float result = 3000;
                for (int i = 0; i < num - 1; i++)
                {
                    if ((HZs[i] >= pHZ) && (HZs[i + 1] <= pHZ)) { rem_pos = i; break; }
                }
                if (rem_pos != -1)
                {
                    if (pHZ == HZs[rem_pos]) return Intensity[rem_pos];
                    else if (pHZ == HZs[rem_pos + 1]) return Intensity[rem_pos + 1];
                    else
                    {
                        result = (float)Helper.Math.Interpolate_value(HZs[rem_pos], Intensity[rem_pos], HZs[rem_pos + 1], Intensity[rem_pos + 1], pHZ);
                    }
                }
                else
                {
                    result =  Intensity[0];
                }

                if (result < 2)
                    result = 3000;
                return result;
            }
            public virtual List<float> Find_freq_mass_by_Wls(float[] Wls, float[] Hzs, List<float> Wls_needed)
            {
                if (Wls_needed.Count == 0) return new List<float>();
                List<System.Drawing.PointF> PtsFinal = Helper.Math.Interpolate_curv(Wls, Hzs);
                List<float> result = new List<float>();
                int max_count = PtsFinal.Count;
                int max_k = Wls_needed.Count;
                int k = 0;
                for (int i = 0; (i != max_count) && (k != max_k); ++i)//Search freq by WL
                {
                    if (PtsFinal[i].X == Wls_needed[k])
                    {
                        result.Add(PtsFinal[i].Y);
                        ++k;
                    }
                }
                return result;
            }
            public virtual System.Drawing.PointF Sweep_Recalculate_borders(float pHZ_needed,float pHZ_Radius)
            {
                return Find_freq_borders_mass(new List<float> { pHZ_needed },pHZ_Radius, HZ_Min, HZ_Max)[0];
            }
            private static List<System.Drawing.PointF> Find_freq_borders_mass(List<float> Hzs_needed, float HZs_radius, float HZs_min, float HZs_max)
            {
                List<System.Drawing.PointF> result = new List<System.Drawing.PointF>(); // for each point: X is LeftBorder, Y is Width
                int max_count = Hzs_needed.Count;
                float preMin = 0, preMax = 0;
                for (int i = 0; i < max_count; i++)//Search freq by WL
                {
                    preMin = Hzs_needed[i] - HZs_radius;
                    preMax = Hzs_needed[i] + HZs_radius;
                    preMin = preMin < HZs_min ? HZs_min : preMin;
                    preMax = preMax > HZs_max ? HZs_max : preMax;
                    result.Add(new System.Drawing.PointF(preMin, (float)((double)preMax - (double)preMin)));
                }
                return result;
            }

            [Obsolete]
            public static AO_Filter Find_and_connect_AnyFilter(bool IsEmulator = false)
            {
                if (IsEmulator) return (new Emulator());

                int NumberOfTypes = 3;
                int[] Devices_per_type = new int[NumberOfTypes];

                string[] Descriptor_forSTCFilter; string[] Serial_forSTCFilter;
                Devices_per_type[0] = STC_Filter.Search_Devices(out Descriptor_forSTCFilter, out Serial_forSTCFilter);
                AO_Filter retFil = null;
#if X64
                if (Devices_per_type[0] != 0)
                    retFil = (new STC_Filter(Descriptor_forSTCFilter.Last(), Serial_forSTCFilter.Last()));
                else
                    retFil =  (new Emulator());
#elif X86
                Devices_per_type[1] = VNIIFTRI_Filter_v15.Search_Devices();
                Devices_per_type[2] = VNIIFTRI_Filter_v20.Search_Devices();
                if (Devices_per_type[0] != 0) retFil =  (new STC_Filter(Descriptor_forSTCFilter.Last(), Serial_forSTCFilter.Last()));

                else if (Devices_per_type[1] != 0) retFil =  (new VNIIFTRI_Filter_v15());
                else if (Devices_per_type[2] != 0) retFil =  (new VNIIFTRI_Filter_v20());
                else retFil =  (new Emulator());
#endif
                return retFil;
            }
            public static List<AO_Filter> Find_all_filters()
            {
                var FinalList = new List<AO_Filter>();
                //search of filters of any types
                var l1 = List_STC_Filters();
                var l2 = List_VNIIFTRI_Filters_v15();
                var l3 = List_VNIIFTRI_Filters_v20();
                FinalList = ConcatLists_ofFilters(l1, l2, l3);


               // if (FinalList.Count == 0) FinalList.Add(new Emulator());
                return FinalList;
            }
            private static List<AO_Filter> List_STC_Filters()
            {
                var FinalList = new List<AO_Filter>();
                string[] FilterNames; string[] FilterSerials;
                var NumOfDev = STC_Filter.Search_Devices(out FilterNames,out FilterSerials);
                for(int i = 0;i<NumOfDev;i++)
                {
                    FinalList.Add(new STC_Filter(FilterNames[i], FilterSerials[i]));
                }
                return FinalList;
            }
            private static List<AO_Filter> List_VNIIFTRI_Filters_v15()
            {
                var FinalList = new List<AO_Filter>();
                return FinalList;
            }
            private static List<AO_Filter> List_VNIIFTRI_Filters_v20()
            {
                var FinalList = new List<AO_Filter>();
                return FinalList;
            }
            private static List<AO_Filter> ConcatLists_ofFilters(params List<AO_Filter>[] filters)
            {
                List<AO_Filter> datalist = new List<AO_Filter>();
                foreach (List<AO_Filter> list in filters)
                { datalist.AddRange(list); }
                return datalist;
            }
        }
        public class Emulator : AO_Filter, ISweepable
        {
            public override FilterTypes FilterType { get { return FilterTypes.Emulator; } }

            protected override string _FilterName { set; get; }
            protected override string _FilterSerial { set; get; }
            protected override string FilterCfgName { set; get; }
            protected override string FilterCfgPath { set; get; }
            protected override string DllName { set; get; }

            protected override float[] HZs { set; get; }
            protected override float[] WLs { set; get; }
            protected override float[] Intensity { set; get; }
            protected override bool AOF_Loaded_without_fails { set; get; }
            protected override bool sAOF_isPowered { set; get; }

            public override float WL_Max { get { return WLs[WLs.Length - 1]; } }
            public override float WL_Min { get { return WLs[0]; } }
            public override float HZ_Max { get { return HZs[0]; } }
            public override float HZ_Min { get { return HZs[WLs.Length - 1]; } }

            protected override bool sAO_Sweep_On { set; get; }

            protected override bool sAO_ProgrammMode_Ready { set; get; }

            public override bool Bit_inverse_needed { get { return sBit_inverse_needed; } }

            public override event SetNotifier onSetWl;
            public override event SetNotifier onSetHz;

            public override bool SweepAvailable { get { return true; } }
            public SweepParameters SweepLastParameters { get; private set; }

            public Emulator() : base()
            {
                var Random = new Random();
                var i_max = Random.Next(4, 10); var num = 0;
                for (int i = 0; i < i_max; i++) num = Random.Next(100000, 999999);

                _FilterName = "Filter "+FilterType.ToString();
                _FilterSerial = "F"+num.ToString();
               sAO_ProgrammMode_Ready = false;
            }
            ~Emulator()
            {
                this.PowerOff();
                this.Dispose();
            }
            public override int Set_Wl(float pWL)
            {
                base.Set_Wl(pWL);
                sWL_Current = pWL;
                sHZ_Current = Get_HZ_via_WL(pWL);
                onSetWl?.Invoke(this,WL_Current,HZ_Current);
                return 0;
            }
            public override int Set_Hz(float freq)
            {
                base.Set_Hz(freq);
                sWL_Current = Get_WL_via_HZ(freq);
                sHZ_Current = freq;
                onSetHz?.Invoke(this,WL_Current, HZ_Current);
                return 0;
            }

            public override int Set_Sweep_on(float MHz_start, float Sweep_range_MHz, double Period/*[мс с точностью до двух знаков]*/, bool OnRepeat)
            {
                sAO_Sweep_On = true;
                return 0;
            }
            public override int Set_Sweep_off()
            {
                sAO_Sweep_On = false;
                return 0;
            }
            protected override int Init_device(uint number)
            {
                return 0;
            }

            protected override int Deinit_device()
            {
                return 0;
            }
            public override string Ask_required_dev_file()
            {
                return "(any *.dev file) | EMULATOR" ;
            }
            
            public override int PowerOn()
            {
                sAOF_isPowered = true;
                return 0;
            }
            public override int PowerOff()
            {
                sAOF_isPowered = false;
                return 0;
            }

            public override void Dispose()
            {
                
            }

            public static int Search_Devices()
            {
                return 1;
            }

            public override string Implement_Error(int pCode_of_error)
            {
                return "Common error";
            }

            public int Set_Sweep_on(float MHz_start, float Sweep_range_MHz, int steps, double time_up, double time_down)
            {
                if (Sweep_range_MHz > HZ_Max - HZ_Min ||
                        Sweep_range_MHz > AO_FreqDeviation_Max ||
                        Sweep_range_MHz < 0 ||
                        MHz_start < HZ_Min ||
                        MHz_start > HZ_Max ||
                        (time_up == 0 && time_down == 0) ||
                        steps <= 0)
                    throw new Exception("Invalid sweep parameters!");

                if (MHz_start + Sweep_range_MHz > HZ_Max)
                    MHz_start = HZ_Max - Sweep_range_MHz;

                //Запомним последние установленные значения
                SweepParameters sweepLastParameters = new SweepParameters();
                sweepLastParameters.steps = steps;
                sweepLastParameters.Sweep_range = Sweep_range_MHz;
                sweepLastParameters.time_up = time_up;
                sweepLastParameters.time_down = time_down;
                sweepLastParameters.f0 = MHz_start;
                SweepLastParameters = sweepLastParameters;

                sAO_Sweep_On = true;

                return 0;
            }

            public int SetHz_KeepSweep(float freq_MHz, bool keep = true)
            {
                int error = Set_Hz(freq_MHz);

                if (sAO_Sweep_On && keep)
                {
                    //если свип включен, то устанавливаем последние его параметры с новой частотой
                    return Set_Sweep_on(freq_MHz,
                        SweepLastParameters.Sweep_range,
                        SweepLastParameters.steps,
                        SweepLastParameters.time_up,
                        SweepLastParameters.time_down);
                }
                else
                {
                    Set_Sweep_off();
                    return error;
                }
            }
        }

#if X86
        public class VNIIFTRI_Filter_v15 : AO_Filter //идея: сделать 2 класса чисто на импорт, а обвязку оставить общую
        {
            public override FilterTypes FilterType { get { return FilterTypes.VNIIFTRI_Filter_v15; } }

            protected override string _FilterName { set; get; }
            protected override string _FilterSerial { set; get; }
            protected override string FilterCfgName { set; get; }
            protected override string FilterCfgPath { set; get; }
            protected override string DllName { set; get; }

            protected override float[] HZs { set; get; }
            protected override float[] WLs { set; get; }
            protected override float[] Intensity { set; get; }
            protected override bool AOF_Loaded_without_fails { set; get; }
            protected override bool sAOF_isPowered { set; get; }

            public override float WL_Max { get { return WLs[WLs.Length - 1]; } }
            public override float WL_Min { get { return WLs[0]; } }
            public override float HZ_Max { get { return HZs[0]; } }
            public override float HZ_Min { get { return HZs[WLs.Length - 1]; } }

            protected override bool sAO_Sweep_On { set; get; }
            protected override bool sAO_ProgrammMode_Ready { set; get; }

            public override event SetNotifier onSetWl;
            public override event SetNotifier onSetHz;

            public VNIIFTRI_Filter_v15() : base()
            {
                Init_device(0);
                _FilterName = Ask_required_dev_file();
                _FilterSerial = "0 - number in the list of VNIIFTRI_Filter_v15 Filters";
                sAO_ProgrammMode_Ready = false;
            }
            public VNIIFTRI_Filter_v15(int number) : base()
            {
                Init_device((uint)number);
                _FilterName = Ask_required_dev_file();
                _FilterSerial = number.ToString() + " - number in the list of VNIIFTRI_Filter_v15 Filters";
                sAO_ProgrammMode_Ready = false;
            }
            ~VNIIFTRI_Filter_v15()
            {
                this.PowerOff();
                this.Dispose();
            }

            public override int Set_Wl(float pWL)
            {
                base.Set_Wl(pWL);
                sWL_Current = pWL;
                sHZ_Current = Get_HZ_via_WL(pWL);
                int code =  AOM_SetWL(pWL);
                onSetWl?.Invoke(this, WL_Current, HZ_Current);
                return code;
            }
            public override int Set_Hz(float freq)
            {
                base.Set_Hz(freq);
                sWL_Current = Get_WL_via_HZ(freq);
                sHZ_Current = freq;
                int code = AOM_SetWL((int)Math.Round(sWL_Current));
                onSetHz?.Invoke(this, WL_Current, HZ_Current);
                return code;
            }
            public override int Set_Sweep_on(float MHz_start, float Sweep_range_MHz, double Period/*[мс с точностью до двух знаков]*/, bool OnRepeat)
            {
                sAO_Sweep_On = false;
                return (int)Status.AOM_OTHER_ERROR;
            }
            public override int Set_Sweep_off()
            {
                sAO_Sweep_On = false;
                return (int)Status.AOM_OTHER_ERROR;
            }
            protected override int Init_device(uint number)
            {
                AOM_Init((int)number);
                return 0;
            }
            protected override int Deinit_device()
            {
                return AOM_Close();
            }
            public override string Ask_required_dev_file()
            {
                StringBuilder dev_name = new StringBuilder(7);
                AOM_GetID(dev_name);
                return dev_name.ToString();
            }
            public override int Read_dev_file(string path)
            {
                float min=0, max=0;
                try
                {
                    var Data_from_dev = Helper.Files.Read_txt(path);
                    float[] pWLs, pHZs, pCoefs;
                    Helper.Files.Get_WLData_byKnownCountofNumbers(3, Data_from_dev.ToArray(), out pWLs, out pHZs, out pCoefs);
                    Helper.Math.Interpolate_curv(pWLs, pHZs);
                    Helper.Math.Interpolate_curv(pWLs, pCoefs);
                    WLs = pWLs;
                    HZs = pHZs;
                    Intensity = pCoefs;
                    int state = AOM_LoadSettings(path,ref min, ref max);
                    FilterCfgPath = path;
                    FilterCfgName = System.IO.Path.GetFileName(path);
                    if ((min != WL_Min) || (max != WL_Max)|| (state!=0)) throw new Exception();
                }
                catch
                {
                    return -1;
                }
                return 0;
            }

            public override int PowerOn()
            {
                var retval = AOM_PowerOn();
                if (retval == 0) sAOF_isPowered = true;
                else sAOF_isPowered = false;
                return retval;
            }
            public override int PowerOff()
            {
                var retval = AOM_PowerOff();
                if (retval == 0) sAOF_isPowered = false;
                else sAOF_isPowered = true;
                return retval;
            }
            public override void Dispose()
            {
                Deinit_device();
            }
            public static int Search_Devices()
            {
                return AOM_GetNumDevices();
            }
            public override string Implement_Error(int pCode_of_error)
            {
                return ((Status)pCode_of_error).ToString();
            }

#region DllFunctions
            public const string basepath = "aom_old.dll";
            //Назначение: функция возвращает число подключенных акустооптических фильтров.
            [DllImport(basepath, CallingConvention = CallingConvention.Cdecl)]
            public static extern int AOM_GetNumDevices();

            //Назначение: функция производит инициализацию подключенного акустооптического фильтра 
            //(обычное значение devicenum = 0, т.е. первое).
            [DllImport(basepath, CallingConvention = CallingConvention.Cdecl)]
            public static extern int AOM_Init(int devicenum);

            //Назначение: функция выполняет деинициализацию акустооптического фильтра.
            [DllImport(basepath, CallingConvention = CallingConvention.Cdecl)]
            public static extern int AOM_Close();

            //Назначение: функция записывает в переменную id значение идентификатор подключенного акустооптического фильтра.
            [DllImport(basepath, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern int AOM_GetID([MarshalAs(UnmanagedType.LPStr)] StringBuilder id);

            //Назначение: функция производит загрузку значений максимальной
            //(wlmax) и минимальной длины волны (wlmin) из файла с именем filename с расширением *.dev.
            [DllImport(basepath,
                CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
            public static extern int AOM_LoadSettings(string filename, ref float wlmin, ref float wlmax);

            //Назначение: функция выполняет выгрузку установленных значений из калибровочного файла формата *.dev.
            [DllImport(basepath, CallingConvention = CallingConvention.Cdecl)]
            public static extern int AOM_UnloadSettings();

            //Назначение: функция производит установку требуемой частоты акустооптического фильтра
            [DllImport(basepath, CallingConvention = CallingConvention.Cdecl)]
            public static extern int AOM_SetWL(float wl);

            //Назначение: функция производит включение акустооптического фильтра.
            [DllImport(basepath, CallingConvention = CallingConvention.Cdecl)]
            public static extern int AOM_PowerOn();

            //Назначение: функция производит выключение акустооптического фильтра.
            [DllImport(basepath, CallingConvention = CallingConvention.Cdecl)]
            public static extern int AOM_PowerOff();

            private enum Status
            {
                AOM_OK = 0,
                AOM_ALREADY_INITIALIZED,
                AOM_ALREADY_LOADED,
                AOM_NOT_INITIALIZED,
                AOM_DEVICE_NOTFOUND,
                AOM_BAD_RESPONSE,
                AOM_NULL_POINTER,
                AOM_FILE_NOTFOUND,
                AOM_FILE_READ_ERROR,
                AOM_WINUSB_INIT_FAIL,
                AOM_NOT_LOADED,
                AOM_RANGE_ERROR,
                AOM_OTHER_ERROR
            }            
#endregion
        }
        public class VNIIFTRI_Filter_v20 : AO_Filter //идея: сделать 2 класса чисто на импорт, а обвязку оставить общую
        {
            public override FilterTypes FilterType { get { return FilterTypes.VNIIFTRI_Filter_v20; } }

            protected override string _FilterName { set; get; }
            protected override string _FilterSerial { set; get; }
            protected override string FilterCfgName { set; get; }
            protected override string FilterCfgPath { set; get; }
            protected override string DllName { set; get; }

            protected override float[] HZs { set; get; }
            protected override float[] WLs { set; get; }
            protected override float[] Intensity { set; get; }
            protected override bool AOF_Loaded_without_fails { set; get; }
            protected override bool sAOF_isPowered { set; get; }

            public override float WL_Max { get { return WLs[WLs.Length - 1]; } }
            public override float WL_Min { get { return WLs[0]; } }
            public override float HZ_Max { get { return HZs[0]; } }
            public override float HZ_Min { get { return HZs[WLs.Length - 1]; } }

            protected override bool sAO_Sweep_On { set; get; }
            protected override bool sAO_ProgrammMode_Ready { set; get; }

            public override event SetNotifier onSetWl;
            public override event SetNotifier onSetHz;

            public VNIIFTRI_Filter_v20() : base()
            {
                Init_device(0);
                _FilterName = Ask_required_dev_file();
                _FilterSerial = "0 - number in the list of VNIIFTRI_Filter_v20 Filters";
                sAO_ProgrammMode_Ready = false;
            }

            public VNIIFTRI_Filter_v20(int number) : base()
            {
                Init_device((uint)number);
                _FilterName = Ask_required_dev_file();
                _FilterSerial = number.ToString() + " - number in the list of VNIIFTRI_Filter_v20 Filters";
                sAO_ProgrammMode_Ready = false;
            }
            ~VNIIFTRI_Filter_v20()
            {
                this.PowerOff();
                this.Dispose();
            }
            public override int Set_Wl(float pWL)
            {
                base.Set_Wl(pWL);
                sWL_Current = pWL;
                sHZ_Current = Get_HZ_via_WL(pWL);
                int code =  AOM_SetWL(pWL);
                onSetWl?.Invoke(this, WL_Current, HZ_Current);
                return code;
            }
            public override int Set_Hz(float freq)
            {
                base.Set_Hz(freq);
                sWL_Current = Get_WL_via_HZ(freq);
                sHZ_Current = freq;
                int code = AOM_SetWL((int)Math.Round(sWL_Current));
                onSetHz?.Invoke(this, WL_Current, HZ_Current);
                return code;
            }
            public override int Set_Sweep_on(float MHz_start, float Sweep_range_MHz, double Period/*[мс с точностью до двух знаков]*/, bool OnRepeat)
            {
                sAO_Sweep_On = false;
                return (int)Status.AOM_OTHER_ERROR;
            }
            public override int Set_Sweep_off()
            {
                sAO_Sweep_On = false;
                return (int)Status.AOM_OTHER_ERROR;
            }
            protected override int Init_device(uint number)
            {
                AOM_Init((int)number);
                return 0;
            }
            protected override int Deinit_device()
            {
                return AOM_Close();
            }
            public override string Ask_required_dev_file()
            {
                StringBuilder dev_name = new StringBuilder(7);
                AOM_GetID(dev_name);
                return dev_name.ToString();
            }

            public override int Read_dev_file(string path)
            {
                float min = 0, max = 0;
                try
                {
                    var Data_from_dev = Helper.Files.Read_txt(path);
                    float[] pWLs, pHZs, pCoefs;
                    Helper.Files.Get_WLData_byKnownCountofNumbers(3, Data_from_dev.ToArray(), out pWLs, out pHZs, out pCoefs);
                    Helper.Math.Interpolate_curv(pWLs, pHZs);
                    Helper.Math.Interpolate_curv(pWLs, pCoefs);
                    WLs = pWLs;
                    HZs = pHZs;
                    Intensity = pCoefs;
                    int state = AOM_LoadSettings(path, ref min, ref max);
                    FilterCfgPath = path;
                    FilterCfgName = System.IO.Path.GetFileName(path);
                    if ((min != WL_Min) || (max != WL_Max) || (state != 0)) throw new Exception();
                }
                catch
                {
                    return -1;
                }
                return 0;
            }

            public override int PowerOn()
            {
                var retval = AOM_PowerOn();
                if (retval == 0) sAOF_isPowered = true;
                else sAOF_isPowered = false;
                return retval;
            }
            public override int PowerOff()
            {
                var retval = AOM_PowerOff();
                if (retval == 0) sAOF_isPowered = false;
                else sAOF_isPowered = true;
                return retval;
            }
            public override void Dispose()
            {
                Deinit_device();
            }
            public static int Search_Devices()
            {

                return AOM_GetNumDevices();
            }
            public override string Implement_Error(int pCode_of_error)
            {
                return ((Status)pCode_of_error).ToString();
            }

#region DllFunctions
            public const string basepath = "aom_new.dll";
            //Назначение: функция возвращает число подключенных акустооптических фильтров.
            [DllImport(basepath, CallingConvention = CallingConvention.Cdecl)]
            public static extern int AOM_GetNumDevices();

            //Назначение: функция производит инициализацию подключенного акустооптического фильтра 
            //(обычное значение devicenum = 0, т.е. первое).
            [DllImport(basepath, CallingConvention = CallingConvention.Cdecl)]
            public static extern int AOM_Init(int devicenum);

            //Назначение: функция выполняет деинициализацию акустооптического фильтра.
            [DllImport(basepath, CallingConvention = CallingConvention.Cdecl)]
            public static extern int AOM_Close();

            //Назначение: функция записывает в переменную id значение идентификатор подключенного акустооптического фильтра.
            [DllImport(basepath, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern int AOM_GetID([MarshalAs(UnmanagedType.LPStr)] StringBuilder id);

            //Назначение: функция производит загрузку значений максимальной
            //(wlmax) и минимальной длины волны (wlmin) из файла с именем filename с расширением *.dev.
            [DllImport(basepath,
                CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
            public static extern int AOM_LoadSettings(string filename, ref float wlmin, ref float wlmax);

            //Назначение: функция выполняет выгрузку установленных значений из калибровочного файла формата *.dev.
            [DllImport(basepath, CallingConvention = CallingConvention.Cdecl)]
            public static extern int AOM_UnloadSettings();

            //Назначение: функция производит установку требуемой частоты акустооптического фильтра
            [DllImport(basepath, CallingConvention = CallingConvention.Cdecl)]
            public static extern int AOM_SetWL(float wl);

            //Назначение: функция производит включение акустооптического фильтра.
            [DllImport(basepath, CallingConvention = CallingConvention.Cdecl)]
            public static extern int AOM_PowerOn();

            //Назначение: функция производит выключение акустооптического фильтра.
            [DllImport(basepath, CallingConvention = CallingConvention.Cdecl)]
            public static extern int AOM_PowerOff();

            private enum Status
            {
                AOM_OK = 0,
                AOM_ALREADY_INITIALIZED,
                AOM_ALREADY_LOADED,
                AOM_NOT_INITIALIZED,
                AOM_DEVICE_NOTFOUND,
                AOM_BAD_RESPONSE,
                AOM_NULL_POINTER,
                AOM_FILE_NOTFOUND,
                AOM_FILE_READ_ERROR,
                AOM_WINUSB_INIT_FAIL,
                AOM_NOT_LOADED,
                AOM_RANGE_ERROR,
                AOM_OTHER_ERROR
            }
#endregion
        }
#endif

        public class STC_Filter : AO_Filter, ISweepable
        {
            public override FilterTypes FilterType { get { return FilterTypes.STC_Filter; } }


            protected override string _FilterName { set; get; }
            protected override string _FilterSerial { set; get; }
            protected override string FilterCfgName { set; get; }
            protected override string FilterCfgPath { set; get; }
            protected override string DllName { set; get; }

            protected override float[] HZs { set; get; }
            protected override float[] WLs { set; get; }
            protected override float[] Intensity { set; get; }
            protected override bool AOF_Loaded_without_fails { set; get; }
            protected override bool sAOF_isPowered { set; get; }

            public override float WL_Max { get { return WLs[WLs.Length - 1]; } }
            public override float WL_Min { get { return WLs[0]; } }
            public override float HZ_Max { get { return HZs[0]; } }
            public override float HZ_Min { get { return HZs[WLs.Length - 1]; } }

            public int Current_Attenuation { get { return sCurrent_Attenuation; } }
            private int sCurrent_Attenuation = 0;

            protected override bool sAO_Sweep_On { set; get; }
            protected override bool sAO_ProgrammMode_Ready { set; get; }
            private double Reference_frequency = 350e6;
            private const double dT_sweep_min = 11.42;
            private const double dT_sweep_max = 11.42*65536;
            private const double F_deviat_max = 5000;//KHz
            private const double dF_deviat_max = 200;//KHz
            private byte[] Own_UsbBuf = new byte[5000];
            private byte[] Own_ProgrammBuf;
#if X86
            private UInt32 Own_m_hPort = 0;
#elif X64
            private UInt64 Own_m_hPort = 0;
#endif

            public override bool SweepAvailable { get { return true; } }
            public SweepParameters SweepLastParameters { get; private set; }


            public override bool Bit_inverse_needed { get { return sBit_inverse_needed; } }

            public override event SetNotifier onSetWl;
            public override event SetNotifier onSetHz;

            //timeout set checker
            private int Timeout_MS = 1000;
            private System.Diagnostics.Stopwatch Timeout_Timer;
            //disposer

            private bool IsDisposed = false;

            public static class MainCommands
            {
                public static byte SET_HZ { get { return 0x03; } }
                public static byte TEST { get { return 0x66; } }
                public static byte POWER_OFF { get { return 0x05; } }
                public static byte USER_CURVE { get { return 0xC0; } }
            }
            /// <summary>
            /// Конструктор. Инициализирует экземляр класса по номеру (и дескриптору) физического АО фильтра
            /// </summary>
            public STC_Filter(string Descriptor,uint number) : base()
            {
                _FilterName = Descriptor;
                _FilterSerial = number.ToString() + " - number in the list of STC Filters";
                try
                {
                    Init_device(number);
                    AOF_Loaded_without_fails = true;
                    sAO_ProgrammMode_Ready = false;
                }
                catch
                {
                    AOF_Loaded_without_fails = false;
                }
                Timeout_Timer = new System.Diagnostics.Stopwatch();
            }
            public STC_Filter(string Descriptor, string Serial) : base()
            {
                _FilterName = Descriptor;
                _FilterSerial = Serial;
                try
                {
                    int errornum = (int)FTDIController_lib.FT_STATUS.FT_OK;
                    try
                    {
                        if (Serial == "undefined\0") errornum = Init_device(Descriptor, false);
                        else errornum = Init_device(Serial);
                        if (errornum != (int)FTDIController_lib.FT_STATUS.FT_OK)
                            throw new Exception();
                    }
                    catch
                    {
                        errornum = Init_device(0);
                        if (errornum != (int)FTDIController_lib.FT_STATUS.FT_OK)
                            throw new Exception("ORIGINAL: " + (FTDIController_lib.FT_STATUS)(errornum));
                    }
                        
                    AOF_Loaded_without_fails = true;

                    sAO_ProgrammMode_Ready = false;
                }
                catch(Exception exc2)
                {
                    AOF_Loaded_without_fails = false;
                }
                Timeout_Timer = new System.Diagnostics.Stopwatch();
            }

          

            /*  /// <summary>
              /// Деструктор
              /// </summary>
              ~STC_Filter()
              {
                  this.PowerOff();
                  this.Dispose();
              }*/
            protected override void Dispose(bool disposing)
            {

                if (IsDisposed) return;
                if (disposing)
                {
                   
                }
                IsDisposed = true;
                // Обращение к методу Dispose базового класса
                base.Dispose(disposing);
                Deinit_device();
            }

            public override int PowerOn()
            {
                var state = Set_Wl((WL_Max + WL_Min) / 2);
                sAOF_isPowered = true;
                return state;
            }


        


            public override int PowerOff()
            {
                try
                {
                    System.Threading.Thread.Sleep(300);
                    Own_UsbBuf[0] = MainCommands.POWER_OFF; //it means, we will send off command

                    for (int i = 1; i < 2; i++) Own_UsbBuf[i] = 0;
                    Own_UsbBuf[0] = (byte)FTDIController_lib.Bit_reverse(Own_UsbBuf[0], Bit_inverse_needed);
                    try { WriteUsb(1); }
                    catch { return (int)FTDIController_lib.FT_STATUS.FT_IO_ERROR; }
                }
                catch { return (int)FTDIController_lib.FT_STATUS.FT_OTHER_ERROR; }
                sAOF_isPowered = false;
                return 0;
            }

            protected override int Deinit_device()
            {
                System.Threading.Thread.Sleep(100);
                int result = Convert.ToInt16(AO_Devices.FTDIController_lib.FT_Close(Own_m_hPort));
                return result;
            }

            /// <summary>
            /// Устанавливает на АОФ заданную длину волны в нм.
            /// </summary>
            public override int Set_Wl(float pWL)
            {
                base.Set_Wl(pWL);
                if (AOF_Loaded_without_fails)
                {
                    try
                    {
                        float freq = Get_HZ_via_WL(pWL);
                        sWL_Current = pWL;
                        sHZ_Current = Get_HZ_via_WL(pWL);
                        var datadel = onSetHz;//disabling the delegate to avoid double notifying
                        onSetHz = null;
                        int code = Set_Hz(freq);
                        onSetHz = datadel;//activating
                        onSetWl?.Invoke(this, WL_Current, HZ_Current);
                        return code;
                    }
                    catch
                    {
                        return (int)FTDIController_lib.FT_STATUS.FT_OTHER_ERROR;
                    }
                }
                else
                {
                    return (int)FTDIController_lib.FT_STATUS.FT_DEVICE_NOT_FOUND;
                }
            }
           
            /// <summary>
            /// Устанавливает на АОФ заданную частоту в МГц.
            /// </summary>
            public override int Set_Hz(float freq)
            {
                base.Set_Hz(freq);
                if (AOF_Loaded_without_fails)
                {

                    var code_er = FTDIController.FT_STATUS.FT_OK;
                    try
                    {
                        Own_UsbBuf = Create_byteMass_forHzTune(freq);
                        /*  var code_er = FTDIController.FT_ResetDevice(Own_m_hPort); //ResetDevice();
                          code_er = FTDIController.FT_Purge(Own_m_hPort, FTDIController.FT_PURGE_RX | FTDIController.FT_PURGE_TX); // все что было в буфере вычищается
                          code_er = FTDIController.FT_ResetDevice(Own_m_hPort); //ResetDevice();*/
                     
                        Timeout_Timer.Restart();
                        WriteUsb(7);
                        Timeout_Timer.Stop();
                        if (Timeout_Timer.ElapsedMilliseconds > Timeout_MS) code_er = FTDIController.FT_STATUS.FT_POWER_PROBLEM;


                         sWL_Current = Get_WL_via_HZ(freq);
                        sHZ_Current = freq;
                        onSetHz?.Invoke(this, WL_Current, HZ_Current);
                        if (code_er != FTDIController.FT_STATUS.FT_OK) throw new Exception("Error ib AO_lib on Set_Hz. Status of the problem: " + code_er.ToString());
                        return 0;
                    }
                    catch (Exception exc)
                    {
                        if (code_er == FTDIController.FT_STATUS.FT_POWER_PROBLEM) return (int)FTDIController.FT_STATUS.FT_POWER_PROBLEM;
                        else return (int)FTDIController_lib.FT_STATUS.FT_OTHER_ERROR;
                    }
                }
                else
                {
                    return (int)FTDIController_lib.FT_STATUS.FT_DEVICE_NOT_FOUND;
                }
            }

            /// <summary>
            /// Устанавливает частоту на АОФ при помощи заранее вычисленного массива байт.
            /// Для вычисления массива необходимо использовать функцию Create_byteMass_forHzTune
            /// </summary>
            public int Set_Hz_via_bytemass(byte[] buf)
            {
                try
                {
                    var code_er = FTDIController.FT_STATUS.FT_OK;
                    /*  var code_er = FTDIController.FT_ResetDevice(Own_m_hPort); //ResetDevice();
                      code_er = FTDIController.FT_Purge(Own_m_hPort, FTDIController.FT_PURGE_RX | FTDIController.FT_PURGE_TX); // все что было в буфере вычищается
                      code_er = FTDIController.FT_ResetDevice(Own_m_hPort); //ResetDevice();*/
                    WriteUsb(buf, 7);
                    if (code_er != FTDIController.FT_STATUS.FT_OK) throw new Exception("Error ib AO_lib on Set_Hz_via_bytemass");
                    return 0;
                }
                catch (Exception exc)
                {

                    return (int)FTDIController_lib.FT_STATUS.FT_OTHER_ERROR;
                }
            }

            /// <summary>
            /// Устанавливает на АОФ заданную частоту в МГц. Позволяет задать коэффициент ослабления.
            /// Пока что (21.01.2020) доступна только для заданного класса. По основному содержимому не отличается от Set_Hz(float freq)
            /// </summary>
            public int Set_Hz(float freq,float pCoef_Power_Decrement = 000)
            {
                
                if (AOF_Loaded_without_fails)
                {
                    try
                    {
                        if (pCoef_Power_Decrement == 0)
                        {
                            Own_UsbBuf = Create_byteMass_forHzTune(freq);
                            sCurrent_Attenuation = (int)pCoef_Power_Decrement;
                        }
                        else
                        {
                            Own_UsbBuf = Create_byteMass_forHzTune(freq, (uint)pCoef_Power_Decrement);
                            sCurrent_Attenuation = (int)pCoef_Power_Decrement;
                        }
                        var code_er = FTDIController.FT_STATUS.FT_OK;
                  /*      var code_er = FTDIController.FT_ResetDevice(Own_m_hPort); //ResetDevice();
                        code_er = FTDIController.FT_Purge(Own_m_hPort, FTDIController.FT_PURGE_RX | FTDIController.FT_PURGE_TX); // все что было в буфере вычищается
                        code_er = FTDIController.FT_ResetDevice(Own_m_hPort); //ResetDevice();*/

                        WriteUsb(7);//установка частоты происходит фактически тут
                        sWL_Current = Get_WL_via_HZ(freq);
                        sHZ_Current = freq;
                        onSetHz?.Invoke(this, WL_Current, HZ_Current);
                        if (code_er != FTDIController.FT_STATUS.FT_OK) throw new Exception("Error ib AO_lib on Set_Hz");
                        return 0;
                    }
                    catch(Exception exc)
                    {

                        return (int)FTDIController_lib.FT_STATUS.FT_OTHER_ERROR;
                    }
                }
                else
                {
                    return (int)FTDIController_lib.FT_STATUS.FT_DEVICE_NOT_FOUND;
                }
            }

            /// <summary>
            /// Создает по заданным параметром массив байт для перестройки на определенную частоту.
            /// </summary>
            public byte[] Create_byteMass_forHzTune(float pfreq, uint pCoef_PowerDecrease = 0)
            {
                float fvspom; short MSB, LSB; ulong lvspom;

                float freq_was = pfreq;
                byte[] data_Own_UsbBuf = new byte[5000];
                uint ivspom = 1700;
                if (pCoef_PowerDecrease == 0)
                    ivspom = (uint)Get_Intensity_via_HZ(pfreq);
                else
                    ivspom = pCoef_PowerDecrease;


                pfreq = (freq_was) /*/ 1.17f*/; //in MHz
                                                //set init freq
                pfreq = ((freq_was) * 1e6f) /*/ 1.17f*/; //in Hz

                fvspom = pfreq / (float)Reference_frequency;
                lvspom = (ulong)((pfreq) * (Math.Pow(2.0, 32.0) / Reference_frequency));
                //fvspom*pow(2.0,32.0);
                //lvspom=freq;
                MSB = (short)(0x0000ffFF & (lvspom >> 16));
                LSB = (short)lvspom;

                data_Own_UsbBuf[0] = MainCommands.SET_HZ; //it means, we will send wavelength

                data_Own_UsbBuf[1] = (byte)(0x00ff & (MSB >> 8));
                data_Own_UsbBuf[2] = (byte)MSB;
                data_Own_UsbBuf[3] = (byte)(0x00ff & (LSB >> 8));
                data_Own_UsbBuf[4] = (byte)LSB;
                data_Own_UsbBuf[5] = (byte)(0x00ff & (ivspom >> 8));
                data_Own_UsbBuf[6] = (byte)ivspom;

                int b2w = 7;

                for (int i = 0; i < b2w; i++)
                {
                    data_Own_UsbBuf[i] = (byte)AO_Devices.FTDIController_lib.Bit_reverse(data_Own_UsbBuf[i], Bit_inverse_needed);
                }
                return data_Own_UsbBuf;
            }

            /// <summary>
            /// Программируемый режим. Реализован не на всех типах АО (21.01.2020). 
            /// Формирует байтовый массив по заданным параметрам для активации программируемого режима
            /// </summary>        
            [Obsolete]
            public void Create_byteMass_forProgramm_mode(float[,] pAO_All_CurveSweep_Params)
            {
                // TODO: прописать описание входного массива.
                int i_max = pAO_All_CurveSweep_Params.GetLength(0);
                float[,] Mass_of_params = new float[i_max, 7];
                int i = 0;
                byte[] Start_mass = new byte[4] {
                    (byte)FTDIController_lib.Bit_reverse(0x14, Bit_inverse_needed),
                    (byte)FTDIController_lib.Bit_reverse(0x11, Bit_inverse_needed),
                    (byte)FTDIController_lib.Bit_reverse(0x12, Bit_inverse_needed),
                    (byte)FTDIController_lib.Bit_reverse(0xff, Bit_inverse_needed) };
                byte[] Separ_mass = new byte[3] { 0x13, 0x13, 0x13 };
                byte[] Finish_mass = new byte[3] {  (byte)FTDIController_lib.Bit_reverse(0x15, Bit_inverse_needed),
                                                    (byte)FTDIController_lib.Bit_reverse(0x15, Bit_inverse_needed),
                                                    (byte)FTDIController_lib.Bit_reverse(0x15, Bit_inverse_needed) };
                for (i = 0; i < i_max; i++)
                {
                    Mass_of_params[i, 0] = pAO_All_CurveSweep_Params[i, 0]; //ДВ (для отображения)
                    if (pAO_All_CurveSweep_Params[i, 3] != 0) //строка со свипом
                    {
                        System.Drawing.PointF data_for_sweep = this.Sweep_Recalculate_borders(pAO_All_CurveSweep_Params[i, 2], pAO_All_CurveSweep_Params[i, 3]);
                        Mass_of_params[i, 1] = data_for_sweep.X;//Частота Синтезатора
                        Mass_of_params[i, 2] = data_for_sweep.Y;//пересчитанная девиация 
                    }
                    else//строка с обычной перестройкой
                    {
                        Mass_of_params[i, 1] = pAO_All_CurveSweep_Params[i, 2];//Частота Синтезатора
                        Mass_of_params[i, 2] = 0;//пересчитанная девиация
                    }
                    Mass_of_params[i, 3] = pAO_All_CurveSweep_Params[i, 4]; //время одной девиации
                    Mass_of_params[i, 4] = pAO_All_CurveSweep_Params[i, 5]; //количество девиаций
                }


                List<byte[]> pre_DataList = new List<byte[]>();
                int Lenght = 0;
                for (i = 0; i < i_max; i++)
                {
                    if (Mass_of_params[i, 2] == 0)//стандартная перестройка
                    {
                        int pCount = 0;
                        int time_ms = (int)(Mass_of_params[i, 3]);
                        pre_DataList.Add(Create_byteMass_forProgrammMode_HZTune(Mass_of_params[i, 1], time_ms,ref pCount));
                       /* pre_DataList.Add(Separ_mass);
                        pre_DataList.Add(new byte[1] { (byte)Mass_of_params[i, 4] });*/
                        Lenght += (pCount/*+ Separ_mass.Length+1*/);
                    }
                    else//свип
                    {
                        int pCount = 0;
                        int time_ms = (int)(Mass_of_params[i, 3]);
                        pre_DataList.Add(Create_byteMass_forProgrammMode_Sweep(Mass_of_params[i, 1], Mass_of_params[i, 2], time_ms, ref pCount));
                      /*  pre_DataList.Add(Separ_mass);
                        pre_DataList.Add(new byte[1] { (byte)Mass_of_params[i, 4] });*/
                        Lenght += (pCount/* + Separ_mass.Length + 1*/);
                    }
                }

                //Переписываем все данные в массив на пересылку
                byte[] Result_mass = new byte[Start_mass.Length + Lenght + Finish_mass.Length];
                int k = 0;
                for(i =0;i < Start_mass.Length;i++)
                {
                    Result_mass[k] = Start_mass[i];k++;
                }

                for (i = 0; i < i_max; i++)
                {
                    for (int j = 0; j < pre_DataList[i].Length; j++)
                    {
                        Result_mass[k] = pre_DataList[i][j]; k++;
                    }                  
                }

                for (i = 0; i < Finish_mass.Length; i++)
                {
                    Result_mass[k] = Finish_mass[i]; k++;
                }

               /* byte[] ownbuf2 = new byte[5000];
                for (i = 0; i < 5000; i++) ownbuf2[i] = 0;
                for (i = 0; i < Result_mass.Length; i++)
                    ownbuf2[i] = Result_mass[i];*/

                Own_ProgrammBuf = Result_mass;
                sAO_ProgrammMode_Ready = true;
            }

            /// <summary>
            /// Программируемый режим. Формирует часть массива байт, связанных со свипом.
            /// </summary>        
            [Obsolete]
            private byte[] Create_byteMass_forProgrammMode_Sweep(float pMHz_start, float pSweep_range_MHz /*не менее 1МГц*/, double pPeriod/*[мс с точностью до двух знаков,минимум 1]*/, ref int pcount)
            {
                float freq, fvspom;
                short MSB, LSB;
                ulong lvspom;
                uint ivspom;
                float delta;
                int steps;
                int i;
                float freqMCU = 74.99e6f;
                float inp_freq = 20*1000.0f / (float)pPeriod; //in Hz, max 4000 hz //дефолт от Алексея
                double New_Freq_byTime = (pSweep_range_MHz * 1e3f / pPeriod); // [kHz/ms] , 57.4 и более //375
                double Step_kHZs = pSweep_range_MHz*1e3f/20.0f;                                     //   было 200, [kHz] // В новом режиме 500 kHz - дефолт от Алексея 
                double Steps_by1MHz = 1e3f / Step_kHZs;                     //      [шагов/МГц]   

             /*   if ((float)(Steps_by1MHz * New_Freq_byTime) < AO_ExchangeRate_Min) //если менее 287, то пересчитываем размер шага, чтобы было более
                {
                    Steps_by1MHz = AO_ExchangeRate_Min / New_Freq_byTime;
                    Step_kHZs = 1e3 / Steps_by1MHz;
                }
                inp_freq = (int)(float)(Steps_by1MHz * New_Freq_byTime);//500-4500*/

                pcount = 2 - 1;
                //   steps = (int)Math.Floor(pSweep_range_MHz * Steps_by1MHz); // number of the steps
                steps = 20;
                double Step_HZs = Step_kHZs * 1000;

                int total_count = pcount + 1 + 4 * steps;
                byte[] data_Own_UsbBuf = new byte[total_count];

                for (i = 1; i < total_count; i++)
                {
                    data_Own_UsbBuf[i] = 0;
                }
                for (i = 0; i < steps; i++)//перепроверить цикл
                {
                    
                    freq = (float)(((pMHz_start) * 1e6f + i * Step_HZs)/*/ 1.17f*/);//1.17 коррекция частоты
                                                                                    //fvspom=freq/Reference_frequency;
                    lvspom = (ulong)((freq) * (Math.Pow(2.0, 32.0) / Reference_frequency)); //расчет управляющего слова для частоты
                    MSB = (short)(0x0000ffFF & (lvspom >> 16));
                    LSB = (short)lvspom;
                    data_Own_UsbBuf[pcount + 1] = (byte)(0x00ff & (MSB >> 8));
                    data_Own_UsbBuf[pcount + 2] = (byte)(MSB);
                    data_Own_UsbBuf[pcount + 3] = (byte)(0x00ff & (LSB >> 8));
                    data_Own_UsbBuf[pcount + 4] = (byte)(LSB);
                    pcount += 4;
                }

                pcount -= 4;//компенсация последнего смещения

                //timer calculations
                ivspom = (uint)(65536 - freqMCU / (2 * 2 * inp_freq));

                /*data_Own_UsbBuf[0] = (byte)(0x00ff & (pcount >> 8));
                  data_Own_UsbBuf[1] = (byte)(pcount);
                  data_Own_UsbBuf[2] = (byte)(0x00ff & (ivspom >> 8)); ;
                  data_Own_UsbBuf[3] = (byte)ivspom;*/
                data_Own_UsbBuf[0] = (byte)(0x00ff & (ivspom >> 8)); ;
                data_Own_UsbBuf[1] = (byte)ivspom;
                pcount = total_count;
                //
                for (i = 0; i < total_count; i++)
                {
                    data_Own_UsbBuf[i] = (byte)FTDIController_lib.Bit_reverse(data_Own_UsbBuf[i], Bit_inverse_needed);
                }
                return data_Own_UsbBuf;
            }

            /// <summary>
            /// Программируемый режим. Формирует часть массива байт, связанных со обычной перестройкой.
            /// </summary>  
            [Obsolete]
            private byte[] Create_byteMass_forProgrammMode_HZTune(float pfreq, double pPeriod/*[мс с точностью до двух знаков,минимум 1]*/, ref int pcount)
            {
                float freq, fvspom;
                short MSB, LSB;
                ulong lvspom;
                uint ivspom;
                float delta;
                int steps;
                int i;
                float freqMCU = 74.99e6f;
                float inp_freq = 20 * 1000.0f / (float)pPeriod;  //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

                pcount = 2 - 1;
                steps = 20; // number of the steps

                int total_count = pcount + 1 + 4 * steps;
                byte[] data_Own_UsbBuf = new byte[total_count];

                for (i = 1; i < total_count; i++)
                {
                    data_Own_UsbBuf[i] = 0;
                }
                for (i = 0; i < steps; i++)//перепроверить цикл
                {

                    if (i < 10)
                        freq = (float)(((pfreq) * 1e6f)/* / 1.17f*/);//1.17 коррекция частоты
                    else
                        freq = (float)(((pfreq) * 1e6f)/* / 1.17f*/);//1.17 коррекция частоты
                                                                     

                    lvspom = (ulong)((freq) * (Math.Pow(2.0, 32.0) / Reference_frequency)); //расчет управляющего слова для частоты
                    MSB = (short)(0x0000ffFF & (lvspom >> 16));
                    LSB = (short)lvspom;
                    data_Own_UsbBuf[pcount + 1] = (byte)(0x00ff & (MSB >> 8));
                    data_Own_UsbBuf[pcount + 2] = (byte)(MSB);
                    data_Own_UsbBuf[pcount + 3] = (byte)(0x00ff & (LSB >> 8));
                    data_Own_UsbBuf[pcount + 4] = (byte)(LSB);
                    pcount += 4;
                }

                pcount -= 4;//компенсация последнего смещения

                //timer calculations
                ivspom = (uint)(65536 - freqMCU / (2 * 2 * inp_freq));

                /*data_Own_UsbBuf[0] = (byte)(0x00ff & (pcount >> 8));
                  data_Own_UsbBuf[1] = (byte)(pcount);
                  data_Own_UsbBuf[2] = (byte)(0x00ff & (ivspom >> 8)); ;
                  data_Own_UsbBuf[3] = (byte)ivspom;*/

                data_Own_UsbBuf[0] = (byte)(0x00ff & (ivspom >> 8)); ;
                data_Own_UsbBuf[1] = (byte)ivspom;
                pcount = total_count;
                //
                for (i = 0; i < total_count; i++)
                {
                    data_Own_UsbBuf[i] = (byte)FTDIController_lib.Bit_reverse(data_Own_UsbBuf[i], Bit_inverse_needed);
                }
                return data_Own_UsbBuf;
            }

            /// <summary>
            /// Программируемый режим. Активирует программируемый режим.
            /// </summary>  
            [Obsolete]
            public int Set_ProgrammMode_on()
            {
                try
                {
                    var code_er = FTDIController.FT_STATUS.FT_OK;
                  /*  var code_er = FTDIController.FT_ResetDevice(Own_m_hPort); //ResetDevice();
                    code_er = FTDIController.FT_Purge(Own_m_hPort, FTDIController.FT_PURGE_RX | FTDIController.FT_PURGE_TX); // все что было в буфере вычищается
                    code_er = FTDIController.FT_ResetDevice(Own_m_hPort); //ResetDevice();*/
                    WriteUsb(Own_ProgrammBuf, Own_ProgrammBuf.Length);

                    if (code_er != FTDIController.FT_STATUS.FT_OK) throw new Exception("Error ib AO_lib on Set_ProgrammMode_on");
                }
                catch { return (int)FTDIController_lib.FT_STATUS.FT_IO_ERROR; }
                return (int)FTDIController_lib.FT_STATUS.FT_OK;
            }

            /// <summary>
            /// Программируемый режим. Деактивирует программируемый режим. Устанавливается центральная частота.
            /// </summary>  
            ///
            [Obsolete]
            public int Set_ProgrammMode_off()
            {

              //  is_Programmed = false;
                return Set_Hz((HZ_Max + HZ_Min) / 2.0f);
            }
            [Obsolete]
            private byte[] Create_byteMass_forSweep(float pMHz_start, float pSweep_range_MHz, double pPeriod/*[мс с точностью до двух знаков,минимум 1]*/, ref int pcount)
            {
                float freq, fvspom;
                short MSB, LSB;
                ulong lvspom;
                uint ivspom;
                float delta;
                int steps;
                int i;
                float freqMCU = 74.99e6f;
                float inp_freq = 20 * 1000.0f / (float)pPeriod; //in Hz, max 4500 hz //дефолт от Алексея
                double New_Freq_byTime = (pSweep_range_MHz * 1e3f / pPeriod); // [kHz/ms] , 57.4 и более //375
                double Step_kHZs = pSweep_range_MHz * 1e3f / 20.0f;                                     //   было 200, [kHz] // В новом режиме 500 kHz - дефолт от Алексея 
                double Steps_by1MHz = 1e3f / Step_kHZs;                     //      [шагов/МГц]   
                pcount = 4 + 2 - 1;//4 бита на начальный массив, 2 на таймер
                steps = 20;
                double Step_HZs = Step_kHZs * 1000;

                int total_count = pcount + 1 + 4 * steps + 3;
                byte[] data_Own_UsbBuf = new byte[total_count];
                bool isInverse_needed = Bit_inverse_needed;
                byte[] Start_mass = new byte[4] { (byte)FTDIController_lib.Bit_reverse(0x14,isInverse_needed), (byte)FTDIController_lib.Bit_reverse(0x11,isInverse_needed),
                    (byte)FTDIController_lib.Bit_reverse(0x12,isInverse_needed), (byte)FTDIController_lib.Bit_reverse(0xff,isInverse_needed) };
                byte[] Finish_mass = new byte[3] { (byte)FTDIController_lib.Bit_reverse(0x15,isInverse_needed), (byte)FTDIController_lib.Bit_reverse(0x15,isInverse_needed),
                    (byte)FTDIController_lib.Bit_reverse(0x15,isInverse_needed) };

                for (i = 1; i < total_count; i++)
                {
                    data_Own_UsbBuf[i] = 0;
                }
                for (i = 0; i < steps; i++)//перепроверить цикл
                {

                    freq = (float)(((pMHz_start) * 1e6f + i * Step_HZs)/*/ 1.17f*/);//1.17 коррекция частоты
                                                                                    //fvspom=freq/Reference_frequency;
                    lvspom = (ulong)((freq) * (Math.Pow(2.0, 32.0) / Reference_frequency)); //расчет управляющего слова для частоты
                    MSB = (short)(0x0000ffFF & (lvspom >> 16));
                    LSB = (short)lvspom;
                    data_Own_UsbBuf[pcount + 1] = (byte)(0x00ff & (MSB >> 8));
                    data_Own_UsbBuf[pcount + 2] = (byte)(MSB);
                    data_Own_UsbBuf[pcount + 3] = (byte)(0x00ff & (LSB >> 8));
                    data_Own_UsbBuf[pcount + 4] = (byte)(LSB);
                    pcount += 4;
                }

                pcount -= 4;//компенсация последнего смещения

                //timer calculations
                ivspom = (uint)(65536 - freqMCU / (2 * 2 * inp_freq));

                data_Own_UsbBuf[4] = (byte)(0x00ff & (ivspom >> 8)); ;
                data_Own_UsbBuf[5] = (byte)ivspom;
                pcount = total_count;

                for (i = 0; i < total_count; i++)
                {
                    data_Own_UsbBuf[i] = (byte)FTDIController_lib.Bit_reverse(data_Own_UsbBuf[i], isInverse_needed);
                }
                for (i = 0; i < 4; i++)
                {
                    data_Own_UsbBuf[i] = Start_mass[i];
                }
                for (i = 0; i < 3; i++)
                {
                    data_Own_UsbBuf[i + total_count - 3] = Finish_mass[i];
                }
                return data_Own_UsbBuf;
            }

            /// <summary>
            /// Пересчитывает заданные пользователем параметры мультичастотного режима в массив конечных данных
            /// </summary>
            /// <param name="freq_mass">Массив частот для перестройки</param>
            /// <param name="T_needed">Необходимое время перестройки для всех частот [мкс] </param>         
            public byte[] Create_byteMass_MultiFrequencyMode_112020(List<float> freq_mass, float T_needed)
            {
                //initializing 
                int freq_num = freq_mass.Count;
                byte[] data_buf = new byte[1 + 2 + 1 + freq_num * 4 + 2];
                ulong const1 = (ulong)Math.Pow(2.0, 32.0);
                byte[] data_iFreq = new byte[4];
                //startbyte - 1 byte
                byte SByte = 0xBB;
               
                //Number of freq - 2 bytes
                byte[] N_Mass = Helper.Processing.uInt_to_2bytes((uint)freq_num);

                //zero byte -1 byte
                byte ZByte = 0x00;

                //time calculations - 2 bytes
                double fsys_mcu = 1.7f * (0.5f * 75e6);
                double mfreq_sys = Reference_frequency / 1e6;
                float step_min = (float)(4.0 / mfreq_sys); //in usec
                float step_max = step_min * (int)Math.Pow(2, 16);
                float step_need = T_needed / freq_num;
                if ((step_need> step_max) || (step_need < step_min)) return null;
                int Time_multiplier = (int)(step_need / step_min);
                byte[] TimeMass = Helper.Processing.uInt_to_2bytes((uint)Time_multiplier);

                //freq calculations and fulfilling
                for (int i = 0; i < freq_num; i++)
                { //передача начальной, конечной частот и шага
                    ulong data_lvspom = (ulong)((freq_mass[i]) * (const1 / (mfreq_sys)));
                    data_iFreq = Helper.Processing.uLong_to_4bytes(data_lvspom);
                    data_buf[4 + (i * 4)] = data_iFreq[0];
                    data_buf[5 + (i * 4)] = data_iFreq[1];
                    data_buf[6 + (i * 4)] = data_iFreq[2];
                    data_buf[7 + (i * 4)] = data_iFreq[3];
                }
                data_buf[0] = SByte;
                data_buf[1] = N_Mass[0];
                data_buf[2] = N_Mass[1];
                data_buf[3] = ZByte;
                data_buf[1 + 2 + 1 + freq_num * 4 + 0] = TimeMass[0];
                data_buf[1 + 2 + 1 + freq_num * 4 + 1] = TimeMass[1];

                return data_buf;
            }

            /// <summary>
            /// Пересчитывает заданные пользователем параметры свипинга в массив конечных данных
            /// </summary>
            /// <param name="mfreq0_sweep">F0 - начальная частоты свипа</param>
            /// <param name="mdeltafreq_sweep">Дельта по частоте, определяющая диапазон перестройки</param>
            /// <param name="mN_sweep">Количество шагов при перестройке</param>
            /// <param name="T_up_sweep">Время "подъема", мкс</param>
            /// <param name="T_down_sweep">Время "спуска", мкс</param>
            /// <param name="mode">Режим — пила(false) или треугольник(true)</param>
            public byte[] Create_byteMass_byKnownParams_062020(float mfreq0_sweep,float mdeltafreq_sweep,
                int mN_sweep,
                double T_up_sweep,double T_down_sweep,
                bool mode,
                bool m_repeat = true)
            {   
                float[] freq = new float[3]; // unsigned long lvspom; unsigned int ivspom; float fvspom, freq[3], minstep;
                byte[] data_buf = new byte[26];
                double fsys_mcu = 1.7f * (0.5f * 75e6);
                double mfreq_sys = Reference_frequency/1e6; 

                //выставляем таймер для того, чтобы определять режим повтора sweep
                uint timer_up = (uint)(65536 - 1e-6 * T_up_sweep * fsys_mcu / 2); //расчеты для таймера , mtup_sweep - время подъема
                uint timer_down = (uint)(65536 - 1e-6 * T_down_sweep * fsys_mcu / 2);//расчеты для таймера перезапускающего свип, mtdown_sweep - время спуска
                                                                                //начальный уровень амплитуды, default
                                                                                //ivspom = 1700;
                                                                                //Амплитуа из калибровочного файла, частота задается в МГц
                float minstep = (float)(4.0 / mfreq_sys); //in usec
                
                if (mN_sweep > 0) { freq[2] = mdeltafreq_sweep / mN_sweep; } //шаг изменения частоты, mN_sweep — количество шагов в sweep
                freq[0] = mfreq0_sweep; freq[1] = mfreq0_sweep + mdeltafreq_sweep; // начальная и конечная частоты
                byte delayt1 = (byte)Math.Round(T_up_sweep / (mN_sweep * minstep));
                byte delayt2 = (byte)Math.Round(T_down_sweep / (mN_sweep * minstep));
                data_buf[0] = 0x0b;
                data_buf[1] = 0x0b;
                byte[] data_iFreq = new byte[4];
                for (int i = 0; i <= 2; i++)
                { //передача начальной, конечной частот и шага
                    ulong data_lvspom = (ulong)((freq[i]) * (Math.Pow(2.0, 32.0) / (mfreq_sys)));
                    data_iFreq = Helper.Processing.uLong_to_4bytes(data_lvspom);
                    data_buf[2 + i * 4 + 0] = data_iFreq[0];
                    data_buf[2 + i * 4 + 1] = data_iFreq[1];
                    data_buf[2 + i * 4 + 2] = data_iFreq[2];
                    data_buf[2 + i * 4 + 3] = data_iFreq[3];
                }
               /* if (mdwell.GetCheck() == 1) { tx[14] = 1; }
                else { tx[14] = 0; } */
                data_buf[14] = Convert.ToByte(!mode);//dwell = true, если режим пила активирован(mode = false)
                data_buf[15] = Convert.ToByte(mode); //режим — пила(false) или треугольник(true)
                byte[] data_mass = new byte[2];
                uint LocalAmpl = (uint)Get_Intensity_via_HZ((mfreq0_sweep + (float)mdeltafreq_sweep / 2)); // ivspom = patof->GetAmplForFreq(mfreq);
                data_mass = Helper.Processing.uInt_to_2bytes(LocalAmpl);
                data_buf[16] = data_mass[0];
                data_buf[17] = data_mass[1]; //амплитуда
                data_mass = Helper.Processing.uInt_to_2bytes(timer_up);
                data_buf[18] = data_mass[0];
                data_buf[19] = data_mass[1];// шаг таймера, определяющего направление счета
                data_mass = Helper.Processing.uInt_to_2bytes(timer_down);
                data_buf[20] = data_mass[0];
                data_buf[21] = data_mass[1];//шаг таймера, определяющего направдение счета

                data_buf[22] = Convert.ToByte(m_repeat); // m_repeat - параметр многократности запуска
                data_buf[23] = delayt1; //задержки для аппаратного таймера ramp
                data_buf[24] = delayt2; //задержки ramp
                return data_buf;
              //  Write(tx, 25, &ret_bytes);

                return null;
            }

            /// <summary>
            /// Пересчитывает заданные пользователем параметры свипа в массив конечных частот
            /// </summary>
            /// <param name="pMHz_start">Начальная частота в МГц</param>
            /// <param name="pSweep_range_MHz">Диапазон варьирования. Максимум 5 МГц (012020)</param>
            /// <param name="pPeriod">Временной интервал, в течение которого необходимо провести один цикл изменения частот, мкс. Минимум - 0,571 мкс (012020)</param>
            /// <param name="pMHz_start">Форма профиля одно цикла: 0 - равнобедренный треугольник, 1 - прямоугольный треугольник</param>
            [Obsolete]
            public static int[] Calculate_sweep_params_012020(float pMHz_start, float pSweep_range_MHz, double pPeriod_mks, bool isTriangle, ref int Multiplier)
            {
                //Принципы: делаем не меньше 25*2 шагов, так как в противном случае можем натыкаться на частные ограничения
                //Например, невозможно пройти 5 МГц в обе стороны (50 шагов) за время в 0,3 мкс
                //Почему? Исходя из максимальной девиации в dF = 5 МГц и максимального шага в 200 KHz. Шаг мы всегда можем сделать меньше. Но не больше.
                //позже решил исходить из 1000 шагов. Так оказалось проще
                int steps_min = (int)(F_deviat_max / dF_deviat_max);
                steps_min = (steps_min * 2);
                double t_dev_min = dT_sweep_min * steps_min;
                double t_dev_max = dT_sweep_max * 1000; //а вот 1000 шагов мы можем сделать всегда. Но не более. Здесь это примерно 0,748мс

                int pPeriod_ns = (int)(pPeriod_mks * 1000);
                if (pPeriod_ns < t_dev_min || pPeriod_ns > t_dev_max)
                { throw new Exception(); }


                int Num_of_steps = 50;
                double dT_current_ns = (double)pPeriod_ns / (double)Num_of_steps;
                while (dT_current_ns > dT_sweep_max || dT_current_ns < dT_sweep_min)
                    //в этом цикле вычисляется количество шагов в случае, если 50 не подходит,
                    //с учетом минимизации количества шагов (для того, чтобы минимизировать погрешность)
                {
                    if (Num_of_steps >= steps_min * 2)
                    {
                        Num_of_steps = Num_of_steps / 2;
                        dT_current_ns = (double)pPeriod_ns / (double)Num_of_steps;
                    }
                    else 
                    if(Num_of_steps >= steps_min +1)
                    {
                        Num_of_steps = Num_of_steps - 1;
                        dT_current_ns = (double)pPeriod_ns / (double)Num_of_steps;
                    }
                }
                //подумать, как минимизировать погрешность по времени. Если , скажем, dT_current_ns=16нс, то ближайшее, что мы можем взять - 11,42нс


                int data_Multiplier = (int)(dT_current_ns / dT_sweep_min)+1;
                int[] Freq_mass_hz;
                if(isTriangle)
                {
                    if (Num_of_steps % 2 != 0) Num_of_steps += 1;

                    double dFreq_step_kHz = pSweep_range_MHz * 1000 / (Num_of_steps/2);//получим шаг по частоте в kHz
                    if (dFreq_step_kHz> dF_deviat_max) { throw new Exception(); }

                    Freq_mass_hz = new int[Num_of_steps];
                    for (int i =0;i< Num_of_steps / 2;i++)
                    {
                        Freq_mass_hz[i] = (int)((pMHz_start*1000 + dFreq_step_kHz*i)*1000);
                    }
                    for (int i = 0; i < Num_of_steps / 2; i++)
                    {
                        Freq_mass_hz[Num_of_steps / 2 + i] = (int)(((pMHz_start+ pSweep_range_MHz) * 1000 - dFreq_step_kHz*i) * 1000);
                    }
                }
                else
                {
                    
                    double dFreq_step_kHz = pSweep_range_MHz * 1000 / (Num_of_steps - 1);//получим шаг по частоте в kHz
                    if (dFreq_step_kHz > dF_deviat_max) { throw new Exception(); }

                    Freq_mass_hz = new int[Num_of_steps];

                    for (int i = 0; i < Num_of_steps - 1; i++)
                    {
                        Freq_mass_hz[i] = (int)((pMHz_start * 1000 + dFreq_step_kHz*i) * 1000);
                    }
                    Freq_mass_hz[Num_of_steps-1] = (int)(((pMHz_start + pSweep_range_MHz) * 1000) * 1000);
                }
                //массив частот посчитан Freq_mass_hz[i] 
                //время перестройки известно dT_current_ns //множитель известен Multiplier
                Multiplier = data_Multiplier;
                return Freq_mass_hz;

            }
            public static byte[] Create_byteMass_sweep_byKnownParams_012020(int[] Freq_mass_hz, int time_multiplier)
            {
                int totalcount = 1 + 2 + 4 * Freq_mass_hz.Length + 2;
                //1 байт на стартовую команду, 2 - на обозначение длины, 4 на каждую частоту, 2 на множитель ramp
                byte[] data_Own_UsbBuf = new byte[totalcount];
                data_Own_UsbBuf[0] = MainCommands.USER_CURVE; //стартовый байт для перестройки по заданной кривой частот.

                byte[] data_L = Helper.Processing.uInt_to_2bytes((uint)(4 * Freq_mass_hz.Length + 2));
                data_Own_UsbBuf[1] = data_L[0];
                data_Own_UsbBuf[2] = data_L[1];

                byte[] data_iFreq = new byte[4];

                ulong data_lvspom;
                for(int i = 0;i< Freq_mass_hz.Length;i++)
                {
                    data_lvspom = (ulong)((Freq_mass_hz[i]) * (Math.Pow(2.0, 32.0) / 350e6));
                    data_iFreq = Helper.Processing.uLong_to_4bytes(data_lvspom);
                    data_Own_UsbBuf[2 + i * 4 + 1] = data_iFreq[0];
                    data_Own_UsbBuf[2 + i * 4 + 2] = data_iFreq[1];
                    data_Own_UsbBuf[2 + i * 4 + 3] = data_iFreq[2];
                    data_Own_UsbBuf[2 + i * 4 + 4] = data_iFreq[3];
                }
                byte[] data_T = Helper.Processing.uInt_to_2bytes((uint)time_multiplier);
                data_Own_UsbBuf[2 + 4 * Freq_mass_hz.Length + 1] = data_T[0];
                data_Own_UsbBuf[2 + 4 * Freq_mass_hz.Length + 2] = data_T[1];

                return data_Own_UsbBuf;

            }
          
        

            public override int Set_Sweep_on(float MHz_start, float Sweep_range_MHz, double Period/*[мкс с точностью до двух знаков,минимум 1]*/, bool OnRepeat)
            {
                //здесь MHz_start = m_f0 - начальна частота в МГц    
                //Sweep_range_MHz = m_deltaf - девиация частоты в МГц
                try
                {

                    Own_UsbBuf = new byte[5000];
                    int count = 0;
                    int Multiplier = 0;
                   /* int[] HZMass = Calculate_sweep_params_012020(MHz_start, Sweep_range_MHz, Period, true, ref Multiplier);
                    Own_UsbBuf = Create_byteMass_byKnownParams_012020(HZMass, Multiplier);*/
                   
                  //  count = Own_UsbBuf.Count();
            
                    try
                    {
                        var code_er = FTDIController.FT_STATUS.FT_OK;
                        /*  var code_er = FTDIController.FT_ResetDevice(Own_m_hPort); //ResetDevice();
                          code_er = FTDIController.FT_Purge(Own_m_hPort, FTDIController.FT_PURGE_RX | FTDIController.FT_PURGE_TX); // все что было в буфере вычищается
                          code_er = FTDIController.FT_ResetDevice(Own_m_hPort); //ResetDevice();*/
                        if (code_er != FTDIController.FT_STATUS.FT_OK) throw new Exception("Error ib AO_lib on Set_Sweep_on");
                        WriteUsb(count);
                    }
                    catch { return (int)FTDIController_lib.FT_STATUS.FT_IO_ERROR; }
                    sAO_Sweep_On = true;
                    return (int)FTDIController_lib.FT_STATUS.FT_OK;
                }
                catch { return (int)FTDIController_lib.FT_STATUS.FT_OTHER_ERROR; }
            }

            public int Set_Sweep_on(float MHz_start, float Sweep_range_MHz, int steps,double time_up,double time_down)
            {
                if (Sweep_range_MHz > HZ_Max - HZ_Min ||
                    Sweep_range_MHz > AO_FreqDeviation_Max ||
                    Sweep_range_MHz < 0 ||
                    MHz_start < HZ_Min ||
                    MHz_start > HZ_Max || 
                    (time_up == 0 && time_down == 0) ||
                    steps <= 0)
                    throw new Exception("Invalid sweep parameters!");

                if (MHz_start + Sweep_range_MHz > HZ_Max)
                    MHz_start = HZ_Max - Sweep_range_MHz;

                //здесь MHz_start = m_f0 - начальна частота в МГц    
                //Sweep_range_MHz = m_deltaf - девиация частоты в МГц
                try
                {

                    Own_UsbBuf = new byte[5000];
                    int count = 0;
                    int Multiplier = 0;
                   // int[] HZMass = Calculate_sweep_params_012020(MHz_start, Sweep_range_MHz, Period, true, ref Multiplier);
                     Own_UsbBuf = Create_byteMass_byKnownParams_062020(MHz_start, Sweep_range_MHz, steps, time_up, time_down, !(time_down<1e-5));
                    // Calculate_sweep_params_062020
                    count = Own_UsbBuf.Count();
                    try
                    {
                        var code_er = FTDIController.FT_STATUS.FT_OK;
                        /*  var code_er = FTDIController.FT_ResetDevice(Own_m_hPort); //ResetDevice();
                          code_er = FTDIController.FT_Purge(Own_m_hPort, FTDIController.FT_PURGE_RX | FTDIController.FT_PURGE_TX); // все что было в буфере вычищается
                          code_er = FTDIController.FT_ResetDevice(Own_m_hPort); //ResetDevice();*/
                        if (code_er != FTDIController.FT_STATUS.FT_OK) throw new Exception("Error ib AO_lib on Set_Sweep_on");
                        WriteUsb(count);

                        //Запомним последние установленные значения
                        SweepParameters sweepLastParameters = new SweepParameters();
                        sweepLastParameters.steps = steps;
                        sweepLastParameters.Sweep_range = Sweep_range_MHz;
                        sweepLastParameters.time_up = time_up;
                        sweepLastParameters.time_down = time_down;
                        sweepLastParameters.f0 = MHz_start;
                        SweepLastParameters = sweepLastParameters;
                    }
                    catch { return (int)FTDIController_lib.FT_STATUS.FT_IO_ERROR; }
                    sAO_Sweep_On = true;
                    return (int)FTDIController_lib.FT_STATUS.FT_OK;
                }
                catch { return (int)FTDIController_lib.FT_STATUS.FT_OTHER_ERROR; }
            }

            public int SetHz_KeepSweep(float freq_MHz, bool keep = true)
            {
                if(sAO_Sweep_On && keep)
                {
                    //если свип включен, то устанавливаем последние его параметры с новой частотой
                    return Set_Sweep_on(freq_MHz,
                        SweepLastParameters.Sweep_range,
                        SweepLastParameters.steps,
                        SweepLastParameters.time_up,
                        SweepLastParameters.time_down);
                }
                else
                {
                    Set_Sweep_off();
                    return Set_Hz(freq_MHz);
                }
            }

            public int Set_MF_test(List<float> Fr, float uSec)
            {
                //здесь MHz_start = m_f0 - начальна частота в МГц    
                //Sweep_range_MHz = m_deltaf - девиация частоты в МГц
                try
                {

                    Own_UsbBuf = new byte[5000];
                   
                    Own_UsbBuf = Create_byteMass_MultiFrequencyMode_112020(Fr, uSec);
                    // Calculate_sweep_params_062020
                    int count = Own_UsbBuf.Count();
                    try
                    {
                        var code_er = FTDIController.FT_STATUS.FT_OK;
                        /*  var code_er = FTDIController.FT_ResetDevice(Own_m_hPort); //ResetDevice();
                          code_er = FTDIController.FT_Purge(Own_m_hPort, FTDIController.FT_PURGE_RX | FTDIController.FT_PURGE_TX); // все что было в буфере вычищается
                          code_er = FTDIController.FT_ResetDevice(Own_m_hPort); //ResetDevice();*/
                        if (code_er != FTDIController.FT_STATUS.FT_OK) throw new Exception("Error ib AO_lib on Set_Sweep_on");
                        WriteUsb(count);
                    }
                    catch { return (int)FTDIController_lib.FT_STATUS.FT_IO_ERROR; }
                    sAO_Sweep_On = true;
                    return (int)FTDIController_lib.FT_STATUS.FT_OK;
                }
                catch { return (int)FTDIController_lib.FT_STATUS.FT_OTHER_ERROR; }
            }

            public override int Set_Sweep_off()
            {
                return Set_Hz(HZ_Current);

            }

            protected override int Init_device(uint number)
            {
                AO_Devices.FTDIController_lib.FT_STATUS ftStatus = AO_Devices.FTDIController_lib.FT_STATUS.FT_OTHER_ERROR;

                if (Own_m_hPort == 0)
                {
                     ftStatus = AO_Devices.FTDIController_lib.FT_Open((uint)number, ref Own_m_hPort);                   
                }

                if (ftStatus == AO_Devices.FTDIController_lib.FT_STATUS.FT_OK)
                {
                    // Set up the port
                    var code_er = FTDIController_lib.FT_SetBaudRate(Own_m_hPort, 9600);
                    code_er = FTDIController_lib.FT_Purge(Own_m_hPort, FTDIController_lib.FT_PURGE_RX | FTDIController_lib.FT_PURGE_TX);
                    code_er = FTDIController_lib.FT_SetTimeouts(Own_m_hPort, 3000, 3000);
                    if (code_er != FTDIController_lib.FT_STATUS.FT_OK) throw new Exception("Error ib AO_lib on Set_Sweep_on");
                }
                else
                {
                    return (int)ftStatus;
                }
                Own_UsbBuf[0] = MainCommands.TEST;//пересылаем тестовый байт
                try { WriteUsb(1); }
                catch { return (int)FTDIController_lib.FT_STATUS.FT_IO_ERROR; }
                return 0;
            }
            protected unsafe int Init_device(string SerialNum_or_Name,bool UseSerial = true)
            {
                AO_Devices.FTDIController_lib.FT_STATUS ftStatus = AO_Devices.FTDIController_lib.FT_STATUS.FT_OTHER_ERROR;

                if (Own_m_hPort == 0)
                {
                    System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();

                    var a = enc.GetBytes(SerialNum_or_Name);
                    if(UseSerial)
                        fixed(byte* SerNumBytePointer = a)
                            ftStatus = AO_Devices.FTDIController_lib.FT_OpenEx(SerNumBytePointer, FTDIController.FT_OPEN_BY_SERIAL_NUMBER, ref Own_m_hPort);    
                    else
                        fixed (byte* SerNumBytePointer = a)
                            ftStatus = AO_Devices.FTDIController_lib.FT_OpenEx(SerNumBytePointer, FTDIController.FT_OPEN_BY_DESCRIPTION, ref Own_m_hPort);
                }

                if (ftStatus == AO_Devices.FTDIController_lib.FT_STATUS.FT_OK)
                {
                    // Set up the port
                    var code_er = FTDIController_lib.FT_SetBaudRate(Own_m_hPort, 9600);
                    code_er = FTDIController_lib.FT_Purge(Own_m_hPort, FTDIController_lib.FT_PURGE_RX | FTDIController_lib.FT_PURGE_TX);
                    code_er = FTDIController_lib.FT_SetTimeouts(Own_m_hPort, 3000, 3000);
                    if (code_er != FTDIController_lib.FT_STATUS.FT_OK) throw new Exception("Error ib AO_lib on Init_device");
                }
                else
                {
                    return (int)ftStatus;
                }
                Own_UsbBuf[0] = MainCommands.TEST;//пересылаем тестовый байт
                try { WriteUsb(1); }
                catch { return (int)FTDIController_lib.FT_STATUS.FT_IO_ERROR; }
                return 0;
            }



            public override string Ask_required_dev_file()
            {
                return ("(special *.dev file)");
            }
          
            public static unsafe int Search_Devices(out string[] FilterNames, out string[] FilterSerials)
            {
                FTDIController_lib.FT_STATUS ftStatus = FTDIController_lib.FT_STATUS.FT_OTHER_ERROR;
                UIntXX numDevs;
                int countofdevs_to_return = 0;
                uint i; int NumberOfSym_max = 64;
                void* p1 = (void*)&numDevs;

                ftStatus = FTDIController_lib.FT_ListDevices(p1, null, FTDIController_lib.FT_LIST_NUMBER_ONLY);
                countofdevs_to_return = (int)numDevs;

                var ListDescFlag = FTDIController_lib.FT_LIST_BY_INDEX_OPEN_BY_DESCRIPTION;
                var ListSerialFlag = FTDIController_lib.FT_LIST_BY_INDEX_OPEN_BY_SERIAL;

                FilterNames = new string[numDevs];
                FilterSerials = new string[numDevs];
                List<string> FilterNames_real = new List<string>();
                List<string> FilterSerials_real = new List<string>();
                List<byte[]> sDevNames = new List<byte[]>();
                List<byte[]> sDevSerials = new List<byte[]>();

                if (ftStatus == FTDIController_lib.FT_STATUS.FT_OK)
                {
                    for (i = 0; i < numDevs; i++) // пройдемся по девайсам и спросим у них дескрипторы
                    {
                        sDevNames.Add(new byte[NumberOfSym_max]);
                        sDevSerials.Add(new byte[NumberOfSym_max]);

                        fixed (byte* pBuf_name = sDevNames[(int)i])
                        {
                            fixed (byte* pBuf_serial = sDevSerials[(int)i])
                            {
                                ftStatus = FTDIController_lib.FT_ListDevices((UIntXX)i, pBuf_name, ListDescFlag);
                                ftStatus = FTDIController_lib.FT_ListDevices((UIntXX)i, pBuf_serial, ListSerialFlag);
                                if (ftStatus == FTDIController_lib.FT_STATUS.FT_OK)
                                {
                                    System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
                                    FilterNames[i] = enc.GetString(sDevNames[(int)i], 0, NumberOfSym_max);
                                    FilterSerials[i] = enc.GetString(sDevSerials[(int)i], 0, NumberOfSym_max);
                                    if (!FilterNames[i].Contains("Deflector")) //игнорируем подключенные дефлекторы
                                    {
                                        FilterNames_real.Add(Helper.Processing.RemoveZeroBytesFromString(FilterNames[i]));
                                        FilterSerials_real.Add(Helper.Processing.RemoveZeroBytesFromString(FilterSerials[i]));
                                    }
                                    else countofdevs_to_return--;
                                }
                                else
                                {
                                    FilterNames = null;
                                    return (int)ftStatus;
                                }
                            }
                        }
                    }
                }
                FilterNames = FilterNames_real.ToArray();
                FilterSerials = FilterSerials_real.ToArray();
                return countofdevs_to_return;
            }
            public override string Implement_Error(int pCode_of_error)
            {
                return ((FTDIController_lib.FT_STATUS)pCode_of_error).ToString();
            }

           
           
#region Перегрузки WriteUsb
            //Перегрузки, которую можно юзать
            public unsafe bool WriteUsb()
            {
                int count_in = Own_UsbBuf.Length;
                return AO_Devices.FTDIController_lib.WriteUsb(Own_m_hPort,count_in, Own_UsbBuf);
            }

            //Перегрузка, которую юзаем везде
            public unsafe bool WriteUsb(int count)
            { return AO_Devices.FTDIController_lib.WriteUsb(Own_m_hPort, count, Own_UsbBuf); }
            public unsafe bool WriteUsb(byte[] ByteMass,int count)
            { return AO_Devices.FTDIController_lib.WriteUsb(Own_m_hPort, count, ByteMass); }

            #endregion
        }

        private static class FTDIController_lib
        {
#if X86
            const string ftdi_dllname = "FTD2XX.dll";
#endif
#if X64
            const string ftdi_dllname = "ftd2xx64.dll";
#endif

            public enum FT_STATUS//:Uint32
            {
                FT_OK = 0,
                FT_INVALID_HANDLE,
                FT_DEVICE_NOT_FOUND,
                FT_DEVICE_NOT_OPENED,
                FT_IO_ERROR,
                FT_INSUFFICIENT_RESOURCES,
                FT_INVALID_PARAMETER,
                FT_INVALID_BAUD_RATE,
                FT_DEVICE_NOT_OPENED_FOR_ERASE,
                FT_DEVICE_NOT_OPENED_FOR_WRITE,
                FT_FAILED_TO_WRITE_DEVICE,
                FT_EEPROM_READ_FAILED,
                FT_EEPROM_WRITE_FAILED,
                FT_EEPROM_ERASE_FAILED,
                FT_EEPROM_NOT_PRESENT,
                FT_EEPROM_NOT_PROGRAMMED,
                FT_INVALID_ARGS,
                FT_OTHER_ERROR,
                FT_POWER_PROBLEM // 08.02.2021. In case of problems with connection of the power supply or smth else
            };

            public const UIntXX FT_BAUD_300 = 300;
            public const UIntXX FT_BAUD_600 = 600;
            public const UIntXX FT_BAUD_1200 = 1200;
            public const UIntXX FT_BAUD_2400 = 2400;
            public const UIntXX FT_BAUD_4800 = 4800;
            public const UIntXX FT_BAUD_9600 = 9600;
            public const UIntXX FT_BAUD_14400 = 14400;
            public const UIntXX FT_BAUD_19200 = 19200;
            public const UIntXX FT_BAUD_38400 = 38400;
            public const UIntXX FT_BAUD_57600 = 57600;
            public const UIntXX FT_BAUD_115200 = 115200;
            public const UIntXX FT_BAUD_230400 = 230400;
            public const UIntXX FT_BAUD_460800 = 460800;
            public const UIntXX FT_BAUD_921600 = 921600;

            public const UIntXX FT_LIST_NUMBER_ONLY = 0x80000000;
            public const UIntXX FT_LIST_BY_INDEX = 0x40000000;
            public const UIntXX FT_LIST_ALL = 0x20000000;
            public const UIntXX FT_OPEN_BY_SERIAL_NUMBER = 1;
            public const UIntXX FT_OPEN_BY_DESCRIPTION = 2;

            public const UIntXX FT_LIST_BY_INDEX_OPEN_BY_DESCRIPTION = FT_LIST_BY_INDEX | FT_OPEN_BY_DESCRIPTION;
            public const UIntXX FT_LIST_BY_INDEX_OPEN_BY_SERIAL = FT_LIST_BY_INDEX | FT_OPEN_BY_SERIAL_NUMBER;

            // Word Lengths
            public const byte FT_BITS_8 = 8;
            public const byte FT_BITS_7 = 7;
            public const byte FT_BITS_6 = 6;
            public const byte FT_BITS_5 = 5;

            // Stop Bits
            public const byte FT_STOP_BITS_1 = 0;
            public const byte FT_STOP_BITS_1_5 = 1;
            public const byte FT_STOP_BITS_2 = 2;

            // Parity
            public const byte FT_PARITY_NONE = 0;
            public const byte FT_PARITY_ODD = 1;
            public const byte FT_PARITY_EVEN = 2;
            public const byte FT_PARITY_MARK = 3;
            public const byte FT_PARITY_SPACE = 4;

            // Flow Control
            public const UInt16 FT_FLOW_NONE = 0;
            public const UInt16 FT_FLOW_RTS_CTS = 0x0100;
            public const UInt16 FT_FLOW_DTR_DSR = 0x0200;
            public const UInt16 FT_FLOW_XON_XOFF = 0x0400;

            // Purge rx and tx buffers
            public const byte FT_PURGE_RX = 1;
            public const byte FT_PURGE_TX = 2;

            // Events
            public const UIntXX FT_EVENT_RXCHAR = 1;
            public const UIntXX FT_EVENT_MODEM_STATUS = 2;

           
            //public static byte* pBuf;
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_ListDevices(void* pvArg1, void* pvArg2, UIntXX dwFlags);  // FT_ListDevices by number only
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_ListDevices(UIntXX pvArg1, void* pvArg2, UIntXX dwFlags); // FT_ListDevcies by serial number or description by index only
            [DllImport(ftdi_dllname)]
            public static extern FT_STATUS FT_Open(UIntXX uiPort, ref FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_OpenEx(void* pvArg1, UIntXX dwFlags, ref FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            public static extern FT_STATUS FT_Close(FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_Read(FT_HANDLE ftHandle, void* lpBuffer, UIntXX dwBytesToRead, ref UIntXX lpdwBytesReturned);
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_Write(FT_HANDLE ftHandle, void* lpBuffer, UIntXX dwBytesToRead, ref UIntXX lpdwBytesWritten);
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_SetBaudRate(FT_HANDLE ftHandle, UIntXX dwBaudRate);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetDataCharacteristics(FT_HANDLE ftHandle, byte uWordLength, byte uStopBits, byte uParity);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetFlowControl(FT_HANDLE ftHandle, char usFlowControl, byte uXon, byte uXoff);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetDtr(FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_ClrDtr(FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetRts(FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_ClrRts(FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_GetModemStatus(FT_HANDLE ftHandle, ref UIntXX lpdwModemStatus);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetChars(FT_HANDLE ftHandle, byte uEventCh, byte uEventChEn, byte uErrorCh, byte uErrorChEn);
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_Purge(FT_HANDLE ftHandle, UIntXX dwMask);
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_SetTimeouts(FT_HANDLE ftHandle, UIntXX dwReadTimeout, UIntXX dwWriteTimeout);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_GetQueueStatus(FT_HANDLE ftHandle, ref UIntXX lpdwAmountInRxQueue);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetBreakOn(FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetBreakOff(FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_GetStatus(FT_HANDLE ftHandle, ref UIntXX lpdwAmountInRxQueue, ref UIntXX lpdwAmountInTxQueue, ref UIntXX lpdwEventStatus);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetEventNotification(FT_HANDLE ftHandle, UIntXX dwEventMask, void* pvArg);
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_ResetDevice(FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetDivisor(FT_HANDLE ftHandle, char usDivisor);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_GetLatencyTimer(FT_HANDLE ftHandle, ref byte pucTimer);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetLatencyTimer(FT_HANDLE ftHandle, byte ucTimer);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_GetBitMode(FT_HANDLE ftHandle, ref byte pucMode);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetBitMode(FT_HANDLE ftHandle, byte ucMask, byte ucEnable);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetUSBParameters(FT_HANDLE ftHandle, UIntXX dwInTransferSize, UIntXX dwOutTransferSize);

            //Сама функция
#if X86
            public static unsafe bool WriteUsb(UInt32 pm_hPort, int count, byte[] pUsbBuf)
#elif X64
            public static unsafe bool WriteUsb(UInt64 pm_hPort, int count, byte[] pUsbBuf)
#endif          
            {
                UIntXX dwRet = 0;
                FTDIController_lib.FT_STATUS ftStatus = FTDIController_lib.FT_STATUS.FT_OTHER_ERROR;
                byte[] cBuf = new Byte[count + 1];

                fixed (byte* pBuf = pUsbBuf)
                {
                    ftStatus = FTDIController_lib.FT_Write(pm_hPort, pBuf, (uint)(count + 1), ref dwRet);
                }
                if (ftStatus != FTDIController_lib.FT_STATUS.FT_OK)
                {
                    //MessageBox.Show("Failed To Write " + Convert.ToString(ftStatus));
                    return false;
                }
                else return true;
            }
            public static unsafe bool WriteUsb(uint pm_hPort, byte[] pUsbBuf)
            {
                int count_in = pUsbBuf.Length;
                return AO_Devices.FTDIController_lib.WriteUsb(pm_hPort, count_in, pUsbBuf);
            }

            public static int Bit_reverse(int input,bool isBitInvNeeded = false)
            {
                int output = 0;
                const int uchar_size = 8;
                if (isBitInvNeeded)
                {
                    for (int i = 0; i != uchar_size; ++i)
                    {
                        output |= ((input >> i) & 1) << (uchar_size - 1 - i);
                    }
                }
                else
                { output = input; }
                return output;
            }


        }
        
        public enum FilterTypes
        {
            Emulator = 0,
            VNIIFTRI_Filter_v15,
            VNIIFTRI_Filter_v20,
            STC_Filter,
            EthernetFilter
        }

    }
}