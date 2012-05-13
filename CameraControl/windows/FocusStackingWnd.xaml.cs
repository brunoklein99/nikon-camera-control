﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CameraControl.Classes;
using CameraControl.Interfaces;

namespace CameraControl.windows
{
  /// <summary>
  /// Interaction logic for FocusStackingWnd.xaml
  /// </summary>
  public partial class FocusStackingWnd : Window, IWindow, INotifyPropertyChanged
  {
    private bool _isBusy;
    public bool IsBusy
    {
      get { return _isBusy; }
      set
      {
        _isBusy = value;
        NotifyPropertyChanged("IsBusy");
        NotifyPropertyChanged("IsFree");
      }
    }

    public bool IsFree
    {
      get { return !_isBusy; }
    }

    private int _photoNo;
    public int PhotoNo
    {
      get { return _photoNo; }
      set
      {
        _photoNo = value;
        NotifyPropertyChanged("PhotoNo");
      }
    }

    private int _focusStep;
    public int FocusStep
    {
      get { return _focusStep; }
      set
      {
        _focusStep = value;
        NotifyPropertyChanged("FocusStep");
      }
    }

    private int photo_count = 0;
    private bool preview = false;
    public FocusStackingWnd()
    {
      InitializeComponent();
      ServiceProvider.Settings.Manager.PhotoTakenDone += Manager_PhotoTakenDone;
      IsBusy = false;
    }

    void Manager_PhotoTakenDone(WIA.Item imageFile)
    {
      if (photo_count <= PhotoNo)
      {
        Thread thread = new Thread(TakePhoto);
        thread.Start();
      }
      else
      {
        IsBusy = false;
      }
    }

    private void TakePhoto()
    {
      if (IsBusy)
      {
        photo_count++;
        Thread.Sleep(200);
        ServiceProvider.DeviceManager.SelectedCameraDevice.StartLiveView();
        Thread.Sleep(800);
        ServiceProvider.DeviceManager.SelectedCameraDevice.Focus(FocusStep);
        Thread.Sleep(1000);
        if (!preview)
        {
          ServiceProvider.DeviceManager.SelectedCameraDevice.TakePictureNoAf();
        }
        else
        {
          if (photo_count <= PhotoNo)
          {
            Thread.Sleep(1000);
            TakePhoto();
          }
          else
          {
            IsBusy = false;
          }
        }
      }
      else
      {
        ServiceProvider.DeviceManager.SelectedCameraDevice.StartLiveView();
      }
    }

    private void btn_preview_Click(object sender, RoutedEventArgs e)
    {
      photo_count = 0;
      IsBusy = true;
      preview = true;
      Thread thread = new Thread(TakePhoto);
      thread.Start(); 
    }

    #region Implementation of IWindow

    public void ExecuteCommand(string cmd)
    {
      switch (cmd)
      {
        case WindowsCmdConsts.FocusStackingWnd_Show:
          Dispatcher.BeginInvoke(new Action(delegate
                                              {
                                                Show();
                                                Activate();
                                                Topmost = true;
                                                Topmost = false;
                                                Focus();
                                              }));
          break;
        case WindowsCmdConsts.FocusStackingWnd_Hide:
          Dispatcher.Invoke(new Action(Hide));
          break;
        case WindowsCmdConsts.All_Close:
          Dispatcher.Invoke(new Action(delegate
                                         {
                                           Hide();
                                           Close();
                                         }));
          break;
      }
    }

    #endregion

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

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
      if (IsVisible)
      {
        e.Cancel = true;
        ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.FocusStackingWnd_Hide);
      }
    }

    private void btn_stop_Click(object sender, RoutedEventArgs e)
    {
      IsBusy = false;
    }

    private void btn_takephoto_Click(object sender, RoutedEventArgs e)
    {
      //ServiceProvider.DeviceManager.SelectedCameraDevice.StopLiveView();
      photo_count = 0;
      IsBusy = true;
      preview = false;
      Thread thread = new Thread(TakePhoto);
      thread.Start(); 
    }
  }
}
