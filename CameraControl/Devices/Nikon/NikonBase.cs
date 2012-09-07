﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Timers;
using CameraControl.Classes;
using CameraControl.Devices.Classes;
using CameraControl.Devices.Others;
using PortableDeviceLib;
using PortableDeviceLib.Model;
using WIA;
using Timer = System.Timers.Timer;

namespace CameraControl.Devices.Nikon
{
  public class NikonBase : BaseMTPCamera
  {
    public const int CONST_CMD_AfDrive = 0x90C1;
    public const int CONST_CMD_StartLiveView = 0x9201;
    public const int CONST_CMD_EndLiveView = 0x9202;
    public const int CONST_CMD_GetLiveViewImage = 0x9203;
    public const int CONST_CMD_InitiateCapture = 0x100E;
    public const int CONST_CMD_InitiateCaptureRecInMedia = 0x9207;
    public const int CONST_CMD_AfAndCaptureRecInSdram = 0x90CB;
    public const int CONST_CMD_InitiateCaptureRecInSdram = 0x90C0;
    public const int CONST_CMD_ChangeAfArea = 0x9205;
    public const int CONST_CMD_MfDrive = 0x9204;
    public const int CONST_CMD_GetDevicePropValue = 0x1015;
    public const int CONST_CMD_SetDevicePropValue = 0x1016;
    public const int CONST_CMD_GetEvent = 0x90C7;
    public const int CONST_CMD_GetDevicePropDesc = 0x1014;
    public const int CONST_CMD_DeviceReady = 0x90C8;
    public const int CONST_CMD_GetObjectInfo = 0x1008;
    public const int CONST_CMD_GetObject = 0x1009;
    public const int CONST_CMD_ChangeCameraMode = 0x90C2;
    public const int CONST_CMD_StartMovieRecInCard = 0x920A;
    public const int CONST_CMD_EndMovieRec = 0x920B;

    public const int CONST_PROP_Fnumber = 0x5007;
    public const int CONST_PROP_ExposureIndex = 0x500F;
    public const int CONST_PROP_ExposureTime = 0x500D;
    public const int CONST_PROP_WhiteBalance = 0x5005;
    public const int CONST_PROP_ExposureProgramMode = 0x500E; 
    public const int CONST_PROP_ExposureBiasCompensation = 0x5010;
    public const int CONST_PROP_BatteryLevel = 0x5001;
    public const int CONST_PROP_LiveViewImageZoomRatio = 0xD1A3;
    public const int CONST_PROP_AFModeSelect = 0xD161;
    public const int CONST_PROP_CompressionSetting = 0x5004;
    public const int CONST_PROP_ExposureMeteringMode = 0x500B;
    public const int CONST_PROP_FocusMode = 0x500A;
    public const int CONST_PROP_LiveViewStatus = 0xD1A2;
    public const int CONST_PROP_ExposureIndicateStatus = 0xD1B1;
    public const int CONST_PROP_RecordingMedia = 0xD10B;

    public const int CONST_Event_DevicePropChanged = 0x4006;
    public const int CONST_Event_StoreFull = 0x400A;
    public const int CONST_Event_CaptureComplete = 0x400D;
    public const int CONST_Event_CaptureCompleteRecInSdram = 0xC102;
    public const int CONST_Event_ObjectAdded = 0x4002;
    public const int CONST_Event_ObjectAddedInSdram = 0xC101;


