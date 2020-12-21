using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using static AO_Lib.AO_Devices;
using System.Net;
using System.Globalization;
using System.Threading;

namespace AO_Lib
{
    public class EthernetFilter : AO_Filter, ISweepable
    {
        public override bool SweepAvailable { get { return true; } }
        public override FilterTypes FilterType => FilterTypes.EthernetFilter;

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

        public override event SetNotifier onSetWl;
        public override event SetNotifier onSetHz;

        public int Current_Attenuation => sCurrent_Attenuation;
        private int sCurrent_Attenuation = 0;

        protected override bool sAO_Sweep_On { set; get; }
        protected override bool sAO_ProgrammMode_Ready { set; get; }
        private double Reference_frequency = 350e6;
        private const double dT_sweep_min = 11.42;
        private const double dT_sweep_max = 11.42 * 65536;
        private const double F_deviat_max = 5000;//KHz
        private const double dF_deviat_max = 200;//KHz
        //private byte[] Own_UsbBuf = new byte[5000];
        //private byte[] Own_ProgrammBuf;

        private TcpClient tcpClient = null;
        public TcpClient TcpClient { get { return tcpClient; } }

        public bool Connected => tcpClient == null ? false : tcpClient.Connected;

        private NetworkStream netStream = null;
        public NetworkStream NetworkStream => netStream;

        private IPEndPoint ipEndPoint = null;// = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5310);

        private IPAddress IP => ipEndPoint.Address;

        private int Port => ipEndPoint.Port;

        public EthernetFilter(string ipAddress, int port)
        {
            Init_device(ipAddress, port);
        }

        public EthernetFilter(IPEndPoint ipEndPoint)
        {
            Init_device(ipEndPoint);
        }

        ~EthernetFilter()
        {
            this.PowerOff();
            this.Dispose();
        }

        public static bool CheckHost(string ipAddress)
        {
            return CheckHost(IPAddress.Parse(ipAddress));
        }

        public static bool CheckHost(IPAddress ipAddress)
        {
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                return false;

            var host = Dns.GetHostEntry(Dns.GetHostName());

            IPAddress[] addresses1 = Dns.GetHostAddresses(Dns.GetHostName());
            IPAddress[] addresses2 = Dns.GetHostAddresses("localhost");
            if (addresses1 != null && addresses1.Contains(ipAddress))
                return true;
            else if (addresses2 != null && addresses2.Contains(ipAddress))
                return true;
            else
                return false;
        }

        public override string Ask_required_dev_file()
        {
            return ("(special *.dev file)");
        }

        public override void Dispose()
        {
            Deinit_device();
        }

        public override string Implement_Error(int pCode_of_error)
        {
            if(pCode_of_error == 0)
                return "No errors";
            return "Unknown error";
        }

        protected override int Deinit_device()
        {
            try
            {
                if (tcpClient != null && tcpClient.Connected)
                {
                    tcpClient.Close();
                }
            }
            catch { }
            try
            {
                if(netStream != null)
                {
                    netStream.Close();
                }
            }
            catch { }
            return 0;
        }

        protected override int Init_device(uint number)
        {
            return 0; //Not using
        }

        protected int Init_device(string ipAddressStr, int port)
        {
            IPAddress ipAddress = IPAddress.Parse(ipAddressStr);
            IPEndPoint endPoint = new IPEndPoint(ipAddress, port);

            return Init_device(endPoint);
        }

        protected int Init_device(IPEndPoint endPoint)
        {
            AOF_Loaded_without_fails = false;

            tcpClient = new TcpClient();
            tcpClient.Connect(endPoint);
            ipEndPoint = endPoint;

            netStream = tcpClient.GetStream();

            if(tcpClient.Connected)
            {
                AOF_Loaded_without_fails = true;
                sAO_ProgrammMode_Ready = false;
            }
            else
            {
                AOF_Loaded_without_fails = false;
            }

            return 0;
        }

