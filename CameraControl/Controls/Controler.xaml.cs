using System;
using System.Windows;
using System.Windows.Controls;
using CameraControl.Classes;
using CameraControl.Core;
using CameraControl.Core.Classes;
using CameraControl.Devices;
using CameraControl.Devices.Classes;
using CameraControl.Translation;

namespace CameraControl.Controls
{
  /// <summary>
  /// Interaction logic for Controler.xaml
  /// </summary>
  public partial class Controler : UserControl
  {

    public Controler()
    {
      InitializeComponent();
      CameraDeviceManager cameraDeviceManager = DataContext as CameraDeviceManager;
      if (ServiceProvider.DeviceManager != null)
        ServiceProvider.DeviceManager.PropertyChanged += DeviceManager_PropertyChanged;
    }

    private void DeviceManager_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
      if (ServiceProvider.DeviceManager == null || ServiceProvider.DeviceManager.SelectedCameraDevice == null)
        return;
      if (e.PropertyName == "SelectedCameraDevice")
      {
        Dispatcher.Invoke(new Action(RefreshItems));
      }
    }

    private void RefreshItems()
    {
      CameraProperty property =
        ServiceProvider.Settings.CameraProperties.Get(ServiceProvider.DeviceManager.SelectedCameraDevice);
      cmb_transfer.Items.Clear();
      if (ServiceProvider.DeviceManager.SelectedCameraDevice.GetCapability(CapabilityEnum.CaptureInRam))
      {
        cmb_transfer.Items.Add(TranslationStrings.LabelTransferItem1);
        cmb_transfer.Items.Add(TranslationStrings.LabelTransferItem2);
        cmb_transfer.Items.Add(TranslationStrings.LabelTransferItem3);
        if (ServiceProvider.DeviceManager.SelectedCameraDevice.CaptureInSdRam)
          cmb_transfer.SelectedItem = TranslationStrings.LabelTransferItem1;
        else if (!ServiceProvider.DeviceManager.SelectedCameraDevice.CaptureInSdRam && property.NoDownload)
          cmb_transfer.SelectedItem = TranslationStrings.LabelTransferItem2;
        else
          cmb_transfer.SelectedItem = TranslationStrings.LabelTransferItem3;
      }
      else
      {
        cmb_transfer.Items.Add(TranslationStrings.LabelTransferItem2);
        cmb_transfer.Items.Add(TranslationStrings.LabelTransferItem3);
        if (property.NoDownload)
          cmb_transfer.SelectedItem = TranslationStrings.LabelTransferItem2;
        else
          cmb_transfer.SelectedItem = TranslationStrings.LabelTransferItem3;
      }

    }

    private void cmb_shutter_GotFocus(object sender, RoutedEventArgs e)
    {
      ComboBox cmb = sender as ComboBox;
      if (cmb != null && cmb.IsFocused)
      {
        //cmb.IsDropDownOpen = !cmb.IsDropDownOpen;
      }
    }

    private void button1_Click(object sender, RoutedEventArgs e)
    {
      ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.CameraPropertyWnd_Show,
                                                    ServiceProvider.DeviceManager.SelectedCameraDevice);
    }

    private void cmb_transfer_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      CameraProperty property =
        ServiceProvider.Settings.CameraProperties.Get(ServiceProvider.DeviceManager.SelectedCameraDevice);
      if ((string) cmb_transfer.SelectedItem == TranslationStrings.LabelTransferItem1)
        ServiceProvider.DeviceManager.SelectedCameraDevice.CaptureInSdRam = true;
      if ((string) cmb_transfer.SelectedItem == TranslationStrings.LabelTransferItem2)
      {
        ServiceProvider.DeviceManager.SelectedCameraDevice.CaptureInSdRam = false;
        property.NoDownload = true;
      }
      if ((string) cmb_transfer.SelectedItem == TranslationStrings.LabelTransferItem3)
      {
        ServiceProvider.DeviceManager.SelectedCameraDevice.CaptureInSdRam = false;
        property.NoDownload = false;
      }
      property.CaptureInSdRam = ServiceProvider.DeviceManager.SelectedCameraDevice.CaptureInSdRam;
    }


  }
}