    private const string AppName = "CameraControl";
    private const int AppMajorVersionNumber = 1;
    private const int AppMinorVersionNumber = 0;
    private Timer _timer = new Timer(1000/15);

   
    private Dictionary<uint, string> _isoTable = new Dictionary<uint, string>()
                                                  {
                                                    {0x0064, "100"},
                                                    {0x007D, "125"},
                                                    {0x00A0, "160"},
                                                    {0x00C8, "200"},
                                                    {0x00FA, "250"},
                                                    {0x0140, "320"},
                                                    {0x0190, "400"},
                                                    {0x01F4, "500"},
                                                    {0x0280, "640"},
                                                    {0x0320, "800"},
                                                    {0x03E8, "1000"},
                                                    {0x04E2, "1250"},
                                                    {0x0640, "1600"},
                                                    {0x07D0, "2000"},
                                                    {0x09C4, "2500"},
                                                    {0x0C80, "3200"},
                                                    {0x0FA0, "4000"},
                                                    {0x1388, "5000"},
                                                    {0x1900, "6400"},
                                                    {0x1F40, "Hi 0.3"},
                                                    {0x2710, "Hi 0.7"},
                                                    {0x3200, "Hi 1"},
                                                    {0x6400, "Hi 2"},
                                                  };
    Dictionary<uint, string> _shutterTable = new Dictionary<uint, string>
                                               {
                         {1, "1/6400"},
                         {2, "1/4000"},
                         {3, "1/3200"},
                         {4, "1/2500"},
                         {5, "1/2000"},
                         {6, "1/1600"},
                         {8, "1/1250"},
                         {10, "1/1000"},
                         {12, "1/800"},
                         {13, "1/750"},
                         {15, "1/640"},
                         {20, "1/500"},
                         {25, "1/400"},
                         {28, "1/350"},
                         {31, "1/320"},
                         {40, "1/250"},
                         {50, "1/200"},
                         {55, "1/180"},
                         {62, "1/160"},
                         {80, "1/125"},
                         {100, "1/100"},
                         {111, "1/90"},
                         {125, "1/80"},
                         {166, "1/60"},
                         {200, "1/50"},
                         {222, "1/45"},
                         {250, "1/40"},
                         {333, "1/30"},
                         {400, "1/25"},
                         {500, "1/20"},
                         {666, "1/15"},
                         {769, "1/13"},
                         {1000, "1/10"},
                         {1250, "1/8"},
                         {1666, "1/6"},
                         {2000, "1/5"},
                         {2500, "1/4"},
                         {3333, "1/3"},
                         {4000, "1/2.5"},
                         {5000, "1/2"},
                         {6250, "1/1.6"},
                         {6666, "1/1.5"},
                         {7692, "1/1.3"},
                         {10000, "1s"},
                         {13000, "1.3s"},
                         {15000, "1.5s"},
                         {16000, "1.6s"},
                         {20000, "2s"},
                         {25000, "2.5s"},
                         {30000, "3s"},
                         {40000, "4s"},
                         {50000, "5s"},
                         {60000, "6s"},
                         {80000, "8s"},
                         {100000, "10s"},
                         {130000, "13s"},
                         {150000, "15s"},
                         {200000, "20s"},
                         {250000, "25s"},
                         {300000, "30s"},
                         { 0xFFFFFFFF , "Bulb"},
                       };
    
    Dictionary<int, string> _exposureModeTable = new Dictionary<int, string>()
                            {
                              {1, "M"},
                              {2, "P"},
                              {3, "A"},
                              {4, "S"},
                              {0x8010, "[Scene mode] AUTO"},
                              {0x8011, "[Scene mode] Portrait"},
                              {0x8012, "[Scene mode] Landscape"},
                              {0x8013, "[Scene mode] Close up"},
                              {0x8014, "[Scene mode] Sports"},
                              {0x8016, "[Scene mode] Flash prohibition AUTO"},
                              {0x8017, "[Scene mode] Child"},
                              {0x8018, "[Scene mode] SCENE"},
                              {0x8019, "[EffectMode] EFFECTS"},
                            };

    Dictionary<uint, string> _wbTable = new Dictionary<uint, string>()
                  {
                    {2, "Auto"},
                    {4, "Daylight"},
                    {5, "Fluorescent "},
                    {6, "Incandescent"},
                    {7, "Flash"},
                    {32784, "Cloudy"},
                    {32785, "Shade"},
                    {32786, "Kelvin"},
                    {32787, "Custom"}
                  };
    
    protected Dictionary<int, string> _csTable = new Dictionary<int, string>()
                                                {
                                                  {0, "JPEG (BASIC)"},
                                                  {1, "JPEG (NORMAL)"},
                                                  {2, "JPEG (FINE)"},
                                                  {3, "TIFF (RGB)"},
                                                  {4, "RAW"},
                                                  {5, "RAW + JPEG (BASIC)"},
                                                  {6, "RAW + JPEG (NORMAL)"},
                                                  {7, "RAW + JPEG (FINE)"}
                                                };
    private Dictionary<int, string> _emmTable = new Dictionary<int, string>
                                                  {
                                                {2, "Center-weighted metering"},
                                                {3, "Multi-pattern metering"},
                                                {4, "Spot metering"}
                                              };

    private Dictionary<uint, string> _fmTable = new Dictionary<uint, string>()
                                              {
                                                {1, "[M] Manual focus"},
                                                {0x8010, "[S] Single AF servo"},
                                                {0x8011, "[C] Continuous AF servo"},
                                                {0x8012, "[A] AF servo mode automatic switching"},
                                                {0x8013, "[F] Constant AF servo"},
                                              };
    internal object Locker = new object(); // object used to lock multi hreaded mothods 



    public NikonBase()
    {
      _timer.AutoReset = true;
      _timer.Elapsed += _timer_Elapsed;
    }

    void _timer_Elapsed(object sender, ElapsedEventArgs e)
    {
      _timer.Stop();
      try
      {
        lock (Locker)
        {
          getEvent();
        }
      }
      catch (Exception)
      {
        
        
      }
      _timer.Start();
    }


