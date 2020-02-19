using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Drawing;
using FT_HANDLE = System.UInt32;
//using aom; 

namespace AO_Lib
{
    public static partial class AO_Devices
    {
        abstract public class AO_Filter : IDisposable
        {
            public abstract FilterTypes FilterType { get; }

            //Все о фильтре: дескриптор(имя), полное и краткое имя dev файла и управляющая dll
            protected abstract string FilterDescriptor_or_name { set; get; }
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
            protected virtual float sHZ_Current { set; get; }
            protected virtual float sWL_Current { set; get; }
            public float WL_Current { get { return sWL_Current; } }
            public float HZ_Current { get { return sHZ_Current; } }
      

            //все о свипе
            protected abstract bool sAO_Sweep_On { set; get; }
            public bool is_inSweepMode { get { return sAO_Sweep_On; } }

            public virtual float AO_ExchangeRate_Min { get { return 500; } } //[Гц]
            public virtual float AO_ExchangeRate_Max { get { return 4500; } } //[Гц]
            public virtual float AO_ProgrammMode_step { get { return 500; } } //[кГц/шаг]
            public virtual float AO_TimeDeviation_Min { get { return 5; } }   // [мс]     
            public virtual float AO_TimeDeviation_Max { get { return 40; } } // [мс]
            public virtual float AO_FreqDeviation_Min { get { return 0.5f; } } // [МГц]
            public virtual float AO_FreqDeviation_Max { get { return 5.0f; } }// [МГц]
            public virtual bool Bit_inverse_needed { get { return false; } }
            protected virtual bool sBit_inverse_needed { set; get; }
            //все о программируемой перестройке
            protected abstract bool sAO_ProgrammMode_Ready { set; get; }
            public bool is_Programmed { get { return sAO_ProgrammMode_Ready; } }
            //функционал

            //перестройка ДВ пропускания
            public abstract int Set_Wl(float pWL);
            public abstract int Set_Hz(float freq);

            public abstract int Set_OutputPower(byte percentage);

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
                    var Data_from_dev = MiniHelp.Files.Read_txt(path);
                    

                    sBit_inverse_needed = Data_from_dev[0].Contains("true") ? true : false;
                    if (Data_from_dev[0].Contains("true") || Data_from_dev[0].Contains("false")) Data_from_dev.RemoveAt(0);
                    FilterCfgPath = path;
                    FilterCfgName = System.IO.Path.GetFileName(path);
                    float[] pWLs, pHZs, pCoefs;
                    MiniHelp.Files.Get_WLData_byKnownCountofNumbers(3, Data_from_dev.ToArray(), out pWLs, out pHZs, out pCoefs);

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
                    MiniHelp.Math.Interpolate_curv(ref pWLs, ref pHZs);
                    MiniHelp.Math.Interpolate_curv(ref pData, ref pCoefs);

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

            public abstract void Dispose();