        /// <summary>
        /// Устанавливает на АОФ заданную частоту в МГц.
        /// </summary>
        public override int Set_Hz(float freq)
        {
            base.Set_Hz(freq);

            if (!AOF_Loaded_without_fails)
                throw new Exception("AOF loaded with errors");

            try
            {
                //HARDCODE

                /*string s = Create_string_forHzTune(freq);
                Write(s);*/

                byte[] buffer = Create_byteMass_forHzTune(freq);
                WriteBytes(buffer, 10, 2);

                /*if (MS_delay > 0)
                    Thread.Sleep(MS_delay);*/

                sWL_Current = Get_WL_via_HZ(freq);
                sHZ_Current = freq;
                onSetHz?.Invoke(this, WL_Current, HZ_Current);
                return 0;
            }
            catch (Exception exc)
            {
                return 1;
            }
        }

        public int Set_Hz(float freq, float pCoef_Power_Decrement = 000)
        {
            base.Set_Hz(freq);

            if(AOF_Loaded_without_fails)
            {
                try
                {
                    //Hardcode

                    /*string s = "";
                    if (pCoef_Power_Decrement == 0)
                    {
                        s = Create_string_forHzTune(freq);
                        sCurrent_Attenuation = (int)pCoef_Power_Decrement;
                    }
                    else
                    {
                        s = Create_string_forHzTune(freq, (uint)pCoef_Power_Decrement);
                        sCurrent_Attenuation = (int)pCoef_Power_Decrement;
                    }

                    Write(s);*/

                    byte[] buffer;

                    if (pCoef_Power_Decrement == 0)
                    {
                        buffer = Create_byteMass_forHzTune(freq);
                        sCurrent_Attenuation = (int)pCoef_Power_Decrement;
                    }
                    else
                    {
                        buffer = Create_byteMass_forHzTune(freq, (uint)pCoef_Power_Decrement);
                        sCurrent_Attenuation = (int)pCoef_Power_Decrement;
                    }

                    WriteBytes(buffer, 10, 2);

                    /*if (MS_delay > 0)
                        Thread.Sleep(MS_delay);*/

                    sWL_Current = Get_WL_via_HZ(freq);
                    sHZ_Current = freq;
                    onSetHz?.Invoke(this, WL_Current, HZ_Current);
                }
                catch
                {
                    return 1;
                }
            }

            return 0;
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
                    return 1;
                }
            }
            else
            {
                return 1;
            }
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
        public byte[] Create_byteMass_byKnownParams_062020(float mfreq0_sweep, float mdeltafreq_sweep,
            int mN_sweep,
            double T_up_sweep, double T_down_sweep,
            bool mode,
            bool m_repeat = true)
        {
            float[] freq = new float[3]; // unsigned long lvspom; unsigned int ivspom; float fvspom, freq[3], minstep;
            byte[] data_buf = new byte[26];
            double fsys_mcu = 1.7f * (0.5f * 75e6);
            double mfreq_sys = Reference_frequency / 1e6; //unsigned char tx[26]; unsigned char delayt1, delayt2; long fsys_mcu = 1.7 * (0.5 * 75e6);

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

        public int Set_Sweep_on(float MHz_start, float Sweep_range_MHz, int steps, double time_up, double time_down)
        {
            //здесь MHz_start = m_f0 - начальна частота в МГц    
            //Sweep_range_MHz = m_deltaf - девиация частоты в МГц
            try
            {
                try
                {
                    byte[] buffer = Create_byteMass_byKnownParams_062020(MHz_start, Sweep_range_MHz, steps, time_up, time_down, !(time_down < 1e-5));
                    Write(buffer);
                }
                catch { }
                sAO_Sweep_On = true;
                return 0;
            }
            catch { }
            return 1;
        }

        private byte[] onBytes = new byte[] { 0x01, 0x02, 0xFF };
        private byte[] offBytes = new byte[] { 0x02, 0x02, 0xFF };

        public override int Set_Sweep_on(float MHz_start, float Sweep_range_MHz, double Period/*[мкс с точностью до двух знаков,минимум 1]*/, bool OnRepeat)
        {
            //здесь MHz_start = m_f0 - начальна частота в МГц    
            //Sweep_range_MHz = m_deltaf - девиация частоты в МГц
            try
            {
                byte[] buffer = new byte[5000];
                int count = 0;
                //????????//
                try
                {
                    Write(buffer, count, buffer.Length);
                }
                catch
                {
                    return 1;
                }
                sAO_Sweep_On = true;
                return 0;
            }
            catch
            {
                return 1;
            }
        }

        public override int Set_Sweep_off()
        {
            return Set_Hz(HZ_Current);
        }

        //HARDCODE
        public string Create_string_forHzTune(float pfreq, uint pCoef_PowerDecrease = 0)
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

            ushort HW = (ushort)(0x0000ffFF & (lvspom >> 16));
            ushort LW = (ushort)(lvspom & 0x0000FFFF);

            byte HWHW = (byte)(0x00FF & (HW >> 8));
            byte HWLW = (byte)(0x00FF & HW);
            byte LWHW = (byte)(0x00FF & (LW >> 8));
            byte LWLW = (byte)(0x00FF & LW);

            byte AMHW = (byte)(0x00ff & (ivspom >> 8));
            byte AMLW = (byte)(0x00ff & ivspom);

            //data_Own_UsbBuf[0] = MainCommands.SET_HZ; //it means, we will send wavelength

            /*data_Own_UsbBuf[1] = (byte)(0x00ff & (MSB >> 8));
            data_Own_UsbBuf[2] = (byte)MSB;
            data_Own_UsbBuf[3] = (byte)(0x00ff & (LSB >> 8));
            data_Own_UsbBuf[4] = (byte)LSB;
            data_Own_UsbBuf[5] = (byte)(0x00ff & (ivspom >> 8));
            data_Own_UsbBuf[6] = (byte)ivspom;

            int b2w = 7;

            for (int i = 0; i < b2w; i++)
            {
                data_Own_UsbBuf[i] = (byte)Bit_reverse(data_Own_UsbBuf[i], Bit_inverse_needed);
            }*/
            //3 ()
            string s = String.Format(CultureInfo.InvariantCulture, "33 33 {0} {1} {2} {3} {4} {5} 66 66 ", HWHW, HWLW, LWHW, LWLW, AMHW, AMLW);
            return s;
        }