    public override bool Init(DeviceDescriptor deviceDescriptor)
    {
      Capabilities.Add(CapabilityEnum.CaptureInRam);
      _stillImageDevice = new StillImageDevice(deviceDescriptor.WpdId);
      _stillImageDevice.ConnectToDevice(AppName, AppMajorVersionNumber, AppMinorVersionNumber);
      _stillImageDevice.DeviceEvent += _stillImageDevice_DeviceEvent;
      HaveLiveView = true;
      DeviceReady();
      DeviceName = _stillImageDevice.Model;
      Manufacturer = _stillImageDevice.Manufacturer;
      InitIso();
      InitShutterSpeed();
      InitFNumber();
      InitMode();
      InitWhiteBalance();
      InitExposureCompensation();
      InitCompressionSetting();
      InitExposureMeteringMode();
      InitFocusMode();
      InitOther();
      ReadDeviceProperties(CONST_PROP_BatteryLevel);
      ReadDeviceProperties(CONST_PROP_ExposureIndicateStatus);
      IsConnected = true;
      PropertyChanged += NikonBase_PropertyChanged;
      CaptureInSdRam = true;
      _timer.Start();
      return true;
    }

    protected void NikonBase_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
      if (e.PropertyName == "CaptureInSdRam")
      {
        SetProperty(CONST_CMD_SetDevicePropValue, CaptureInSdRam ? new[] { (byte)1 } : new[] { (byte)0 }, CONST_PROP_RecordingMedia, -1);        
      }
    }

    void _stillImageDevice_DeviceEvent(object sender, PortableDeviceEventArgs e)
    {
      if (e.EventType.EventGuid == PortableDeviceGuids.WPD_EVENT_DEVICE_REMOVED)
      {
        _timer.Stop();
        _stillImageDevice.Disconnect();
        _stillImageDevice.IsConnected = false;
        IsConnected = false;
        ServiceProvider.DeviceManager.DisconnectCamera(_stillImageDevice);
      }
      if (PhotoCaptured != null && e.EventType.EventGuid == PortableDeviceGuids.WPD_EVENT_OBJECT_ADDED)
      {
        //PhotoCapturedEventArgs args = new PhotoCapturedEventArgs
        //                                {
        //                                  WiaImageItem = null,
        //                                  EventArgs = e,
        //                                  CameraDevice = this,
        //                                  FileName = e.EventType.DeviceObject.Name
        //                                };
        //PhotoCaptured(this, args);
      }
    }


    void IsoNumber_ValueChanged(object sender, string key, int val)
    {
      lock (Locker)
      {
        SetProperty(CONST_CMD_SetDevicePropValue, BitConverter.GetBytes(val),
                                           CONST_PROP_ExposureIndex, -1);
      }
    }

    void ExposureCompensation_ValueChanged(object sender, string key, int val)
    {
      lock (Locker)
      {
        SetProperty(CONST_CMD_SetDevicePropValue, BitConverter.GetBytes(val),
                                           CONST_PROP_ExposureBiasCompensation, -1);
      }
    }

    private void InitOther()
    {
      LiveViewImageZoomRatio = new PropertyValue<int>();
      LiveViewImageZoomRatio.AddValues("All", 0);
      LiveViewImageZoomRatio.AddValues("25%", 1);
      LiveViewImageZoomRatio.AddValues("33%", 2);
      LiveViewImageZoomRatio.AddValues("50%", 3);
      LiveViewImageZoomRatio.AddValues("66%", 4);
      LiveViewImageZoomRatio.AddValues("100%", 5);
      LiveViewImageZoomRatio.SetValue("All");
      LiveViewImageZoomRatio.ValueChanged += LiveViewImageZoomRatio_ValueChanged;
    }

    void LiveViewImageZoomRatio_ValueChanged(object sender, string key, int val)
    {
      lock (Locker)
      {
        SetProperty(CONST_CMD_SetDevicePropValue, BitConverter.GetBytes(val),
                                   CONST_PROP_LiveViewImageZoomRatio, -1);
      }
    }