            public virtual float Get_HZ_via_WL(float pWL)
            {
                float distance = (pWL - WL_Min);
                if ((distance < (WLs.Length)) && (distance >= 0))
                {
                    int a = (int)distance;
                    if ((distance - a) < 1e6f)  { return HZs[a]; }
                    else { return (float)MiniHelp.Math.Interpolate_value(WLs[a], HZs[a], WLs[a+1], HZs[a+1], pWL); } //Проблема здесь 
                }
                else
                {
                    if (distance < 0) return HZs[0];
                    else return HZs[HZs.Length - 1];
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
                        return (float)MiniHelp.Math.Interpolate_value(HZs[rem_pos], WLs[rem_pos], HZs[rem_pos + 1], WLs[rem_pos + 1], pHZ);
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
                    else { return (float)MiniHelp.Math.Interpolate_value(WLs[a], Intensity[a], WLs[a + 1], Intensity[a + 1], pWL); }
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
                        result = (float)MiniHelp.Math.Interpolate_value(HZs[rem_pos], Intensity[rem_pos], HZs[rem_pos + 1], Intensity[rem_pos + 1], pHZ);
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
                List<System.Drawing.PointF> PtsFinal = MiniHelp.Math.Interpolate_curv(Wls, Hzs);
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
            public static AO_Filter Find_and_connect_AnyFilter(bool IsEmulator = false)
            {
                if (IsEmulator) return (new Emulator());

                int NumberOfTypes = 3;
                int[] Devices_per_type = new int[NumberOfTypes];

                string Descriptor_forSTCFilter; uint Flag_forSTC_filter;
                Devices_per_type[0] = STC_Filter.Search_Devices(out Descriptor_forSTCFilter, out Flag_forSTC_filter);
                AO_Filter retFil = null;
#if X64
                if (Devices_per_type[0] != 0)
                    retFil =  (new STC_Filter(Descriptor_forSTCFilter, (uint)(Devices_per_type[0] - 1), Flag_forSTC_filter));
                else
                    retFil =  (new Emulator());
#elif X86
                Devices_per_type[1] = VNIIFTRI_Filter_v15.Search_Devices();
                Devices_per_type[2] = VNIIFTRI_Filter_v20.Search_Devices();
                if (Devices_per_type[0] != 0) retFil =  (new STC_Filter(Descriptor_forSTCFilter, (uint)(Devices_per_type[0] - 1), Flag_forSTC_filter));

                else if (Devices_per_type[1] != 0) retFil =  (new VNIIFTRI_Filter_v15());
                else if (Devices_per_type[2] != 0) retFil =  (new VNIIFTRI_Filter_v20());
                else retFil =  (new Emulator());
#endif
                return retFil;
            }
        }
        public class Emulator : AO_Filter
        {
            public override FilterTypes FilterType { get { return FilterTypes.Emulator; } }

            protected override string FilterDescriptor_or_name { set; get; }
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

            public Emulator()
            {
                sAO_ProgrammMode_Ready = false;
            }
            ~Emulator()
            {
                this.PowerOff();
                this.Dispose();
            }
            public override int Set_Wl(float pWL)
            {
                sWL_Current = pWL;
                sHZ_Current = Get_HZ_via_WL(pWL);
                return 0;
            }
            public override int Set_Hz(float freq)
            {
                sWL_Current = Get_WL_via_HZ(freq);
                sHZ_Current = freq;
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
            public override int Set_OutputPower(byte percentage)
            {
                return 0;
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
        }

#if X86
        public class VNIIFTRI_Filter_v15 : AO_Filter //идея: сделать 2 класса чисто на импорт, а обвязку оставить общую
        {
            public override FilterTypes FilterType { get { return FilterTypes.VNIIFTRI_Filter_v15; } }

            protected override string FilterDescriptor_or_name { set; get; }
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

            public VNIIFTRI_Filter_v15()
            {
                Init_device(0);
                sAO_ProgrammMode_Ready = false;
            }
            ~VNIIFTRI_Filter_v15()
            {
                this.PowerOff();
                this.Dispose();
            }

            public override int Set_Wl(float pWL)
            {
                sWL_Current = pWL;
                sHZ_Current = Get_HZ_via_WL(pWL);
                return AOM_SetWL(pWL);
            }
            public override int Set_Hz(float freq)
            {
                sWL_Current = Get_WL_via_HZ(freq);
                sHZ_Current = freq;
                return AOM_SetWL((int)Math.Round(sWL_Current));
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
            public override int Set_OutputPower(byte percentage)
            {
                return -1;
            }
            public override int Read_dev_file(string path)
            {
                float min=0, max=0;
                try
                {
                    var Data_from_dev = MiniHelp.Files.Read_txt(path);
                    float[] pWLs, pHZs, pCoefs;
                    MiniHelp.Files.Get_WLData_byKnownCountofNumbers(3, Data_from_dev.ToArray(), out pWLs, out pHZs, out pCoefs);
                    MiniHelp.Math.Interpolate_curv(pWLs, pHZs);
                    MiniHelp.Math.Interpolate_curv(pWLs, pCoefs);
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

            protected override string FilterDescriptor_or_name { set; get; }
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

            public VNIIFTRI_Filter_v20()
            {
                Init_device(0);
                sAO_ProgrammMode_Ready = false;
            }
            ~VNIIFTRI_Filter_v20()
            {
                this.PowerOff();
                this.Dispose();
            }

            public override int Set_Wl(float pWL)
            {
                sWL_Current = pWL;
                sHZ_Current = Get_HZ_via_WL(pWL);
                return AOM_SetWL(pWL);
            }
            public override int Set_Hz(float freq)
            {
                sWL_Current = Get_WL_via_HZ(freq);
                sHZ_Current = freq;
                return AOM_SetWL((int)Math.Round(sWL_Current));
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
            public override int Set_OutputPower(byte percentage)
            {
                return -1;
            }
            public override int Read_dev_file(string path)
            {
                float min = 0, max = 0;
                try
                {
                    var Data_from_dev = MiniHelp.Files.Read_txt(path);
                    float[] pWLs, pHZs, pCoefs;
                    MiniHelp.Files.Get_WLData_byKnownCountofNumbers(3, Data_from_dev.ToArray(), out pWLs, out pHZs, out pCoefs);
                    MiniHelp.Math.Interpolate_curv(pWLs, pHZs);
                    MiniHelp.Math.Interpolate_curv(pWLs, pCoefs);
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

        public class STC_Filter : AO_Filter
        {
            public override FilterTypes FilterType { get { return FilterTypes.STC_Filter; } }

            protected override string FilterDescriptor_or_name { set; get; }
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
            private UInt32 Own_dwListDescFlags = 0;
            private UInt32 Own_m_hPort = 0;
            public override bool Bit_inverse_needed { get { return sBit_inverse_needed; } }

            /// <summary>
            /// Конструктор. Инициализирует экземляр класса по номеру (и дескриптору) физического АО фильтра
            /// </summary>
            public STC_Filter(string Descriptor,uint number,FT_HANDLE ListFlag) 
            {
                Own_dwListDescFlags = ListFlag;
                FilterDescriptor_or_name = Descriptor;
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
            }
           
            /// <summary>
            /// Деструктор
            /// </summary>
            ~STC_Filter()
            {
                this.PowerOff();
                this.Dispose();
            }
         
            /// <summary>
            /// Устанавливает на АОФ заданную длину волны в нм.
            /// </summary>
            public override int Set_Wl(float pWL)
            {
                if (AOF_Loaded_without_fails)
                {
                    try
                    {
                        float freq = Get_HZ_via_WL(pWL);
                        sWL_Current = pWL;
                        sHZ_Current = Get_HZ_via_WL(pWL);
                        return (this.Set_Hz(freq));
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
                if (AOF_Loaded_without_fails)
                {
                    try
                    {
                        Own_UsbBuf = Create_byteMass_forHzTune(freq);
                        WriteUsb(7);
                        sWL_Current = Get_WL_via_HZ(freq);
                        sHZ_Current = freq;
                        return 0;
                    }
                    catch (Exception exc)
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
            /// Устанавливает частоту на АОФ при помощи заранее вычисленного массива байт.
            /// Для вычисления массива необходимо использовать функцию Create_byteMass_forHzTune
            /// </summary>
            public int Set_Hz_via_bytemass(byte[] buf)
            {
                try
                {
                    WriteUsb(buf, 7);
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
                        WriteUsb(7);//установка частоты происходит фактически тут
                        sWL_Current = Get_WL_via_HZ(freq);
                        sHZ_Current = freq;
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

                data_Own_UsbBuf[0] = 0x03; //it means, we will send wavelength

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
            public int Set_ProgrammMode_on()
            {
                try
                {                   
                    WriteUsb(Own_ProgrammBuf, Own_ProgrammBuf.Length);
                }
                catch { return (int)FTDIController_lib.FT_STATUS.FT_IO_ERROR; }
                return (int)FTDIController_lib.FT_STATUS.FT_OK;
            }

            /// <summary>
            /// Программируемый режим. Деактивирует программируемый режим. Устанавливается центральная частота.
            /// </summary>  
            public int Set_ProgrammMode_off()
            {

              //  is_Programmed = false;
                return Set_Hz((HZ_Max + HZ_Min) / 2.0f);
            }

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
                //
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
            /// Пересчитывает заданные пользователем параметры свипинга в массив конечных данных
            /// </summary>
            /// <param name="pMHz_start">Начальная частота в МГц</param>
            /// <param name="pSweep_range_MHz">Диапазон варьирования. Максимум 5 МГц (012020)</param>
            /// <param name="pPeriod">Временной интервал, в течение которого необходимо провести один цикл изменения частот, мкс. Минимум - 0,571 мкс (012020)</param>
            /// <param name="pMHz_start">Форма профиля одно цикла: 0 - равнобедренный треугольник, 1 - прямоугольный треугольник</param>
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
            public static byte[] Create_byteMass_byKnownParams_012020(int[] Freq_mass_hz, int time_multiplier)
            {
                int totalcount = 1 + 2 + 4 * Freq_mass_hz.Length + 2;
                //1 байт на стартовую команду, 2 - на обозначение длины, 4 на каждую частоту, 2 на множитель ramp
                byte[] data_Own_UsbBuf = new byte[totalcount];
                data_Own_UsbBuf[0] = 0xC0;

                byte[] data_L = uInt_to_2bytes((uint)(4 * Freq_mass_hz.Length + 2));
                data_Own_UsbBuf[1] = data_L[0];
                data_Own_UsbBuf[2] = data_L[1];

                byte[] data_iFreq = new byte[4];

                ulong data_lvspom;
                for(int i = 0;i< Freq_mass_hz.Length;i++)
                {
                    data_lvspom = (ulong)((Freq_mass_hz[i]) * (Math.Pow(2.0, 32.0) / 350e6));
                    data_iFreq = uLong_to_4bytes(data_lvspom);
                    data_Own_UsbBuf[2 + i * 4 + 1] = data_iFreq[0];
                    data_Own_UsbBuf[2 + i * 4 + 2] = data_iFreq[1];
                    data_Own_UsbBuf[2 + i * 4 + 3] = data_iFreq[2];
                    data_Own_UsbBuf[2 + i * 4 + 4] = data_iFreq[3];
                }
                byte[] data_T = uInt_to_2bytes((uint)time_multiplier);
                data_Own_UsbBuf[2 + 4 * Freq_mass_hz.Length + 1] = data_T[0];
                data_Own_UsbBuf[2 + 4 * Freq_mass_hz.Length + 2] = data_T[1];

                return data_Own_UsbBuf;

            }
          
            public static byte[] uLong_to_4bytes(ulong lvspom)
            {
                short MSB, LSB;
                byte[] result = new byte[4];
                MSB = (short)(0x0000ffFF & (lvspom >> 16));
                LSB = (short)lvspom;
                result[0] = (byte)(0x00ff & (MSB >> 8));
                result[1] = (byte)(MSB);
                result[2] = (byte)(0x00ff & (LSB >> 8));
                result[3] = (byte)(LSB);
                return result;
            }
            public static byte[] uInt_to_2bytes(ulong ivspom)
            {
                byte[] result = new byte[2];
                result[0] = (byte)(0x00ff & (ivspom >> 8)); ;
                result[1] = (byte)ivspom;
                return result;
            }

            public/* override */int Set_Sweep_on_test(float MHz_start, float Sweep_range_MHz, double Period/*[мс с точностью до двух знаков,минимум 1]*/, bool OnRepeat)
            {
                //здесь MHz_start = m_f0 - начальна частота в МГц    
                //Sweep_range_MHz = m_deltaf - девиация частоты в МГц
                try
                {

                    float freq, fvspom;
                    short MSB, LSB;
                    ulong lvspom;
                    byte[] tx = new byte[5000];
                    uint ivspom;
                    float delta;
                    int steps, count;
                    int i, j; float freqMCU = 74.99e6f;

                    float inp_freq = 5000; //in Hz, max 5000Hz
                    count = 4;
                    //steps=(float)m_deltaf*5;
                    tx[0] = 0x14;
                    tx[1] = 0x11;
                    tx[2] = 0x12; //it means, we will send wavelength
                    tx[3] = 0xff; //repeat cont
                    for (i = 4; i < 5000; i++)
                    {
                        tx[i] = 0;
                    }
                    for (j = 1; j <= 2; j++)
                    {
                        //timer calculations
                        ivspom = (uint)(65536 - freqMCU / (2 * 2 * inp_freq));
                        tx[count] = (byte)(0x00ff & (ivspom >> 8)); count++;    //timer calculations and bytes
                        tx[count] = (byte)(ivspom); count++;                    //timer calculations and bytes
                        for (i = 0; i < 20; i++)
                        {
                            freq = (float)((60 +30*j) * 1e6 + i * 2e5); //in Hz
                            lvspom = (ulong)((freq) * (Math.Pow(2.0, 32.0) / 350e6));
                            MSB = (short)(0x0000ffFF & (lvspom >> 16));
                            LSB = (short)lvspom;
                            tx[count] = (byte)(0x00ff & (MSB >> 8));
                            tx[count + 1] = (byte)(MSB);
                            tx[count + 2] = (byte)(0x00ff & (LSB >> 8));
                            tx[count + 3] = (byte)(LSB);
                            count += 4;
                        }
                    }
                    tx[count] = 0x15;
                    tx[++count] = 0x15;
                    tx[++count] = 0x15;
                    //count+=3;
                    for (i = 0; i < 5000; i++)
                    {
                        tx[i] = (byte)FTDIController_lib.Bit_reverse(tx[i], false);
                    }
                    FTDIController_lib.FT_ResetDevice(Own_m_hPort); //ResetDevice();
                    FTDIController_lib.FT_Purge(Own_m_hPort, FTDIController_lib.FT_PURGE_RX | FTDIController_lib.FT_PURGE_TX); // Purge(FT_PURGE_RX || FT_PURGE_TX);
                    FTDIController_lib.FT_ResetDevice(Own_m_hPort); //ResetDevice();
                    try
                    {
                        WriteUsb(tx, count+1);
                        return 0;
                    }
                    catch { return (int)FTDIController_lib.FT_STATUS.FT_IO_ERROR; }
                }
                catch { return (int)FTDIController_lib.FT_STATUS.FT_OTHER_ERROR; }
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
                    int[] HZMass = Calculate_sweep_params_012020(MHz_start, Sweep_range_MHz, Period, true, ref Multiplier);
                    Own_UsbBuf = Create_byteMass_byKnownParams_012020(HZMass, Multiplier);

               /*      FTDIController.FT_ResetDevice(Own_m_hPort); //ResetDevice();
                    FTDIController.FT_Purge(Own_m_hPort, FTDIController.FT_PURGE_RX | FTDIController.FT_PURGE_TX); // Purge(FT_PURGE_RX || FT_PURGE_TX);
                    FTDIController.FT_ResetDevice(Own_m_hPort); //ResetDevice();*/
                    try
                    {
                     /*   FTDIController.FT_ResetDevice(Own_m_hPort); //ResetDevice();
                        FTDIController.FT_Purge(Own_m_hPort, FTDIController.FT_PURGE_RX | FTDIController.FT_PURGE_TX); // все что было в буфере вычищается
                        FTDIController.FT_ResetDevice(Own_m_hPort); //ResetDevice();*/

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
               /* try
                {
                    //DWORD ret_bytes;
                    Own_UsbBuf[0] = 0x0a;
                    Own_UsbBuf[0] = (byte)AO_Devices.FTDIController.Bit_reverse(Own_UsbBuf[0]);

                    try { WriteUsb(1); }
                    catch { return (int)FTDIController.FT_STATUS.FT_IO_ERROR; }
                    PowerOff();
                }
                catch { return (int)FTDIController.FT_STATUS.FT_OTHER_ERROR; }
                sAO_Sweep_On = false;
                return (int)FTDIController.FT_STATUS.FT_OK;*/

            }

            public override int Set_OutputPower(byte Percentage)
            {
                try
                {
                    Own_UsbBuf[0] = 0x09; //it means, we will send off command
                    Own_UsbBuf[1] = Percentage;
                    try { WriteUsb(2); }
                    catch { return (int)FTDIController_lib.FT_STATUS.FT_IO_ERROR; }
                }
                catch { return (int)FTDIController_lib.FT_STATUS.FT_OTHER_ERROR; }
                return 0;
            }

            protected override int Init_device(uint number)
            {
                AO_Devices.FTDIController_lib.FT_STATUS ftStatus = AO_Devices.FTDIController_lib.FT_STATUS.FT_OTHER_ERROR;
                UInt32 dwOpenFlag;

                if (Own_m_hPort == 0)
                {
                    dwOpenFlag = Own_dwListDescFlags & ~AO_Devices.FTDIController_lib.FT_LIST_BY_INDEX;
                    dwOpenFlag = Own_dwListDescFlags & ~AO_Devices.FTDIController_lib.FT_LIST_ALL;
                    dwOpenFlag = 0;
                    if (dwOpenFlag == 0)
                    {
                        //uiCurrentIndex = (uint)cmbDevList.SelectedIndex;
                        ftStatus = AO_Devices.FTDIController_lib.FT_Open((uint)number, ref Own_m_hPort);
                    }
                    else
                    {
                        System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
                        byte[] sDevName = enc.GetBytes(FilterDescriptor_or_name);
                    }
                }

                if (ftStatus == AO_Devices.FTDIController_lib.FT_STATUS.FT_OK)
                {
                    // Set up the port
                    FTDIController_lib.FT_SetBaudRate(Own_m_hPort, 9600);
                    FTDIController_lib.FT_Purge(Own_m_hPort, FTDIController_lib.FT_PURGE_RX | FTDIController_lib.FT_PURGE_TX);
                    FTDIController_lib.FT_SetTimeouts(Own_m_hPort, 3000, 3000);
                }
                else
                {
                    return (int)ftStatus;
                }
                Own_UsbBuf[0] = 0x66;
                try { WriteUsb(1); }
                catch { return (int)FTDIController_lib.FT_STATUS.FT_IO_ERROR; }
                return 0;
            }

            protected override int Deinit_device()
            {
                System.Threading.Thread.Sleep(100);
                int result = Convert.ToInt16(AO_Devices.FTDIController_lib.FT_Close(Own_m_hPort));
                return result;
            }

            public override string Ask_required_dev_file()
            {
                return ("(special *.dev file)");
            }

            public static unsafe int Search_Devices(out string data_FilterDescriptor_or_name, out uint data_dwListDescFlags)
            {
                FTDIController_lib.FT_STATUS ftStatus = FTDIController_lib.FT_STATUS.FT_OTHER_ERROR;
                UInt32 numDevs;
                int countofdevs_to_return = 0;
                int i;
                byte[] sDevName = new byte[64];
                void* p1;             

                p1 = (void*)&numDevs;
                ftStatus = FTDIController_lib.FT_ListDevices(p1, null, FTDIController_lib.FT_LIST_NUMBER_ONLY);
                countofdevs_to_return = (int)numDevs;
                data_dwListDescFlags = FTDIController_lib.FT_LIST_BY_INDEX | FTDIController_lib.FT_OPEN_BY_DESCRIPTION;
                string datastring = "";
                if (ftStatus == FTDIController_lib.FT_STATUS.FT_OK)
                {
                    if (data_dwListDescFlags == FTDIController_lib.FT_LIST_ALL)
                    {
                        for (i = 0; i < numDevs; i++)
                        {
                            //cmbDevList.Items.Add(i);
                        }
                    }
                    else
                    {

                        for (i = 0; i < numDevs; i++) // пройдемся по девайсам и спросим у них дескрипторы
                        {
                            fixed (byte* pBuf = sDevName)
                            {
                                ftStatus = FTDIController_lib.FT_ListDevices((UInt32)i, pBuf, data_dwListDescFlags);
                                if (ftStatus == FTDIController_lib.FT_STATUS.FT_OK)
                                {
                                    //string str;
                                    System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
                                    datastring = enc.GetString(sDevName, 0, sDevName.Length);
                                    //cmbDevList.Items.Add(str);
                                }
                                else
                                {
                                    data_FilterDescriptor_or_name = null;
                                    return (int)ftStatus;
                                }
                            }
                        }
                    }
                }
                data_FilterDescriptor_or_name = datastring;
                return countofdevs_to_return;
            }

            public override string Implement_Error(int pCode_of_error)
            {
                return ((FTDIController_lib.FT_STATUS)pCode_of_error).ToString();
            }

            public override int PowerOn()
            {
                var state = Set_Hz((HZ_Max + HZ_Min) / 2);
                sAOF_isPowered = true;
                return state;
            }

            public override int PowerOff()
            {
                try
                {
                    System.Threading.Thread.Sleep(300);
                    Own_UsbBuf[0] = 0x05; //it means, we will send off command
                    
                    for (int i = 1; i < 2; i++) Own_UsbBuf[i] = 0;
                    Own_UsbBuf[0] = (byte)FTDIController_lib.Bit_reverse(Own_UsbBuf[0], Bit_inverse_needed);
                    try { WriteUsb(1); }
                    catch { return (int)FTDIController_lib.FT_STATUS.FT_IO_ERROR; }
                }
                catch { return (int)FTDIController_lib.FT_STATUS.FT_OTHER_ERROR; }
                sAOF_isPowered = false;
                return 0;
            }
           
            public override void Dispose()
            {
                Deinit_device();
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
            const string ftdi_dllname = "FTD2XX.dll";

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
                FT_OTHER_ERROR
            };

            public const UInt32 FT_BAUD_300 = 300;
            public const UInt32 FT_BAUD_600 = 600;
            public const UInt32 FT_BAUD_1200 = 1200;
            public const UInt32 FT_BAUD_2400 = 2400;
            public const UInt32 FT_BAUD_4800 = 4800;
            public const UInt32 FT_BAUD_9600 = 9600;
            public const UInt32 FT_BAUD_14400 = 14400;
            public const UInt32 FT_BAUD_19200 = 19200;
            public const UInt32 FT_BAUD_38400 = 38400;
            public const UInt32 FT_BAUD_57600 = 57600;
            public const UInt32 FT_BAUD_115200 = 115200;
            public const UInt32 FT_BAUD_230400 = 230400;
            public const UInt32 FT_BAUD_460800 = 460800;
            public const UInt32 FT_BAUD_921600 = 921600;

            public const UInt32 FT_LIST_NUMBER_ONLY = 0x80000000;
            public const UInt32 FT_LIST_BY_INDEX = 0x40000000;
            public const UInt32 FT_LIST_ALL = 0x20000000;
            public const UInt32 FT_OPEN_BY_SERIAL_NUMBER = 1;
            public const UInt32 FT_OPEN_BY_DESCRIPTION = 2;

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
            public const UInt32 FT_EVENT_RXCHAR = 1;
            public const UInt32 FT_EVENT_MODEM_STATUS = 2;

           
            //public static byte* pBuf;
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_ListDevices(void* pvArg1, void* pvArg2, UInt32 dwFlags);  // FT_ListDevices by number only
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_ListDevices(UInt32 pvArg1, void* pvArg2, UInt32 dwFlags); // FT_ListDevcies by serial number or description by index only
            [DllImport(ftdi_dllname)]
            public static extern FT_STATUS FT_Open(UInt32 uiPort, ref FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_OpenEx(void* pvArg1, UInt32 dwFlags, ref FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            public static extern FT_STATUS FT_Close(FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_Read(FT_HANDLE ftHandle, void* lpBuffer, UInt32 dwBytesToRead, ref UInt32 lpdwBytesReturned);
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_Write(FT_HANDLE ftHandle, void* lpBuffer, UInt32 dwBytesToRead, ref UInt32 lpdwBytesWritten);
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_SetBaudRate(FT_HANDLE ftHandle, UInt32 dwBaudRate);
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
            static extern unsafe FT_STATUS FT_GetModemStatus(FT_HANDLE ftHandle, ref UInt32 lpdwModemStatus);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetChars(FT_HANDLE ftHandle, byte uEventCh, byte uEventChEn, byte uErrorCh, byte uErrorChEn);
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_Purge(FT_HANDLE ftHandle, UInt32 dwMask);
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_SetTimeouts(FT_HANDLE ftHandle, UInt32 dwReadTimeout, UInt32 dwWriteTimeout);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_GetQueueStatus(FT_HANDLE ftHandle, ref UInt32 lpdwAmountInRxQueue);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetBreakOn(FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetBreakOff(FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_GetStatus(FT_HANDLE ftHandle, ref UInt32 lpdwAmountInRxQueue, ref UInt32 lpdwAmountInTxQueue, ref UInt32 lpdwEventStatus);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetEventNotification(FT_HANDLE ftHandle, UInt32 dwEventMask, void* pvArg);
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
            static extern unsafe FT_STATUS FT_SetUSBParameters(FT_HANDLE ftHandle, UInt32 dwInTransferSize, UInt32 dwOutTransferSize);

            //Сама функция
            public static unsafe bool WriteUsb(uint pm_hPort, int count, byte[] pUsbBuf)
            {
                UInt32 dwRet = 0;
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
            STC_Filter
        }

        private static class MiniHelp
        {
            public class Math
            {
                public static double Interpolate_value(double x1, double y1, double x2, double y2, double x_tofind)
                {
                    return ((y2 - y1) / (x2 - x1)) * (x_tofind - x1) + y1;
                }
                public static List<PointF> Interpolate_curv(float[] Wls, float[] Hzs)
                {
                    List<PointF> result = new List<PointF>();
                    int count_of_gaps = Wls.Length - 1;

                    for (int i = 0; i < count_of_gaps; i++)
                    {
                        result.Add(new PointF(Wls[i], Hzs[i]));
                        float count_of_wls_to_restruct = (Wls[i + 1] - Wls[i] - 1);
                        for (int j = 1; j <= count_of_wls_to_restruct; j++)
                        {
                            float new_hz_val = Hzs[i] + j * ((float)(Hzs[i + 1] - Hzs[i])) / (Wls[i + 1] - Wls[i]);// = (x- Hzs[i] )/ (float)j;
                            result.Add(new PointF(Wls[i] + j, new_hz_val));
                        }
                    }
                    result.Add(new PointF(Wls[count_of_gaps], Hzs[count_of_gaps]));
                    return result;
                }
                public static void Interpolate_curv(ref float[] Wls, ref float[] Hzs, float precision = 1f)
                {
                    List<PointF> result = new List<PointF>();
                    if (precision != 1)
                    {
                        for (int i = 0; i < Wls.Length; i++)
                        {
                            Wls[i] /= precision;
                        }
                    }
                    int count_of_gaps = Wls.Length - 1;

                    for (int i = 0; i < count_of_gaps; i++)
                    {
                        result.Add(new PointF(Wls[i], Hzs[i]));
                        float count_of_wls_to_restruct = (Wls[i + 1] - Wls[i] - 1);
                        for (int j = 1; j <= count_of_wls_to_restruct; j++)
                        {
                            float new_hz_val = Hzs[i] + j * ((float)(Hzs[i + 1] - Hzs[i])) / (Wls[i + 1] - Wls[i]);// = (x- Hzs[i] )/ (float)j;
                            result.Add(new PointF(Wls[i] + j, new_hz_val));
                        }
                    }
                    result.Add(new PointF(Wls[count_of_gaps], Hzs[count_of_gaps]));
                    int data_count = result.Count;
                    Wls = new float[data_count];
                    Hzs = new float[data_count];
                    for (int i = 0; i < data_count; i++)
                    {
                        Wls[i] = result[i].X;
                        Hzs[i] = result[i].Y;
                    }
                    if (precision != 1)
                    {
                        for (int i = 0; i < Wls.Length; i++)
                        {
                            Wls[i] *= precision;
                        }
                    }
                }
            }
            public class Files
            {
                public static List<string> Read_txt(string path)
                {
                    string[] AllLines = System.IO.File.ReadAllLines(path);
                    List<string> result = new List<string>(AllLines);
                    return result;
                }
                public static bool Write_txt(string path, List<string> data)
                {
                    bool result = false;
                    try
                    {
                        string[] AllLines = new string[data.Count];
                        data.CopyTo(AllLines);
                        System.IO.File.WriteAllLines(path, AllLines);
                        result = true;
                    }
                    catch
                    {
                        result = false;
                    }
                    return result;
                }

                public static void AddString_2file(string FileName, string str)
                {
                    List<string> data = new List<string>() ;
                    try { data = Read_txt(FileName); } catch { }
                    data.Add(str);
                    Write_txt(FileName, data);
                }

                public static void Get_WLData_byKnownCountofNumbers(int countofnumbers, string[] AllStrings,
               out float[] pWls, out float[] pHzs, out float[] pCoefs)
                {

                    float CurWl = 0, CurHz = 0, CurCoef = 0;
                    List<float> dWls = new List<float>();
                    List<float> dHzs = new List<float>();
                    List<float> dCoefs = new List<float>();

                    float[] Params = new float[countofnumbers];
                    for (int i = 0; i < AllStrings.Length; ++i)
                    {
                        try
                        {
                            Get_WLData_fromDevString(AllStrings[i], countofnumbers, Params);
                            dHzs.Add(Params[0]); dWls.Add(Params[1]); dCoefs.Add(Params[2]);
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    dWls.Reverse();
                    dHzs.Reverse();
                    dCoefs.Reverse();

                    pWls = dWls.ToArray();
                    pHzs = dHzs.ToArray();
                    pCoefs = dCoefs.ToArray();
                }

              

                public static void Get_WLData_fromDevString(string datastring, int NumberOfParamsInString, float[] pPars)
                {
                    int startindex = 0; bool startfound = false;
                    int finishindex = 0; bool finishfound = false;
                    List<float> datavalues = new List<float>();

                    for (int i = 0; i < datastring.Length; i++)
                    {
                        if ((datastring[i] != ' ') && ((Char.IsDigit(datastring[i])) || (datastring[i] == '.') || (datastring[i] == ',') || (datastring[i] == '-')))
                        {
                            if (startfound)
                            {
                                finishindex++;
                            }
                            else
                            {
                                startindex = i;
                                startfound = true;
                            }
                        }
                        else
                        {
                            if (startfound)
                            {
                                finishindex = i;
                                finishfound = true;
                            }
                        }
                        if (startfound && finishfound)
                        {
                            string data = datastring.Substring(startindex, finishindex - startindex);
                            double result = 0;
                            double.TryParse((data.Replace(',', '.')),System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture,out result);
                            datavalues.Add((float)result);
                            startfound = finishfound = false;
                        }
                    }
                    if (startfound && !finishfound)
                    {
                        string data = datastring.Substring(startindex);
                        double result = 0;
                        double.TryParse((data.Replace(',', '.')), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out result);
                        datavalues.Add((float)result);
                    }
                    for (int i = 0; i < NumberOfParamsInString; i++) { pPars[i] = datavalues[i]; }
                }

                public static void Get_Data_fromDevFile(string[] AllStrings,  out List<float[]> Colomns)
                {
                    List<float[]> DataRows = new List<float[]>();
                    List<float> Params = new List<float>();
                    for (int i = 0; i < AllStrings.Length; ++i)
                    {
                        try
                        {
                            Get_Data_fromString(AllStrings[i], ref Params);
                            DataRows.Add(Params.ToArray());
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    if (DataRows[0][0] < DataRows[DataRows.Count - 1][0]) DataRows.Reverse();
                    Colomns = new List<float[]>();
                    for (int i = 0; i < DataRows[0].Length; i++)
                    {
                        float[] Colomn_of_vals = new float[DataRows.Count];
                        for (int j = 0; j < Colomn_of_vals.Length; j++)
                        {
                            Colomn_of_vals[j] = DataRows[j][i];
                        }
                        Colomns.Add(Colomn_of_vals);
                    }
                }
                public static void Get_Data_fromString(string datastring, ref List<float> pPars)
                {
                    int startindex = 0; bool startfound = false;
                    int finishindex = 0; bool finishfound = false;
                    List<float> datavalues = new List<float>();

                    //Оказывается, не во всех системах все читается корректно. Делаем проверку
                    bool isDotNeeded = false;
                    double datastr = 0;
                    try { datastr = Convert.ToDouble(("0.1155").Replace('.', ',')); isDotNeeded = false; }
                    catch { isDotNeeded = true; }
                    try { datastr = Convert.ToDouble(("0,1155").Replace(',', '.')); isDotNeeded = true; }
                    catch { isDotNeeded = false; }


                    for (int i = 0; i < datastring.Length; i++)
                    {
                        if (((datastring[i] != ' ') || (datastring[i] != '\t'))
                            && ((Char.IsDigit(datastring[i])) || (datastring[i] == '.') || (datastring[i] == ',') || (datastring[i] == '-')))
                        {
                            if (startfound)
                            {
                                finishindex++;
                            }
                            else
                            {
                                startindex = i;
                                startfound = true;
                            }
                        }
                        else
                        {
                            if (startfound)
                            {
                                finishindex = i;
                                finishfound = true;
                            }
                        }
                        if (startfound && finishfound)
                        {
                            string data = datastring.Substring(startindex, finishindex - startindex);


                            datavalues.Add((float)Convert.ToDouble(data.Replace('.', ',')));
                            startfound = finishfound = false;
                        }
                    }
                    if (startfound && !finishfound)
                    {
                        string data = datastring.Substring(startindex);
                        if (isDotNeeded)
                            datavalues.Add((float)Convert.ToDouble(data.Replace(',', '.')));
                        else
                            datavalues.Add((float)Convert.ToDouble(data.Replace('.', ',')));
                    }
                    pPars = datavalues;
                }
            }
        }
    }
}