        /// <summary>
        /// Создает по заданным параметром массив байт для перестройки на определенную частоту.
        /// </summary>
        public byte[] Create_byteMass_forHzTune(float pfreq, uint pCoef_PowerDecrease = 0)
        {
            float fvspom; short MSB, LSB; ulong lvspom;

            float freq_was = pfreq;
            float freq100f = pfreq*100f;
            if (freq100f > ushort.MaxValue)
                freq100f = ushort.MaxValue;
            uint freq100 = (uint) (pfreq * 100f);

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

            //data_Own_UsbBuf[0] = MainCommands.SET_HZ; //it means, we will send wavelength

            data_Own_UsbBuf[0] = 0x03; //set hz
            data_Own_UsbBuf[1] = (byte)(0x00ff & (MSB >> 8));
            data_Own_UsbBuf[2] = (byte)MSB;
            data_Own_UsbBuf[3] = (byte)(0x00ff & (LSB >> 8));
            data_Own_UsbBuf[4] = (byte)LSB;
            data_Own_UsbBuf[5] = (byte)(0x00ff & (ivspom >> 8));
            data_Own_UsbBuf[6] = (byte)ivspom;
            data_Own_UsbBuf[7] = (byte)(0x00ff & (freq100 >> 8));
            data_Own_UsbBuf[8] = (byte)freq100;
            data_Own_UsbBuf[9] = 0xFF;

            /*int b2w = 7;
            for (int i = 0; i < b2w; i++)
            {
                data_Own_UsbBuf[i] = (byte)Bit_reverse(data_Own_UsbBuf[i], Bit_inverse_needed);
            }*/

            return data_Own_UsbBuf;
        }

        public override int PowerOn()
        {
            //var state = Set_Hz((HZ_Max + HZ_Min) / 2);
            //Write("1 ");
            //WriteBytes(onBytes, onBytes.Length, 10);
            sAOF_isPowered = true;
            return 0;
        }

        public override int PowerOff()
        {
            try
            {
                //HARDCODE
                /*byte[] buffer = new byte[1024];

                buffer[0] = (byte)Bit_reverse(STC_Filter.MainCommands.POWER_OFF, Bit_inverse_needed); //почему там int?*/

                try
                {
                    //HARDCODE
                    //Write("2 ");
                    WriteBytes(offBytes, offBytes.Length, 4);
                    //Write(buffer, 0, 1);
                }
                catch
                {
                    sAOF_isPowered = false;
                    return 1;
                }
            }
            catch
            {
                sAOF_isPowered = false;
                return 1;
            }
            sAOF_isPowered = false;
            return 0;
        }