    private void InitIso()
    {
      lock (Locker)
      {
        IsoNumber = new PropertyValue<int>();
        IsoNumber.ValueChanged += IsoNumber_ValueChanged;
        IsoNumber.Clear();
        try
        {
          DeviceReady();
          byte[] result = _stillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropDesc, CONST_PROP_ExposureIndex);
          int type = BitConverter.ToInt16(result, 2);
          byte formFlag = result[9];
          UInt16 defval = BitConverter.ToUInt16(result, 7);
          for (int i = 0; i < result.Length - 12; i += 2)
          {
            UInt16 val = BitConverter.ToUInt16(result, 12 + i);
            IsoNumber.AddValues(_isoTable.ContainsKey(val) ? _isoTable[val] : val.ToString(), val);
          }
          IsoNumber.SetValue(defval);
        }
        catch (Exception)
        {
          IsoNumber.IsEnabled = false;
        }
      }
    }

    private void InitShutterSpeed()
    {
      ShutterSpeed = new PropertyValue<long>();
      ShutterSpeed.ValueChanged += ShutterSpeed_ValueChanged;
      ReInitShutterSpeed();
    }

    private void ReInitShutterSpeed()
    {
      lock (Locker)
      {
        DeviceReady();
        try
        {
          byte datasize = 4;
          ShutterSpeed.Clear();
          byte[] result = _stillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropDesc, CONST_PROP_ExposureTime);
          int type = BitConverter.ToInt16(result, 2);
          byte formFlag = result[(2 * datasize) + 5];
          UInt32 defval = BitConverter.ToUInt32(result, datasize + 5);
          for (int i = 0; i < result.Length - ((2 * datasize) + 6 + 2); i += datasize)
          {
            UInt32 val = BitConverter.ToUInt32(result, ((2 * datasize) + 6 + 2) + i);
            ShutterSpeed.AddValues(_shutterTable.ContainsKey(val) ? _shutterTable[val] : val.ToString(), val);
          }
          ShutterSpeed.SetValue(defval);
        }
        catch (Exception ex)
        {


        }
      }
    }
    void ShutterSpeed_ValueChanged(object sender, string key, long val)
    {
      SetProperty(CONST_CMD_SetDevicePropValue, BitConverter.GetBytes(val),
                                         CONST_PROP_ExposureTime, -1);
    }

    private void InitMode()
    {
      try
      {
        DeviceReady();
        byte datasize = 2;
        Mode = new PropertyValue<uint>();
        Mode.IsEnabled = false;
        Mode.ValueChanged += Mode_ValueChanged;
        byte[] result = _stillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropDesc, CONST_PROP_ExposureProgramMode);
        int type = BitConverter.ToInt16(result, 2);
        byte formFlag = result[(2 * datasize) + 5];
        UInt16 defval = BitConverter.ToUInt16(result, datasize + 5);
        for (int i = 0; i < result.Length - ((2 * datasize) + 6 + 2); i += datasize)
        {
          UInt16 val = BitConverter.ToUInt16(result, ((2 * datasize) + 6 + 2) + i);
          Mode.AddValues(_exposureModeTable.ContainsKey(val) ? _exposureModeTable[val] : val.ToString(), val);
        }
        Mode.SetValue(defval);
      }
      catch (Exception ex)
      {
      }
    }



    void Mode_ValueChanged(object sender, string key, uint val)
    {
      switch (key)
      {
        case "M":
          ShutterSpeed.IsEnabled = true;
          FNumber.IsEnabled = true;
          ReInitShutterSpeed();
          break;
        case "P":
          ShutterSpeed.IsEnabled = false;
          FNumber.IsEnabled = false;
          break;
        case "A":
          ShutterSpeed.IsEnabled = false;
          FNumber.IsEnabled = true;
          break;
        case "S":
          ShutterSpeed.IsEnabled = true;
          FNumber.IsEnabled = false;
          break;
        default:
          ShutterSpeed.IsEnabled = false;
          FNumber.IsEnabled = false;
          break;
      }
    }

    private void InitFNumber()
    {
      FNumber = new PropertyValue<int> { IsEnabled = true };
      FNumber.ValueChanged += FNumber_ValueChanged;
      ReInitFNumber();
    }

    private void ReInitFNumber()
    {
      try
      {
        DeviceReady();
        FNumber.Clear();
        const byte datasize = 2;
        byte[] result = _stillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropDesc, CONST_PROP_Fnumber);
        int type = BitConverter.ToInt16(result, 2);
        byte formFlag = result[(2 * datasize) + 5];
        UInt16 defval = BitConverter.ToUInt16(result, datasize + 5);
        for (int i = 0; i < result.Length - ((2 * datasize) + 6 + 2); i += datasize)
        {
          UInt16 val = BitConverter.ToUInt16(result, ((2 * datasize) + 6 + 2) + i);
          double d = val;
          string s = "ƒ/" + (d / 100).ToString("0.0");
          FNumber.AddValues(s, val);
        }
        FNumber.SetValue(defval);
      }
      catch (Exception ex)
      {
      }
    }

    void FNumber_ValueChanged(object sender, string key, int val)
    {
      SetProperty(CONST_CMD_SetDevicePropValue, BitConverter.GetBytes(val),
                                   CONST_PROP_Fnumber, -1);
    }

    private void InitWhiteBalance()
    {
      try
      {
        DeviceReady();
        byte datasize = 2;
        WhiteBalance = new PropertyValue<long>();
        WhiteBalance.ValueChanged += WhiteBalance_ValueChanged;
        byte[] result = _stillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropDesc, CONST_PROP_WhiteBalance);
        int type = BitConverter.ToInt16(result, 2);
        byte formFlag = result[(2 * datasize) + 5];
        UInt16 defval = BitConverter.ToUInt16(result, datasize + 5);
        for (int i = 0; i < result.Length - ((2 * datasize) + 6 + 2); i += datasize)
        {
          UInt16 val = BitConverter.ToUInt16(result, ((2 * datasize) + 6 + 2) + i);
          WhiteBalance.AddValues(_wbTable.ContainsKey(val) ? _wbTable[val] : val.ToString(), val);
        }
        WhiteBalance.SetValue(defval);
      }
      catch (Exception ex)
      {
      }
    }

    void WhiteBalance_ValueChanged(object sender, string key, long val)
    {
      SetProperty(CONST_CMD_SetDevicePropValue, BitConverter.GetBytes((UInt16)val),
                              CONST_PROP_WhiteBalance, -1); 
    }

    public void InitExposureCompensation()
    {
      try
      {
        DeviceReady();
        byte datasize = 2;
        ExposureCompensation = new PropertyValue<int>();
        ExposureCompensation.ValueChanged += ExposureCompensation_ValueChanged;
        byte[] result = _stillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropDesc, CONST_PROP_ExposureBiasCompensation);
        int type = BitConverter.ToInt16(result, 2);
        byte formFlag = result[(2*datasize) + 5];
        Int16 defval = BitConverter.ToInt16(result, datasize + 5);
        for (int i = 0; i < result.Length - ((2*datasize) + 6 + 2); i += datasize)
        {
          Int16 val = BitConverter.ToInt16(result, ((2*datasize) + 6 + 2) + i);
          decimal d = val;
          string s = Decimal.Round(d/1000, 1).ToString();
          if (d > 0)
            s = "+" + s;
          ExposureCompensation.AddValues(s, val);
        }
        ExposureCompensation.SetValue(defval);
      }
      catch (Exception ex)
      {
      }
    }

    protected virtual void InitCompressionSetting()
    {
      try
      {
        DeviceReady();
        byte datasize = 1;
        CompressionSetting = new PropertyValue<int>();
        CompressionSetting.ValueChanged += CompressionSetting_ValueChanged;
        byte[] result = _stillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropDesc, CONST_PROP_CompressionSetting);
        int type = BitConverter.ToInt16(result, 2);
        byte formFlag = result[(2 * datasize) + 5];
        byte defval = result[ datasize + 5];
        for (int i = 0; i < result.Length - ((2 * datasize) + 6 + 2); i += datasize)
        {
          byte val = result[((2*datasize) + 6 + 2) + i];
          CompressionSetting.AddValues(_csTable.ContainsKey(val) ? _csTable[val] : val.ToString(), val);
        }
        CompressionSetting.SetValue(defval);
      }
      catch (Exception ex)
      {
      }
    }

    protected void CompressionSetting_ValueChanged(object sender, string key, int val)
    {
      SetProperty(CONST_CMD_SetDevicePropValue, BitConverter.GetBytes((byte)val),
                        CONST_PROP_CompressionSetting, -1); 
    }

    public void InitExposureMeteringMode()
    {
      try
      {
        DeviceReady();
        byte datasize = 2;
        ExposureMeteringMode = new PropertyValue<int>();
        ExposureMeteringMode.ValueChanged += ExposureMeteringMode_ValueChanged;
        byte[] result = _stillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropDesc, CONST_PROP_ExposureMeteringMode);
        int type = BitConverter.ToInt16(result, 2);
        byte formFlag = result[(2 * datasize) + 5];
        UInt16 defval = BitConverter.ToUInt16(result, datasize + 5);
        for (int i = 0; i < result.Length - ((2 * datasize) + 6 + 2); i += datasize)
        {
          UInt16 val = BitConverter.ToUInt16(result, ((2 * datasize) + 6 + 2) + i);
          ExposureMeteringMode.AddValues(_emmTable.ContainsKey(val) ? _emmTable[val] : val.ToString(), val);
        }
        ExposureMeteringMode.SetValue(defval);
      }
      catch (Exception)
      {
      }
    }

    void ExposureMeteringMode_ValueChanged(object sender, string key, int val)
    {
      SetProperty(CONST_CMD_SetDevicePropValue, BitConverter.GetBytes((UInt16)val),
                        CONST_PROP_ExposureMeteringMode, -1);      
    }

    private void InitFocusMode()
    {
      try
      {
        DeviceReady();
        byte datasize = 2;
        FocusMode = new PropertyValue<uint>();
        FocusMode.IsEnabled = false;
        byte[] result = _stillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropDesc, CONST_PROP_FocusMode);
        int type = BitConverter.ToInt16(result, 2);
        byte formFlag = result[(2 * datasize) + 5];
        UInt16 defval = BitConverter.ToUInt16(result, datasize + 5);
        for (int i = 0; i < result.Length - ((2 * datasize) + 6 + 2); i += datasize)
        {
          UInt16 val = BitConverter.ToUInt16(result, ((2 * datasize) + 6 + 2) + i);
          FocusMode.AddValues(_fmTable.ContainsKey(val) ? _fmTable[val] : val.ToString(), val);
        }
        FocusMode.SetValue(defval);
      }
      catch (Exception )
      {
      }
    }
    
    public override void StartLiveView()
    {
      lock (Locker)
      {
        DeviceReady();
        //check if the live view already is started if yes returning without doing anything
        MTPDataResponse response = ExecuteReadDataEx(CONST_CMD_GetDevicePropValue, CONST_PROP_LiveViewStatus, -1);
        ErrorCodes.GetException(response.ErrorCode);
        if (response.Data != null && response.Data.Length > 0 && response.Data[0] > 0)
          return;
        DeviceReady();
        ErrorCodes.GetException(_stillImageDevice.ExecuteWithNoData(CONST_CMD_StartLiveView));
        DeviceReady();
        LiveViewImageZoomRatio.SetValue();
      }
    }

    public override void StopLiveView()
    {
      lock (Locker)
      {
        DeviceReady();
        ErrorCodes.GetException(_stillImageDevice.ExecuteWithNoData(CONST_CMD_EndLiveView));
        DeviceReady();
      }
    }

    public override LiveViewData GetLiveViewImage()
    {
      LiveViewData viewData = new LiveViewData();
      if (Monitor.TryEnter(Locker,100))
      {
        try
        {
          DeviceReady();
          viewData.HaveFocusData = true;

          const int headerSize = 384;

          byte[] result = _stillImageDevice.ExecuteReadData(CONST_CMD_GetLiveViewImage);
          if (result == null || result.Length <= headerSize)
            return null;
          int cbBytesRead = result.Length;
          GetAditionalLIveViewData(viewData, result);
          MemoryStream copy = new MemoryStream((int)cbBytesRead - headerSize);
          copy.Write(result, headerSize, (int)cbBytesRead - headerSize);
          copy.Close();
          viewData.ImageData = copy.GetBuffer();
        }
        finally
        {
          Monitor.Exit(Locker);
        }
      }
      return viewData;
    }

    public override void CapturePhoto()
    {
      Monitor.Enter(Locker);
      try
      {
        DeviceReady();
        ErrorCodes.GetException(CaptureInSdRam
                                  ? _stillImageDevice.ExecuteWithNoData(CONST_CMD_AfAndCaptureRecInSdram)
                                  : _stillImageDevice.ExecuteWithNoData(CONST_CMD_InitiateCapture));
      }
      catch (COMException comException)
      {
        ErrorCodes.GetException(comException);
      }
      finally
      {
        Monitor.Exit(Locker); 
      }
    }

    protected virtual void GetAditionalLIveViewData(LiveViewData viewData, byte[] result)
    {
      viewData.LiveViewImageWidth = ToInt16(result, 0);
      viewData.LiveViewImageHeight = ToInt16(result, 2);

      viewData.ImageWidth = ToInt16(result, 4);
      viewData.ImageHeight = ToInt16(result, 6);

      viewData.FocusFrameXSize = ToInt16(result, 16);
      viewData.FocusFrameYSize = ToInt16(result, 18);

      viewData.FocusX = ToInt16(result, 20);
      viewData.FocusY = ToInt16(result, 22);

      viewData.Focused = result[40] != 1;
    }

    public override void Focus(int step)
    {
      if (step == 0)
        return;
      lock (Locker)
      {
        DeviceReady();
        ErrorCodes.GetException(step > 0
                                  ? _stillImageDevice.ExecuteWithNoData(CONST_CMD_MfDrive, 0x00000001, (uint) step)
                                  : _stillImageDevice.ExecuteWithNoData(CONST_CMD_MfDrive, 0x00000002, (uint) -step));
      }
    }

    public override void AutoFocus()
    {
      lock (Locker)
      {
        DeviceReady();
        ErrorCodes.GetException(_stillImageDevice.ExecuteWithNoData(CONST_CMD_AfDrive));
      }
    }

    public override void CapturePhotoNoAf()
    {
      lock (Locker)
      {
        byte[] val = null;
        byte oldval = 0;
        try
        {
          DeviceReady();
          MTPDataResponse response = ExecuteReadDataEx(CONST_CMD_GetDevicePropValue, CONST_PROP_LiveViewStatus, -1);
          ErrorCodes.GetException(response.ErrorCode);
          // test if live view is on 
          if (response.Data != null && response.Data.Length > 0 && response.Data[0] > 0)
          {
            if (CaptureInSdRam)
            {
              DeviceReady();
              ErrorCodes.GetException(_stillImageDevice.ExecuteWithNoData(CONST_CMD_InitiateCaptureRecInSdram, 0xFFFFFFFF));
              return;
            }
            StopLiveView();
          }

          DeviceReady();
          val = _stillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropValue, CONST_PROP_AFModeSelect, -1);
          if (val != null && val.Length > 0)
            oldval = val[0];
          SetProperty(CONST_CMD_SetDevicePropValue, new[] { (byte)4 }, CONST_PROP_AFModeSelect, -1);
          DeviceReady();
          ErrorCodes.GetException(CaptureInSdRam
                                    ? ExecuteWithNoData(CONST_CMD_InitiateCaptureRecInSdram, 0xFFFFFFFF)
                                    : ExecuteWithNoData(CONST_CMD_InitiateCapture));
          DeviceReady();
        }
        finally
        {
          if (val != null && val.Length > 0)
            SetProperty(CONST_CMD_SetDevicePropValue, new[] { oldval }, CONST_PROP_AFModeSelect, -1);
        }

      }
    }

    public override void Focus(int x, int y)
    {
      lock (Locker)
      {
        DeviceReady();
        ErrorCodes.GetException(_stillImageDevice.ExecuteWithNoData(CONST_CMD_ChangeAfArea, (uint) x, (uint) y));
      }
    }

    public override void Close()
    {
      lock (Locker)
      {
        try
        {
          DeviceReady();
          _timer.Stop();
          _stillImageDevice.Disconnect();
          HaveLiveView = false;
        }
        catch (Exception exception)
        {
          ServiceProvider.Log.Error("Close camera",exception);
        }
      }
    }

    public static short ToInt16(byte[] value, int startIndex)
    {
      int i = (short) (value[startIndex] << 8 | value[startIndex+1]);
      return (short) (i);
      //return System.BitConverter.ToInt16(value.Reverse().ToArray(), value.Length - sizeof(Int16) - startIndex);
    }

    private byte _liveViewImageZoomRatio;

    //public override PropertyValue<int> LiveViewImageZoomRatio
    //{
    //  get
    //  {
    //    if (_stillImageDevice == null)
    //      return 0;
    //    lock (Locker)
    //    {
    //      byte[] data = _stillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropValue,
    //                                                      CONST_PROP_LiveViewImageZoomRatio,
    //                                                      -1);
    //      if (data != null && data.Length == 1)
    //      {
    //        _liveViewImageZoomRatio = data[0];
    //        ////NotifyPropertyChanged("LiveViewImageZoomRatio");
    //      }
    //    }
    //    return _liveViewImageZoomRatio;
    //  }
    //  set
    //  {
    //    lock (Locker)
    //    {
    //      if (_stillImageDevice.ExecuteWriteData(CONST_CMD_SetDevicePropValue, new[] {value},
    //                                             CONST_PROP_LiveViewImageZoomRatio, -1) == 0)
    //        _liveViewImageZoomRatio = value;
    //      NotifyPropertyChanged("LiveViewImageZoomRatio");
    //    }
    //  }
    //}

    public override void ReadDeviceProperties(int prop)
    {
      lock (Locker)
      {
        try
        {
          HaveLiveView = true;
          switch (prop)
          {
            case CONST_PROP_Fnumber:
              //FNumber.SetValue(_stillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropValue, CONST_PROP_Fnumber));
              ReInitFNumber();
              break;
            case CONST_PROP_ExposureIndex:
              IsoNumber.SetValue(_stillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropValue,
                                                                   CONST_PROP_ExposureIndex));
              break;
            case CONST_PROP_ExposureTime:
              ShutterSpeed.SetValue(_stillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropValue,
                                                                      CONST_PROP_ExposureTime));
              break;
            case CONST_PROP_WhiteBalance:
              WhiteBalance.SetValue(_stillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropValue,
                                                                      CONST_PROP_WhiteBalance));
              break;
            case CONST_PROP_ExposureProgramMode:
              Mode.SetValue(_stillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropValue,
                                                              CONST_PROP_ExposureProgramMode));
              break;
            case CONST_PROP_ExposureBiasCompensation:
              ExposureCompensation.SetValue(_stillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropValue,
                                                                              CONST_PROP_ExposureBiasCompensation));
              break;
            case CONST_PROP_CompressionSetting:
              CompressionSetting.SetValue(_stillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropValue,
                                                                            CONST_PROP_CompressionSetting));
              break;
            case CONST_PROP_ExposureMeteringMode:
              ExposureMeteringMode.SetValue(_stillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropValue,
                                                                              CONST_PROP_ExposureMeteringMode));
              break;
            case CONST_PROP_FocusMode:
              FocusMode.SetValue(_stillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropValue, CONST_PROP_FocusMode));
              break;
            case CONST_PROP_BatteryLevel:
              Battery = _stillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropValue, CONST_PROP_BatteryLevel, -1)[0];
              break;
            case CONST_PROP_ExposureIndicateStatus:
              sbyte i =
                unchecked(
                  (sbyte)
                  _stillImageDevice.ExecuteReadData(CONST_CMD_GetDevicePropValue, CONST_PROP_ExposureIndicateStatus, -1)
                    [0]);
              ExposureStatus = Convert.ToInt32(i);
              break;
          }
        }
        catch (Exception ex)
        {
        }
      }
    }

    public override void TransferFile(object o, string filename)
    {
      DeviceReady();
      PortableDeviceEventArgs deviceEventArgs = o as PortableDeviceEventArgs;
      if (deviceEventArgs != null)
      {
        //_stillImageDevice.SaveFile(deviceEventArgs.EventType.DeviceObject, filename);
        byte[] result = _stillImageDevice.ExecuteReadBigData(CONST_CMD_GetObject,
                                                             (int) deviceEventArgs.EventType.ObjectHandle, -1);
        using (BinaryWriter writer=new BinaryWriter(File.Open(filename,FileMode.Create)))
        {
          writer.Write(result);
        }
      }
    }

    public override void LockCamera()
    {
      _stillImageDevice.ExecuteWithNoData(CONST_CMD_ChangeCameraMode, 1);
    }

    public override void UnLockCamera()
    {
      _stillImageDevice.ExecuteWithNoData(CONST_CMD_ChangeCameraMode, 0);
    }

    public override void StartRecordMovie()
    {
      base.StartRecordMovie();
      DeviceReady();
      ErrorCodes.GetException(_stillImageDevice.ExecuteWithNoData(CONST_CMD_StartMovieRecInCard));
    }

    public override void StopRecordMovie()
    {
      base.StopRecordMovie();
      DeviceReady();
      ErrorCodes.GetException(_stillImageDevice.ExecuteWithNoData(CONST_CMD_EndMovieRec));
    }

    private void getEvent()
    {
      DeviceReady();
      MTPDataResponse response = ExecuteReadDataEx(CONST_CMD_GetEvent);

      if (response.Data == null)
      {
        ServiceProvider.Log.Debug("Get event error :" + response.ErrorCode.ToString("X"));
        return;
      }
      int eventCount = BitConverter.ToInt16(response.Data, 0);
      if (eventCount > 0)
      {
        for (int i = 0; i < eventCount; i++)
        {
          DeviceReady();
          uint eventCode = BitConverter.ToUInt16(response.Data, 6 * i + 2);
          ushort eventParam = BitConverter.ToUInt16(response.Data, 6 * i + 4);
          int longeventParam = BitConverter.ToInt32(response.Data, 6 * i + 4);
          switch (eventCode)
          {
            case CONST_Event_DevicePropChanged:
              ReadDeviceProperties(eventParam);
              break;
            case CONST_Event_ObjectAddedInSdram:
            case CONST_Event_ObjectAdded:
              {
                byte[] objectdata = _stillImageDevice.ExecuteReadData(CONST_CMD_GetObjectInfo, longeventParam);
                string filename = Encoding.Unicode.GetString(objectdata, 53, 12*2);
                if (filename.Contains("\0"))
                  filename = filename.Split('\0')[0];
                PhotoCapturedEventArgs args = new PhotoCapturedEventArgs
                {
                  WiaImageItem = null,
                  EventArgs = new PortableDeviceEventArgs(new PortableDeviceEventType() { ObjectHandle = (uint)longeventParam }),
                  CameraDevice = this,
                  FileName = filename
                };
                if (PhotoCaptured != null)
                  PhotoCaptured(this, args);
              }
              break;
            case CONST_Event_CaptureComplete:
            case CONST_Event_CaptureCompleteRecInSdram:
              {
                OnCaptureCompleted(this, new EventArgs());
              }
              break;
            default:
              Console.WriteLine("Unknow event code " + eventCode.ToString("X"));
              ServiceProvider.Log.Debug("Unknow event code :" + eventCode.ToString("X") + "|" +
                                        longeventParam.ToString("X"));
              break;
          }
        }
      }
    }

    public void DeviceReady()
    {
      DeviceReady(0);
    }

    public void DeviceReady(int retrynum)
    {
      if (retrynum > 100)
        return;
      //uint cod = Convert.ToUInt32(_stillImageDevice.ExecuteWithNoData(CONST_CMD_DeviceReady));
      ulong cod = (ulong)_stillImageDevice.ExecuteWithNoData(CONST_CMD_DeviceReady);
      if (cod != 0 || cod != ErrorCodes.MTP_OK)
      {
        if (cod == ErrorCodes.MTP_Device_Busy || cod == 0x800700AA)
        {
          //Console.WriteLine("Device not ready");
          //Thread.Sleep(50);
          retrynum++;
          DeviceReady(retrynum);
        }
        else
        {
          //Console.WriteLine("Device ready code #0" + cod.ToString("X"));
        }
      }
    }

    protected void SetProperty(int code, byte[] data, int param1, int param2)
    {
      bool retry = false;
      int retrynum = 0;
      DeviceReady();
      do
      {
        if (retrynum > 5)
        {
          return;
        }
        retry = false;
        uint resp = _stillImageDevice.ExecuteWriteData(code, data, param1, param2);
        if (resp != 0 || resp != ErrorCodes.MTP_OK)
        {
          //Console.WriteLine("Retry ...." + resp.ToString("X"));
          if (resp == ErrorCodes.MTP_Device_Busy || resp == 0x800700AA)
          {
            Thread.Sleep(100);
            retry = true;
            retrynum++;
          }
        }
      } while (retry);
    }

    public override event PhotoCapturedEventHandler PhotoCaptured;
    //public override event EventHandler CaptureCompleted;

  }
}