using System.Windows;
using CameraControl.Classes;
using CameraControl.Devices.Classes;

namespace CameraControl.windows
{
  /// <summary>
  /// Interaction logic for EditSession.xaml
  /// </summary>
  public partial class EditSession : Window
  {
    public PhotoSession Session { get; set; }
    public EditSession(PhotoSession session)
    {
      Session = session;
      Session.BeginEdit();
      InitializeComponent();
      DataContext = Session;
    }

    private void btn_browse_Click(object sender, RoutedEventArgs e)
    {
      var dialog = new System.Windows.Forms.FolderBrowserDialog();
      dialog.SelectedPath = Session.Folder;
      if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK)
      {
        Session.Folder = dialog.SelectedPath;
      }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
      
    }

    private void button1_Click(object sender, RoutedEventArgs e)
    {
      Session.EndEdit();
      DialogResult = true;
      Close();
    }

    private void button2_Click(object sender, RoutedEventArgs e)
    {
      Session.CancelEdit();
      Close();
    }
  }
}