        public static int Bit_reverse(int input, bool isBitInvNeeded = false)
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
            {
                output = input;
            }
            return output;
        }

        public unsafe bool Write(byte[] buffer)
        {
            return Write(buffer, 0, buffer.Length);
        }

        public unsafe bool Write(byte[] buffer , int offset, int count)
        {
            netStream.Write(buffer, offset, count);
            return true;
        }

        public bool WriteBytes(byte[] bytes, int length, int repeats)
        {
            try
            {
                ReconnectHARDCODE();
                for (int j = 0; j < repeats; j++)
                    for (int i = 0; i < length; i++)
                    {
                        netStream.WriteByte(bytes[i]);
                    }
            }
            catch(Exception ex) { }
            
            return true;
        }

        public unsafe bool Write(string s)
        {
            //Thread.Sleep(50);
            //string -> byte array
            //ReconnectHARDCODE();

            //byte[] sendbuffer = Encoding.ASCII.GetBytes(s);

            /*bool wasPowered = isPowered;

            if (wasPowered)
            {
                sendbuffer = Encoding.ASCII.GetBytes("2 ");
                netStream.Write(sendbuffer, 0, sendbuffer.Length);
            }*/

            byte[] sendbuffer = Encoding.ASCII.GetBytes(s);
            //netStream.Write(sendbuffer, 0, sendbuffer.Length);
            //netStream.WriteByte(2);
            for(int j = 0; j < 3; j++)
            {
                netStream.WriteByte(0x03);
                for (int i = 0; i < 6; i++)
                {
                    netStream.WriteByte((byte)i);
                }
                netStream.WriteByte(0xFf);
            }
            
            /*if(s[0] == '3')
            {
                datas.Add(s + "    " + decode(s));
                System.IO.File.WriteAllLines("log.txt", datas.ToArray());
            }*/
            /*if (wasPowered)
            {
                sendbuffer = Encoding.ASCII.GetBytes("1 ");
                netStream.Write(sendbuffer, 0, sendbuffer.Length);
            }*/

            return false;
        }

        private string decode(string s)
        {
            string[] arr = s.Split(' ');

            ulong lng = 0;

            unsafe
            {
                ulong* lngPtr = &lng;
                byte* lngArr = (byte*)lngPtr;

                lngArr[0] = Convert.ToByte(arr[4]);
                lngArr[1] = Convert.ToByte(arr[3]);
                lngArr[2] = Convert.ToByte(arr[2]);
                lngArr[3] = Convert.ToByte(arr[1]);
            }

            //arr[0]
            float freqL = (float)(1d * lng / Math.Pow(2, 32)*Reference_frequency)*1e-6f;

            return Convert.ToString(freqL) + "   " + Convert.ToString( ((int)(Convert.ToByte(arr[5])) << 8) + Convert.ToByte(arr[6]));
        }

        List<string> datas = new List<string>();

        public unsafe bool WriteHZ(string s)
        {
            //Thread.Sleep(50);
            //string -> byte array
            //ReconnectHARDCODE();

            byte[] sendbuffer = Encoding.ASCII.GetBytes(s);

            bool wasPowered = isPowered;

            sendbuffer = Encoding.ASCII.GetBytes(s);
            netStream.Write(sendbuffer, 0, sendbuffer.Length);


            /*sendbuffer = Encoding.ASCII.GetBytes("2 ");
            netStream.Write(sendbuffer, 0, sendbuffer.Length);

            sendbuffer = Encoding.ASCII.GetBytes("1 ");
            netStream.Write(sendbuffer, 0, sendbuffer.Length);
            if (wasPowered)
            {

            }*/

            return false;
        }

        private void ReconnectHARDCODE()
        {
            try
            {
                var endPoint = ipEndPoint;
                int port = Port;
                Deinit_device();
                Thread.Sleep(100);
                Init_device(endPoint);
            }
            catch(Exception) { }
        }
    }
}
