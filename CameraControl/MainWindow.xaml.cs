﻿using System;
using System.Linq;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CameraControl.Classes;
using CameraControl.Core;
using CameraControl.Core.Classes;
using CameraControl.Core.Interfaces;
using CameraControl.Core.Translation;
using CameraControl.Devices;
using CameraControl.Devices.Classes;
using CameraControl.Layouts;
using CameraControl.windows;
using MahApps.Metro.Controls;
using EditSession = CameraControl.windows.EditSession;
using FileInfo = System.IO.FileInfo;
using HelpProvider = CameraControl.Classes.HelpProvider;
using MessageBox = System.Windows.MessageBox;
//using MessageBox = System.Windows.Forms.MessageBox;
using Path = System.IO.Path;
using Timer = System.Timers.Timer;

namespace CameraControl
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow, IMainWindowPlugin, INotifyPropertyChanged
    {

        public PropertyWnd PropertyWnd { get; set; }
        public string DisplayName { get; set; }

        private object _locker = new object();
        private FileItem _selectedItem = null;
        private Timer _selectiontimer = new Timer(4000);
        private DateTime _lastLoadTime = DateTime.Now;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow" /> class.
        /// </summary>
        public MainWindow()
        {
            DisplayName = "Default";
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (sender1, args) => this.Close()));

            SelectPresetCommand = new RelayCommand<CameraPreset>(SelectPreset);
            ExecuteExportPluginCommand = new RelayCommand<IExportPlugin>(ExecuteExportPlugin);
            ExecuteToolPluginCommand = new RelayCommand<IToolPlugin>(ExecuteToolPlugin);
            InitializeComponent();
            if (!string.IsNullOrEmpty(ServiceProvider.Branding.ApplicationTitle))
            {
                Title = ServiceProvider.Branding.ApplicationTitle;
            }
            if (!string.IsNullOrEmpty(ServiceProvider.Branding.LogoImage) && File.Exists(ServiceProvider.Branding.LogoImage))
            {
                BitmapImage bi = new BitmapImage();
                // BitmapImage.UriSource must be in a BeginInit/EndInit block.
                bi.BeginInit();
                bi.UriSource = new Uri(ServiceProvider.Branding.LogoImage);
                bi.EndInit();
                Icon = bi;
            }
            _selectiontimer.Elapsed += _selectiontimer_Elapsed;
            _selectiontimer.AutoReset = false;
        }

        void _selectiontimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_selectedItem != null)
                ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.Select_Image, _selectedItem);
        }

        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //WiaManager = new WIAManager();
            //ServiceProvider.Settings.Manager = WiaManager;
            ServiceProvider.DeviceManager.PhotoCaptured += DeviceManager_PhotoCaptured;

            DataContext = ServiceProvider.Settings;
            ServiceProvider.DeviceManager.CameraSelected += DeviceManager_CameraSelected;
            SetLayout(ServiceProvider.Settings.SelectedLayout);
            //ServiceProvider.Settings.ApplyTheme(this);
            var thread = new Thread(CheckForUpdate);
            thread.Start();
        }

        private void CheckForUpdate()
        {
            Thread.Sleep(2000);
            if ((DateTime.Now - ServiceProvider.Settings.LastUpdateCheckDate).TotalDays > 7)
            {
                ServiceProvider.Settings.LastUpdateCheckDate = DateTime.Now;
                ServiceProvider.Settings.Save();
                Dispatcher.Invoke(new Action(() => NewVersionWnd.CheckForUpdate(false)));
            }
        }

        void DeviceManager_CameraSelected(ICameraDevice oldcameraDevice, ICameraDevice newcameraDevice)
        {
            Dispatcher.BeginInvoke(
                new Action(
                    delegate
                        {
                            //btn_capture_noaf.IsEnabled = newcameraDevice != null && newcameraDevice.GetCapability(CapabilityEnum.CaptureNoAf);
                            ((Flyout) Flyouts.Items[0]).IsOpen = false;
                            ((Flyout) Flyouts.Items[1]).IsOpen = false;
                            Title = (ServiceProvider.Branding.ApplicationTitle ?? "digiCamControl") + " - " +
                                    (newcameraDevice == null ? "" : newcameraDevice.DisplayName);
                        }));
        }

        private void ExecuteExportPlugin(IExportPlugin obj)
        {
            HideFlatOuts();
            obj.Execute();
        }

        private void ExecuteToolPlugin(IToolPlugin obj)
        {
            HideFlatOuts();
            obj.Execute();
        }

        private void HideFlatOuts()
        {
            ((Flyout)Flyouts.Items[0]).IsOpen = false;
            ((Flyout)Flyouts.Items[1]).IsOpen = false;
        }

        void DeviceManager_PhotoCaptured(object sender, PhotoCapturedEventArgs eventArgs)
        {
            if (ServiceProvider.Settings.UseParallelTransfer)
            {
                PhotoCaptured(eventArgs);
            }
            else
            {
                lock (_locker)
                {
                    PhotoCaptured(eventArgs);
                }
            }
        }

        void PhotoCaptured(object o)
        {
            PhotoCapturedEventArgs eventArgs = o as PhotoCapturedEventArgs;
            if (eventArgs == null)
                return;
            try
            {
                Log.Debug("Photo transfer begin.");
                eventArgs.CameraDevice.IsBusy = true;
                CameraProperty property = eventArgs.CameraDevice.LoadProperties();
                PhotoSession session = (PhotoSession)eventArgs.CameraDevice.AttachedPhotoSession ??
                                       ServiceProvider.Settings.DefaultSession;
                StaticHelper.Instance.SystemMessage = "";
                if (!eventArgs.CameraDevice.CaptureInSdRam)
                {
                    if (property.NoDownload)
                    {
                        eventArgs.CameraDevice.IsBusy = false;
                        return;
                    }
                    var extension = Path.GetExtension(eventArgs.FileName);
                    if (extension != null && (session.DownloadOnlyJpg && extension.ToLower() != ".jpg"))
                    {
                        eventArgs.CameraDevice.IsBusy = false;
                        return;
                    }
                }
                StaticHelper.Instance.SystemMessage = TranslationStrings.MsgPhotoTransferBegin;
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
                    if (File.Exists(fileName) && !session.AllowOverWrite)
                        fileName =
                          StaticHelper.GetUniqueFilename(
                            Path.GetDirectoryName(fileName) + "\\" + Path.GetFileNameWithoutExtension(fileName) + "_", 0,
                            Path.GetExtension(fileName));
                }
                
                if(session.AllowOverWrite && File.Exists(fileName))
                {
                    File.Delete(fileName);
                }

                if (!Directory.Exists(Path.GetDirectoryName(fileName)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(fileName));
                }
                Log.Debug("Transfer started :" + fileName);
                //DateTime startTIme = DateTime.Now;
                eventArgs.CameraDevice.TransferFile(eventArgs.Handle, fileName);
                //Log.Debug("Transfer done :" + fileName);
                //Log.Debug("[BENCHMARK]Speed :" +
                //          (new FileInfo(fileName).Length / (DateTime.Now - startTIme).TotalSeconds / 1024 / 1024).ToString("0000.00"));
                //Log.Debug("[BENCHMARK]Transfer time :" + ((DateTime.Now - startTIme).TotalSeconds).ToString("0000.000"));
                
                // write comment and tags directly in transferred file
                if (ServiceProvider.Settings.DefaultSession.WriteComment)
                {
                    if (!string.IsNullOrEmpty(ServiceProvider.Settings.DefaultSession.Comment))
                        Exiv2Helper.SaveComment(fileName, ServiceProvider.Settings.DefaultSession.Comment);
                    if (ServiceProvider.Settings.DefaultSession.SelectedTag1!=null && !string.IsNullOrEmpty(ServiceProvider.Settings.DefaultSession.SelectedTag1.Value))
                        Exiv2Helper.AddKeyword(fileName, ServiceProvider.Settings.DefaultSession.SelectedTag1.Value);
                    if (ServiceProvider.Settings.DefaultSession.SelectedTag2 != null && !string.IsNullOrEmpty(ServiceProvider.Settings.DefaultSession.SelectedTag2.Value))
                        Exiv2Helper.AddKeyword(fileName, ServiceProvider.Settings.DefaultSession.SelectedTag2.Value);
                    if (ServiceProvider.Settings.DefaultSession.SelectedTag3 != null && !string.IsNullOrEmpty(ServiceProvider.Settings.DefaultSession.SelectedTag3.Value))
                        Exiv2Helper.AddKeyword(fileName, ServiceProvider.Settings.DefaultSession.SelectedTag3.Value);
                    if (ServiceProvider.Settings.DefaultSession.SelectedTag4 != null && !string.IsNullOrEmpty(ServiceProvider.Settings.DefaultSession.SelectedTag4.Value))
                        Exiv2Helper.AddKeyword(fileName, ServiceProvider.Settings.DefaultSession.SelectedTag4.Value);
                }
                _selectedItem = session.AddFile(fileName);
                //select the new file only when the multiple camera support isn't used to prevent high CPU usage on raw files
                if (ServiceProvider.Settings.AutoPreview &&
                    !ServiceProvider.WindowsManager.Get(typeof(MultipleCameraWnd)).IsVisible &&
                    !ServiceProvider.Settings.UseExternalViewer )
                {
                    if ((Path.GetExtension(fileName).ToLower() == ".jpg" && ServiceProvider.Settings.AutoPreviewJpgOnly) || !ServiceProvider.Settings.AutoPreviewJpgOnly)
                    {

                        if (ServiceProvider.Settings.DelayImageLoading &&
                            (DateTime.Now - _lastLoadTime).TotalSeconds < 4)
                        {
                            _selectiontimer.Stop();
                            _selectiontimer.Start();
                        }
                        else
                        {
                            ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.Select_Image, _selectedItem);
                        }
                    }
                }
                _lastLoadTime = DateTime.Now;
                //ServiceProvider.Settings.Save(session);
                StaticHelper.Instance.SystemMessage = TranslationStrings.MsgPhotoTransferDone;
                eventArgs.CameraDevice.IsBusy = false;
                //show fullscreen only when the multiple camera support isn't used
                if (ServiceProvider.Settings.Preview &&
                    !ServiceProvider.WindowsManager.Get(typeof(MultipleCameraWnd)).IsVisible &&
                    !ServiceProvider.Settings.UseExternalViewer)
                    ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.FullScreenWnd_ShowTimed);
                if (ServiceProvider.Settings.UseExternalViewer && File.Exists(ServiceProvider.Settings.ExternalViewerPath))
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
            Log.Debug("Photo transfer done.");
            // not indicated to be used 
            GC.Collect();
            //GC.WaitForPendingFinalizers();
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

        public RelayCommand<IToolPlugin> ExecuteToolPluginCommand
        {
            get;
            private set;
        }

        private void SelectPreset(CameraPreset preset)
        {
            preset.Set(ServiceProvider.DeviceManager.SelectedCameraDevice);
        }

        private void button3_Click(object sender, RoutedEventArgs e)
        {
            if (ServiceProvider.DeviceManager.SelectedCameraDevice == null)
                return;
            Log.Debug("Main window capture started");
            try
            {
                if (ServiceProvider.DeviceManager.SelectedCameraDevice.ShutterSpeed != null && ServiceProvider.DeviceManager.SelectedCameraDevice.ShutterSpeed.Value == "Bulb")
                {
                    if (ServiceProvider.DeviceManager.SelectedCameraDevice.GetCapability(CapabilityEnum.Bulb))
                    {
                        ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.BulbWnd_Show, ServiceProvider.DeviceManager.SelectedCameraDevice); return;
                    }
                    else
                    {
                        StaticHelper.Instance.SystemMessage = TranslationStrings.MsgBulbModeNotSupported;
                        return;
                    }
                }
                CameraHelper.Capture(ServiceProvider.DeviceManager.SelectedCameraDevice);
            }
            catch (DeviceException exception)
            {
                StaticHelper.Instance.SystemMessage = exception.Message;
                Log.Error("Take photo", exception);
            }
            catch (Exception exception)
            {
                StaticHelper.Instance.SystemMessage = exception.Message;
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

        }

        private void but_fullscreen_Click(object sender, RoutedEventArgs e)
        {
            ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.FullScreenWnd_Show);
        }

        private void btn_about_Click(object sender, RoutedEventArgs e)
        {
            AboutWnd wnd = new AboutWnd();
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
            NewVersionWnd.CheckForUpdate(true);
        }

        private void mnu_reconnect_Click(object sender, RoutedEventArgs e)
        {
            ServiceProvider.DeviceManager.DisableNativeDrivers = ServiceProvider.Settings.DisableNativeDrivers;
            ServiceProvider.DeviceManager.ConnectToCamera();
        }

        private void MenuItem_Click_4(object sender, RoutedEventArgs e)
        {
            CameraPreset cameraPreset = new CameraPreset();
            SavePresetWnd wnd = new SavePresetWnd(cameraPreset);
            if (wnd.ShowDialog() == true)
            {
                foreach (CameraPreset preset in ServiceProvider.Settings.CameraPresets)
                {
                    if (preset.Name == cameraPreset.Name)
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
            if (ServiceProvider.DeviceManager.SelectedCameraDevice == null)
                return;

            Log.Debug("Main window capture no af started");
            try
            {
                if (ServiceProvider.DeviceManager.SelectedCameraDevice.ShutterSpeed!=null && ServiceProvider.DeviceManager.SelectedCameraDevice.ShutterSpeed.Value == "Bulb")
                {
                    if (ServiceProvider.DeviceManager.SelectedCameraDevice.GetCapability(CapabilityEnum.Bulb))
                    {
                        ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.BulbWnd_Show, ServiceProvider.DeviceManager.SelectedCameraDevice); return;
                    }
                    else
                    {
                        StaticHelper.Instance.SystemMessage = TranslationStrings.MsgBulbModeNotSupported;
                        return;
                    }
                }
                CameraHelper.CaptureNoAf(ServiceProvider.DeviceManager.SelectedCameraDevice);
            }
            catch (DeviceException exception)
            {
                StaticHelper.Instance.SystemMessage = exception.Message;
                Log.Error("Take photo", exception);
            }
            catch (Exception exception)
            {
                StaticHelper.Instance.SystemMessage = exception.Message;
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
            ServiceProvider.WindowsManager.ExecuteCommand(ServiceProvider.WindowsManager.Get(typeof(TagSelectorWnd)).IsVisible
                                                            ? WindowsCmdConsts.TagSelectorWnd_Hide
                                                            : WindowsCmdConsts.TagSelectorWnd_Show);
        }

        private void btn_del_Sesion_Click(object sender, RoutedEventArgs e)
        {
            if (ServiceProvider.Settings.PhotoSessions.Count > 1)
            {
                try
                {
                    if (MessageBox.Show(string.Format(TranslationStrings.MsgDeleteSessionQuestion, ServiceProvider.Settings.DefaultSession.Name), TranslationStrings.LabelDeleteSession, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        PhotoSession session = ServiceProvider.Settings.DefaultSession;
                        if (!string.IsNullOrEmpty(session.ConfigFile) && File.Exists(session.ConfigFile))
                            File.Delete(session.ConfigFile);
                        ServiceProvider.Settings.PhotoSessions.Remove(session);
                        ServiceProvider.Settings.DefaultSession = ServiceProvider.Settings.PhotoSessions[0];
                        ServiceProvider.Settings.Save();
                    }
                }
                catch (Exception exception)
                {
                    Log.Error("Unable to remove session", exception);
                    MessageBox.Show(TranslationStrings.LabelUnabletoDeleteSession + exception.Message);
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
            ((Flyout)Flyouts.Items[0]).IsOpen = !((Flyout)Flyouts.Items[0]).IsOpen;
            ((Flyout)Flyouts.Items[1]).IsOpen = !((Flyout)Flyouts.Items[1]).IsOpen;
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
            ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.DownloadPhotosWnd_Show, ServiceProvider.DeviceManager.SelectedCameraDevice);
        }

        private void MetroWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HideFlatOuts();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.MultipleCameraWnd_Show);
            HideFlatOuts();
        }

        private void btn_sort_Click(object sender, RoutedEventArgs e)
        {
            SortCameras(true);
        }

        private void btn_sort_desc_Click(object sender, RoutedEventArgs e)
        {
            SortCameras(false);
        }

        private void SortCameras(bool asc)
        {
            // making sure the camera names are refreshed from properties
            foreach (var device in ServiceProvider.DeviceManager.ConnectedDevices)
            {
                device.LoadProperties();
            }
            if (asc)
            {
                ServiceProvider.DeviceManager.ConnectedDevices =
                    new AsyncObservableCollection<ICameraDevice>(
                        ServiceProvider.DeviceManager.ConnectedDevices.OrderBy(x => x.DisplayName));
            }
            else
            {
                ServiceProvider.DeviceManager.ConnectedDevices =
                    new AsyncObservableCollection<ICameraDevice>(
                        ServiceProvider.DeviceManager.ConnectedDevices.OrderByDescending(x => x.DisplayName));
            }

        }

        private void but_star_Click(object sender, RoutedEventArgs e)
        {
            ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.BulbWnd_Show, ServiceProvider.DeviceManager.SelectedCameraDevice);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            //if (e.Key == Key.Right)
            //{
            //    ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.Next_Image);
            //    e.Handled = true;
            //}
            //if (e.Key == Key.Left)
            //{
            //    ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.Prev_Image);
            //    e.Handled = true;
            //}
            if(e.Key==Key.Delete)
            {
                ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.Del_Image);
                e.Handled = true;
            }
            //if (e.Key == Key.P)
            //{
            //    ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.Like_Image);
            //    e.Handled = true;
            //}
            //if (e.Key == Key.X)
            //{
            //    ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.Unlike_Image);
            //    e.Handled = true;
            //}

        }

        private void mnu_send_log_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new ErrorReportWnd("Log file");
            wnd.ShowDialog();
        }

        private void btn_changelog_Click(object sender, RoutedEventArgs e)
        {
            NewVersionWnd.ShowChangeLog();
        }

        #region Implementation of INotifyPropertyChanged

        public virtual event PropertyChangedEventHandler PropertyChanged;

        public virtual void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }

        #endregion

    }
}
