﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using CameraControl.Classes;
using CameraControl.Core;
using CameraControl.Core.Classes;
using CameraControl.Core.Devices;
using CameraControl.Core.Devices.Classes;
using CameraControl.Core.Interfaces;
using CameraControl.Devices;
using CameraControl.Devices.Classes;
using CameraControl.Layouts;
using CameraControl.Translation;
using CameraControl.windows;
using MahApps.Metro;
using MahApps.Metro.Controls;
using Application = System.Windows.Application;
using EditSession = CameraControl.windows.EditSession;
using HelpProvider = CameraControl.Classes.HelpProvider;
using MessageBox = System.Windows.Forms.MessageBox;
using Path = System.IO.Path;

namespace CameraControl
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : MetroWindow
  {

    public PropertyWnd PropertyWnd { get; set; }

    //public WIAManager WiaManager { get; set; }

    public MainWindow()
    {
      CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (sender, args) => this.Close()));
      SelectDeviceCommand = new RelayCommand<ICameraDevice>(SelectCamera);
      SelectPresetCommand = new RelayCommand<CameraPreset>(SelectPreset);
      ExecuteExportPluginCommand = new RelayCommand<IExportPlugin>(ExecuteExportPlugin);
      //WiaManager = new WIAManager();
      //ServiceProvider.Settings.Manager = WiaManager;
      ServiceProvider.DeviceManager.PhotoCaptured += DeviceManager_PhotoCaptured;
      InitializeComponent();
      DataContext = ServiceProvider.Settings;
      if ((DateTime.Now - ServiceProvider.Settings.LastUpdateCheckDate).TotalDays > 7)
      {
        ServiceProvider.Settings.LastUpdateCheckDate = DateTime.Now;
        ServiceProvider.Settings.Save();
        CheckForUpdate();
      }
      ServiceProvider.Settings.SessionSelected += Settings_SessionSelected;
      ServiceProvider.DeviceManager.CameraConnected += DeviceManager_CameraConnected;
      ServiceProvider.DeviceManager.CameraSelected += DeviceManager_CameraSelected;

      SetLayout(ServiceProvider.Settings.SelectedLayout);
      ServiceProvider.Settings.ApplyTheme(this);
    }

    private void ExecuteExportPlugin(IExportPlugin obj)
    {
      obj.Execute();
    }

    void Settings_SessionSelected(PhotoSession oldvalue, PhotoSession newvalue)
    {
      if (oldvalue != null)
        ServiceProvider.Settings.Save(oldvalue);
      ServiceProvider.QueueManager.Clear();
      if (ServiceProvider.DeviceManager.SelectedCameraDevice != null)
        ServiceProvider.DeviceManager.SelectedCameraDevice.AttachedPhotoSession = newvalue;
    }

    void DeviceManager_CameraSelected(ICameraDevice oldcameraDevice, ICameraDevice newcameraDevice)
    {
      if(newcameraDevice==null)
        return;
      // load session data only if not sessiom attached to the selected camera
      if (newcameraDevice.AttachedPhotoSession == null)
      {
        CameraProperty property = ServiceProvider.Settings.CameraProperties.Get(newcameraDevice);
        newcameraDevice.AttachedPhotoSession = ServiceProvider.Settings.GetSession(property.PhotoSessionName);
      }
      if (newcameraDevice.AttachedPhotoSession != null)
        ServiceProvider.Settings.DefaultSession = (PhotoSession)newcameraDevice.AttachedPhotoSession;
      Dispatcher.Invoke(
        new Action(
          delegate
            {
              btn_capture_noaf.IsEnabled = newcameraDevice.GetCapability(CapabilityEnum.CaptureNoAf);
              btn_liveview.IsEnabled = newcameraDevice.GetCapability(CapabilityEnum.LiveView);
            }));
    }

    void DeviceManager_CameraConnected(ICameraDevice cameraDevice)
    {
      CameraProperty property = ServiceProvider.Settings.CameraProperties.Get(cameraDevice);
      cameraDevice.DisplayName = property.DeviceName;
      cameraDevice.AttachedPhotoSession = ServiceProvider.Settings.GetSession(property.PhotoSessionName);
    }

    void DeviceManager_PhotoCaptured(object sender, PhotoCapturedEventArgs eventArgs)
    {
      Thread thread = new Thread(PhotoCaptured);
      thread.Start(eventArgs);
    }

    private void CheckForUpdate()
    {
      if(PhotoUtils.CheckForUpdate())
        Close();
    }

    void PhotoCaptured(object o)
    {
      PhotoCapturedEventArgs eventArgs = o as PhotoCapturedEventArgs;
      if (eventArgs == null)
        return;
      try
      {
        StaticHelper.Instance.SystemMessage = TranslationStrings.MsgPhotoTransferBegin;
        Log.Debug("Photo transfer begin.");
        PhotoSession session =(PhotoSession) eventArgs.CameraDevice.AttachedPhotoSession ?? ServiceProvider.Settings.DefaultSession;
        if ((session.NoDownload && !eventArgs.CameraDevice.CaptureInSdRam))
        {
          eventArgs.CameraDevice.IsBusy = false;
          return;
        }
        string fileName = "";
        if (!session.UseOriginalFilename || eventArgs.CameraDevice.CaptureInSdRam)
        {
          fileName =
            session.GetNextFileName(Path.GetExtension(eventArgs.FileName),
                                    eventArgs.CameraDevice);
        }
        else
        {
          fileName = Path.Combine(session.Folder, eventArgs.FileName);
          if (File.Exists(fileName))
            fileName =
              PhotoUtils.GetUniqueFilename(
                Path.GetDirectoryName(fileName) + "\\" + Path.GetFileNameWithoutExtension(fileName) + "_", 0,
                Path.GetExtension(fileName));
        }
        if (!Directory.Exists(Path.GetDirectoryName(fileName)))
        {
          Directory.CreateDirectory(Path.GetDirectoryName(fileName));
        }
        Log.Debug("Transfer started :" + fileName);
        eventArgs.CameraDevice.TransferFile(eventArgs.EventArgs, fileName);
        Log.Debug("Transfer done :" + fileName);
        //select the new file only when the multiple camera support isn't used to prevent high CPU usage on raw files
        if (ServiceProvider.Settings.AutoPreview && !ServiceProvider.WindowsManager.Get(typeof(MultipleCameraWnd)).IsVisible && !ServiceProvider.Settings.UseExternalViewer)
        {
          ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.Select_Image, session.AddFile(fileName));
        }
        else
        {
          session.AddFile(fileName);
        }
        ServiceProvider.Settings.Save(session);
        StaticHelper.Instance.SystemMessage = TranslationStrings.MsgPhotoTransferDone;
        eventArgs.CameraDevice.IsBusy = false;
        //show fullscreen only when the multiple camera support isn't used
        if (ServiceProvider.Settings.Preview && !ServiceProvider.WindowsManager.Get(typeof(MultipleCameraWnd)).IsVisible && !ServiceProvider.Settings.UseExternalViewer)
          ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.FullScreenWnd_ShowTimed);
        if(ServiceProvider.Settings.UseExternalViewer && File.Exists(ServiceProvider.Settings.ExternalViewerPath))
        {
          string arg = ServiceProvider.Settings.ExternalViewerArgs;
          arg = arg.Contains("%1") ? arg.Replace("%1", fileName) : arg + " " + fileName;
          PhotoUtils.Run(ServiceProvider.Settings.ExternalViewerPath, arg, ProcessWindowStyle.Normal);
        }
        if (ServiceProvider.Settings.PlaySound)
        {
          PhotoUtils.PlayCaptureSound();
        }
      }
      catch (Exception ex)
      {
        eventArgs.CameraDevice.IsBusy = false;
        StaticHelper.Instance.SystemMessage = string.Format(TranslationStrings.MsgPhotoTransferError, ex.Message);
        Log.Error("Transfer error !", ex);
      }
    }

    /// <summary>
    /// Gets the command for selecting a device
    /// </summary>
    public RelayCommand<ICameraDevice> SelectDeviceCommand
    {
      get;
      private set;
    }

    public RelayCommand<CameraPreset> SelectPresetCommand
    {
      get;
      private set;
    }

    public RelayCommand<IExportPlugin> ExecuteExportPluginCommand
    {
      get;
      private set;
    }

    private void SelectCamera(ICameraDevice cameraDevice)
    {
      ServiceProvider.DeviceManager.SelectedCameraDevice = cameraDevice;
    }

    private void SelectPreset(CameraPreset preset)
    {
      preset.Set(ServiceProvider.DeviceManager.SelectedCameraDevice);
    }
    
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
      ServiceProvider.DeviceManager.DisableNativeDrivers = ServiceProvider.Settings.DisableNativeDrivers;
      ServiceProvider.DeviceManager.ConnectToCamera();
    }


    private void button3_Click(object sender, RoutedEventArgs e)
    {
      Log.Debug("Main window capture started");
      //if (!ServiceProvider.Settings.DefaultSession.TimeLapse.IsDisabled)
      //{
      //  if (
      //    MessageBox.Show("A time lapse photo session runnig !\n Do you want to stop it  ?",
      //                    "", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
      //  {
      //    ServiceProvider.Settings.DefaultSession.TimeLapse.Stop();
      //    return;
      //  }
      //}

      try
      {
        if (ServiceProvider.DeviceManager.SelectedCameraDevice.ShutterSpeed.Value == "Bulb")
        {
          if (ServiceProvider.DeviceManager.SelectedCameraDevice.GetCapability(CapabilityEnum.Bulb))
          {
            BulbWnd wnd = new BulbWnd();
            wnd.ShowDialog();
            return;
          }
          else
          {
            StaticHelper.Instance.SystemMessage = TranslationStrings.MsgBulbModeNotSupported;
            return;
          }
        }
        ServiceProvider.DeviceManager.SelectedCameraDevice.CapturePhoto();
      }
      catch (DeviceException exception)
      {
        StaticHelper.Instance.SystemMessage = exception.Message;
        Log.Error("Take photo", exception);
      }
      catch (Exception exception)
      {
        MessageBox.Show("No picture was taken !\n" + exception.Message);
        Log.Error("Take photo", exception);
      }
    }

    private void btn_edit_Sesion_Click(object sender, RoutedEventArgs e)
    {
      if (File.Exists(ServiceProvider.Settings.DefaultSession.ConfigFile))
      {
        File.Delete(ServiceProvider.Settings.DefaultSession.ConfigFile);
      }
      EditSession editSession = new EditSession(ServiceProvider.Settings.DefaultSession);
      editSession.ShowDialog();
      ServiceProvider.Settings.Save(ServiceProvider.Settings.DefaultSession);
    }

    private void btn_add_Sesion_Click(object sender, RoutedEventArgs e)
    {
      EditSession editSession = new EditSession(new PhotoSession());
      if (editSession.ShowDialog() == true)
      {
        ServiceProvider.Settings.Add(editSession.Session);
        ServiceProvider.Settings.DefaultSession = editSession.Session;
      }
    }

    private void button1_Click(object sender, RoutedEventArgs e)
    {
      if (button1.IsChecked == true)
      {
        if (PropertyWnd == null)
        {
          PropertyWnd = new PropertyWnd();
        }
        PropertyWnd.IsVisibleChanged -= PropertyWnd_IsVisibleChanged;
        PropertyWnd.Show();
        PropertyWnd.IsVisibleChanged += PropertyWnd_IsVisibleChanged;
      }
      else
      {
        if (PropertyWnd != null && PropertyWnd.Visibility == Visibility.Visible)
        {
          PropertyWnd.IsVisibleChanged -= PropertyWnd_IsVisibleChanged;
          PropertyWnd.Hide();
        }
      }
    }

    private void PropertyWnd_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
      button1.IsChecked = !button1.IsChecked;
    }

    private void Window_Closed(object sender, EventArgs e)
    {
      if (PropertyWnd != null)
      {
        PropertyWnd.Hide();
        PropertyWnd.Close();
      }
      ServiceProvider.WindowsManager.ExecuteCommand(CmdConsts.All_Close);
      //WiaManager.DisconnectCamera();
    }

    private void but_timelapse_Click(object sender, RoutedEventArgs e)
    {
      if (PropertyWnd != null && PropertyWnd.IsVisible)
        PropertyWnd.Topmost = false;
      TimeLapseWnd wnd = new TimeLapseWnd();
      wnd.ShowDialog();
      if (PropertyWnd != null && PropertyWnd.IsVisible)
        PropertyWnd.Topmost = true;
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
      //if (!ServiceProvider.Settings.DefaultSession.TimeLapse.IsDisabled)
      //{
      //  if (
      //    MessageBox.Show("A time lapse photo session runnig !\n Do you want to stop it and exit from application ?",
      //                    "Closing", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.No)
      //  {
      //    e.Cancel = true;
      //  }
      //  else
      //  {
      //    ServiceProvider.Settings.DefaultSession.TimeLapse.Stop();
      //  }
      //}
    }

    private void but_fullscreen_Click(object sender, RoutedEventArgs e)
    {
      ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.FullScreenWnd_Show);
    }

    private void btn_liveview_Click(object sender, RoutedEventArgs e)
    {
      ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.LiveViewWnd_Show);
    }

    private void btn_about_Click(object sender, RoutedEventArgs e)
    {
      AboutWnd wnd=new AboutWnd();
      wnd.ShowDialog();
    }



    private void btn_br_Click(object sender, RoutedEventArgs e)
    {
      BraketingWnd wnd = new BraketingWnd(ServiceProvider.DeviceManager.SelectedCameraDevice, ServiceProvider.Settings.DefaultSession);
      wnd.ShowDialog();
    }



    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {
      PhotoUtils.Run("http://www.digicamcontrol.com/", "");
    }

    private void MenuItem_Click_1(object sender, RoutedEventArgs e)
    {
      if(PhotoUtils.CheckForUpdate())
      {
        Close();
      }
      else
      {
        MessageBox.Show(TranslationStrings.MsgApplicationUpToDate); 
      }
    }

    private void MenuItem_Click_2(object sender, RoutedEventArgs e)
    {
      ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.MultipleCameraWnd_Show);
    }

    private void mnu_reconnect_Click(object sender, RoutedEventArgs e)
    {
      ServiceProvider.DeviceManager.DisableNativeDrivers = ServiceProvider.Settings.DisableNativeDrivers;
      ServiceProvider.DeviceManager.ConnectToCamera();
    }

    private void MenuItem_Click_3(object sender, RoutedEventArgs e)
    {
      ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.CameraPropertyWnd_Show,
                                                    ServiceProvider.DeviceManager.SelectedCameraDevice);
    }

    private void MenuItem_Click_4(object sender, RoutedEventArgs e)
    {
      CameraPreset cameraPreset = new CameraPreset();
      SavePresetWnd wnd=new SavePresetWnd(cameraPreset);
      if (wnd.ShowDialog() == true)
      {
        foreach (CameraPreset preset in ServiceProvider.Settings.CameraPresets)
        {
          if(preset.Name==cameraPreset.Name)
          {
            cameraPreset = preset;
            break;
          }
        }
        cameraPreset.Get(ServiceProvider.DeviceManager.SelectedCameraDevice);
        if (!ServiceProvider.Settings.CameraPresets.Contains(cameraPreset))
          ServiceProvider.Settings.CameraPresets.Add(cameraPreset);
        ServiceProvider.Settings.Save();
      }
    }

    private void MenuItem_Click_5(object sender, RoutedEventArgs e)
    {
      PresetEditWnd wnd = new PresetEditWnd();
      wnd.ShowDialog();
    }


    private void btn_browse_Click(object sender, RoutedEventArgs e)
    {
      ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.BrowseWnd_Show);
    }

    private void btn_capture_noaf_Click(object sender, RoutedEventArgs e)
    {
      Log.Debug("Main window capture no af started");
      try
      {
        if (ServiceProvider.DeviceManager.SelectedCameraDevice.ShutterSpeed.Value == "Bulb")
        {
          if (ServiceProvider.DeviceManager.SelectedCameraDevice.GetCapability(CapabilityEnum.Bulb))
          {
            BulbWnd wnd = new BulbWnd();
            wnd.ShowDialog();
            return;
          }
          else
          {
            StaticHelper.Instance.SystemMessage = TranslationStrings.MsgBulbModeNotSupported;
            return;
          }
        }
        ServiceProvider.DeviceManager.SelectedCameraDevice.CapturePhotoNoAf();
      }
      catch (DeviceException exception)
      {
        StaticHelper.Instance.SystemMessage = exception.Message;
        Log.Error("Take photo", exception);
      }
      catch (Exception exception)
      {
        MessageBox.Show("No picture was taken !\n" + exception.Message);
        Log.Error("Take photo", exception);
      }
    }

    void SetLayout(string enumname)
    {
      LayoutTypeEnum type;
      if (Enum.TryParse(enumname, true, out type))
      {
        
      }
      SetLayout(type);
    }

    void SetLayout(LayoutTypeEnum type)
    {
      ServiceProvider.Settings.SelectedLayout = type.ToString();
      switch (type)
      {
        case LayoutTypeEnum.Normal:
          {
            StackLayout.Children.Clear();
            LayoutNormal control = new LayoutNormal();
            StackLayout.Children.Add(control);
          }
          break;
        case LayoutTypeEnum.Grid:
          {
            StackLayout.Children.Clear();
            LayoutGrid control = new LayoutGrid();
            StackLayout.Children.Add(control);
          }
          break;
        case LayoutTypeEnum.GridRight:
          {
            StackLayout.Children.Clear();
            LayoutGridRight control = new LayoutGridRight();
            StackLayout.Children.Add(control);
          }
          break;
      }
    }

    private void MenuItem_Click_6(object sender, RoutedEventArgs e)
    {
      SetLayout(LayoutTypeEnum.Normal);
    }

    private void MenuItem_Click_7(object sender, RoutedEventArgs e)
    {
      SetLayout(LayoutTypeEnum.GridRight);
    }

    private void MenuItem_Click_8(object sender, RoutedEventArgs e)
    {
      SetLayout(LayoutTypeEnum.Grid);
    }

    private void btn_Tags_Click(object sender, RoutedEventArgs e)
    {
      if(ServiceProvider.Settings.DefaultSession.Tags.Count==0)
      {
        MessageBox.Show(TranslationStrings.MsgUseSessionEditorTags);
        return;
      }
      ServiceProvider.WindowsManager.ExecuteCommand(ServiceProvider.WindowsManager.Get(typeof(TagSelectorWnd)).IsVisible
                                                      ? WindowsCmdConsts.TagSelectorWnd_Hide
                                                      : WindowsCmdConsts.TagSelectorWnd_Show);
    }

    private void btn_del_Sesion_Click(object sender, RoutedEventArgs e)
    {
      if(ServiceProvider.Settings.PhotoSessions.Count>1)
      {
        if (MessageBox.Show(string.Format(TranslationStrings.MsgDeleteSessionQuestion,ServiceProvider.Settings.DefaultSession.Name),"Delete session",MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
        {
          PhotoSession session = ServiceProvider.Settings.DefaultSession;
          ServiceProvider.Settings.DefaultSession = ServiceProvider.Settings.PhotoSessions[0];
          File.Delete(session.ConfigFile);
          ServiceProvider.Settings.PhotoSessions.Remove(session);
          ServiceProvider.Settings.Save();
        }
      }
      else
      {
        MessageBox.Show(TranslationStrings.MsgLastSessionCantBeDeleted);
      }
    }

    private void btn_settings_Click(object sender, RoutedEventArgs e)
    {
      if (PropertyWnd != null && PropertyWnd.IsVisible)
        PropertyWnd.Topmost = false;
      SettingsWnd wnd = new SettingsWnd();
      wnd.ShowDialog();
      if (PropertyWnd != null && PropertyWnd.IsVisible)
        PropertyWnd.Topmost = true;
    }

    private void btn_menu_Click(object sender, RoutedEventArgs e)
    {
      Flyouts[0].IsOpen = !Flyouts[0].IsOpen;
      Flyouts[1].IsOpen = !Flyouts[1].IsOpen;
    }

    private void mnu_forum_Click(object sender, RoutedEventArgs e)
    {
      PhotoUtils.Run("http://www.digicamcontrol.com/forum/", "");
    }

    private void btn_getRaw_Click(object sender, RoutedEventArgs e)
    {
      PhotoUtils.Run("http://www.microsoft.com/en-us/download/details.aspx?id=26829", "");
    }

    private void btn_donate_Click(object sender, RoutedEventArgs e)
    {
      PhotoUtils.Donate();
    }

    private void btn_help_Click(object sender, RoutedEventArgs e)
    {
      HelpProvider.Run(HelpSections.MainMenu);
    }

    private void but_download_Click(object sender, RoutedEventArgs e)
    {
      ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.DownloadPhotosWnd_Show,ServiceProvider.DeviceManager.SelectedCameraDevice);
    }

  }
}
