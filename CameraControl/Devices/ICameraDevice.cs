﻿using CameraControl.Classes;
using CameraControl.Devices.Classes;

namespace CameraControl.Devices
{
  public interface ICameraDevice
  {
    bool HaveLiveView { get; set; }
    PropertyValue<int> FNumber { get; set; }
    PropertyValue<int> IsoNumber { get; set; }
    PropertyValue<long> ShutterSpeed { get; set; }
    PropertyValue<long> WhiteBalance { get; set; }
    PropertyValue<uint> Mode { get; set; }
    PropertyValue<int> ExposureCompensation { get; set; }
    PropertyValue<int> CompressionSetting { get; set; }
    PropertyValue<int> ExposureMeteringMode { get; set; }
    PropertyValue<uint> FocusMode { get; set; }
    bool IsConected { get; set; }
    string DeviceName { get; set; }
    string Manufacturer { get; set; }

    int Battery { get; set; }
    byte LiveViewImageZoomRatio { get; set; }

    bool Init(string id, WIAManager manager);
    void StartLiveView();
    void StopLiveView();
    LiveViewData GetLiveViewImage();
    void AutoFocus();
    void Focus(int step);
    void Focus(int x, int y);
    void TakePictureNoAf();
    void TakePicture();
    void Close();
    void ReadDeviceProperties(int prop);
  }
}