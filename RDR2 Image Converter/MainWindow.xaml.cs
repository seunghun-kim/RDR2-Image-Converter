using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Runtime.CompilerServices;

namespace RDR2_Image_Converter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private ObservableCollection<ImageItem> _imageItems;
        public ObservableCollection<ImageItem> ImageItems
        {
            get { return _imageItems; }
            set
            {
                _imageItems = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<ImageItem> _convertQueueItems;
        public ObservableCollection<ImageItem> ConvertQueueItems
        {
            get { return _convertQueueItems; }
            set
            {
                _convertQueueItems = value;
                OnPropertyChanged();
            }
        }

        private Stream imageStream;
        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
            ImageItems = new ObservableCollection<ImageItem>();
            ConvertQueueItems = new ObservableCollection<ImageItem>();
            DataContext = this;

            ImageItems.CollectionChanged += ListViewItems_CollectionChanged;
            ConvertQueueItems.CollectionChanged += ListViewItems_CollectionChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void ListViewItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            btnAddToConvQueue.IsEnabled = ImageItems.Any(item => item.IsSelected);
            btnRemoveFromConvQueue.IsEnabled = ConvertQueueItems.Any(item => item.IsSelectedConvQueue);
            btnStartConv.IsEnabled = ConvertQueueItems.Count > 0;
        }

        ~MainWindow()
        {
            imageStream.Dispose();
        }

        private void btnStartConv_Click(object sender, RoutedEventArgs e)
        {
            string? dstDir = tboxDstDir.Text;

            if (string.IsNullOrEmpty(dstDir))
            {
                MessageBox.Show("Please select a destination directory.");
                return;
            }

            if (!System.IO.Directory.Exists(dstDir))
            {
                MessageBox.Show("Destination directory does not exist.");
                return;
            }

            SaveSettings();

            var copiedImages = new List<string>();

            foreach (var item in ConvertQueueItems)
            {
                tboxConvLog.Text += $"Processing file: {item.ImagePathConvQueue}\n";

                string dstPath = System.IO.Path.Combine(dstDir, System.IO.Path.GetFileName(item.ImagePathConvQueue));
                try
                {
                    System.IO.File.Copy(item.ImagePathConvQueue, dstPath, true);
                    tboxConvLog.Text += $"Successfully copied image to: {dstPath}\n";
                    copiedImages.Add(dstPath);
                }
                catch (IOException ex)
                {
                    tboxConvLog.Text += $"Failed to copy image: {ex.Message}\n";
                }
            }

            tboxConvLog.Text += "Copy process completed.\n";

            if (copiedImages.Count > 0)
            {
                MessageBoxResult result = MessageBox.Show("Do you want to open the destination folder?", "Open Folder", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start("explorer.exe", dstDir);
                }
            }
        }

        private void btnSelectSrcDir_Click(object sender, RoutedEventArgs e)
        {
            string? path = selectPath();
            if (!string.IsNullOrEmpty(path))
            {
                tboxSrcDir.Text = path;
            }
        }

        private void btnSelectDstDir_Click(object sender, RoutedEventArgs e)
        {
            string? path = selectPath();
            if (!string.IsNullOrEmpty(path))
            {
                tboxDstDir.Text = path;
            }
        }

        private void LoadSettings()
        {
            tboxSrcDir.Text = Properties.Settings.Default.SrcDir;
            tboxDstDir.Text = Properties.Settings.Default.DstDir;
        }

        private void SaveSettings()
        {
            Properties.Settings.Default.SrcDir = tboxSrcDir.Text;
            Properties.Settings.Default.DstDir = tboxDstDir.Text;
            Properties.Settings.Default.Save();
        }

        private Bitmap? extractImageFromFile(string path)
        {
            int findMarker(byte[] marker, byte[] fileBytes)
            {
                int markerIndex = -1;
                for (int i = 0; i < fileBytes.Length - 1; i++)
                {
                    if (fileBytes[i] == marker[0] && fileBytes[i + 1] == marker[1])
                    {
                        markerIndex = i;
                        break;
                    }
                }

                return markerIndex;
            }

            // JPEG SOI and EOI markers
            byte[] soi = new byte[] { 0xFF, 0xD8 };
            byte[] eoi = new byte[] { 0xFF, 0xD9 };

            // Read file
            byte[] bytes = System.IO.File.ReadAllBytes(path);

            // Find SOI and EOI marker
            int soiIndex = findMarker(soi, bytes);
            int eoiIndex = findMarker(eoi, bytes);
            if (soiIndex == -1 || eoiIndex == -1)
            {
                return null;
            }

            // Extract image
            byte[] image = new byte[eoiIndex - soiIndex + 2];
            System.Buffer.BlockCopy(bytes, soiIndex, image, 0, image.Length);

            // Convert to bitmap
            imageStream = new MemoryStream(image);
            return new Bitmap(imageStream);
        }

        private string? selectPath()
        {
            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                return dialog.FileName;
            }

            return string.Empty;
        }

        private void btnDetectSrcDir_Click(object sender, RoutedEventArgs e)
        {
            // Default path: Documents\Rockstar Games\Red Dead Redemption 2\Profiles
            string defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Rockstar Games\\Red Dead Redemption 2\\Profiles\\";

            // Find user's profile folder. Its name should be 8-digit hex number.
            string[] dirs = System.IO.Directory.GetDirectories(defaultPath);
            if (dirs.Length == 0)
            {
                MessageBox.Show("Failed to find profile folder.");
                return;
            }

            string profileDir = dirs[0];
            string[] files = System.IO.Directory.GetFiles(profileDir, "PRDR*");
            if (files.Length == 0)
            {
                MessageBox.Show("Failed to find PRDR* file.");
                return;
            }

            string srcDir = System.IO.Path.GetDirectoryName(files[0]);
            tboxSrcDir.Text = srcDir;
        }

        private void btnShowImages_Click(object sender, RoutedEventArgs e)
        {
            // Read images from source directory
            string? srcDir = tboxSrcDir.Text;
            if (string.IsNullOrEmpty(srcDir))
            {
                MessageBox.Show("Please select source directory.");
                return;
            }

            if (!System.IO.Directory.Exists(srcDir))
            {
                MessageBox.Show("Source directory does not exist.");
                return;
            }

            var files = System.IO.Directory.GetFiles(srcDir, "PRDR*");
            ImageItems.Clear();
            ConvertQueueItems.Clear();

            foreach (var file in files)
            {
                Bitmap? image = extractImageFromFile(file);
                if (image != null)
                {
                    string tempFilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetFileName(file) + ".jpg");
                    // if the file already exists, delete it
                    if (!System.IO.File.Exists(tempFilePath))
                    {
                        try
                        {
                            image.Save(tempFilePath, ImageFormat.Jpeg);
                        }
                        catch (ExternalException ex)
                        {
                            MessageBox.Show($"Error saving image: {ex.Message}");
                        }
                    }
                    ImageItems.Add(new ImageItem { IsSelected = false, ImagePath = tempFilePath });
                    image.Dispose();
                }
            }

            tboxConvLog.Text += $"Found {ImageItems.Count} images in source directory.\n";
        }

        private void btnAddToConvQueue_Click(object sender, RoutedEventArgs e)
        {
            // Move all selected images from lvImages to lvConvertQueue
            var selectedItems = ImageItems.Where(item => item.IsSelected).ToList();
            foreach (var item in selectedItems)
            {
                ConvertQueueItems.Add(new ImageItem { IsSelectedConvQueue = false, ImagePathConvQueue = item.ImagePath });
                ImageItems.Remove(item);
            }

            chkSelectAllConvQueue.IsChecked = false;
            chkSelectAll.IsChecked = false;
        }

        private void btnRemoveFromConvQueue_Click(object sender, RoutedEventArgs e)
        {
            // Move all selected images from lvConvertQueue to lvImages
            var selectedItems = ConvertQueueItems.Where(item => item.IsSelectedConvQueue).ToList();
            foreach (var item in selectedItems)
            {
                ImageItems.Add(new ImageItem { IsSelected = false, ImagePath = item.ImagePathConvQueue });
                ConvertQueueItems.Remove(item);
            }

            chkSelectAllConvQueue.IsChecked = false;
            chkSelectAll.IsChecked = false;
        }

        private void tboxSrcDir_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Activate "Show Images" button if the source directory is set
            btnShowImages.IsEnabled = !string.IsNullOrEmpty(tboxSrcDir.Text);

            // Deactivate buttons and clear ListView data
            btnAddToConvQueue.IsEnabled = false;
            btnRemoveFromConvQueue.IsEnabled = false;
            btnStartConv.IsEnabled = false;

            try
            {
                ImageItems.Clear();
                ConvertQueueItems.Clear();
            }
            catch (NullReferenceException)
            {

            }
        }

        private void chkSelectAllConvQueue_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = (sender as CheckBox).IsChecked ?? false;
            foreach (var item in ConvertQueueItems)
            {
                item.IsSelectedConvQueue = isChecked;
            }
            UpdateButtonStates(sender, e);
        }

        private void chkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = (sender as CheckBox).IsChecked ?? false;
            foreach (var item in ImageItems)
            {
                item.IsSelected = isChecked;
            }
            UpdateButtonStates(sender, e);
        }

        private void UpdateButtonStates(object sender, RoutedEventArgs e)
        {
            btnAddToConvQueue.IsEnabled = ImageItems.Any(item => item.IsSelected);
            btnRemoveFromConvQueue.IsEnabled = ConvertQueueItems.Any(item => item.IsSelectedConvQueue);
            btnStartConv.IsEnabled = ConvertQueueItems.Count > 0;
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T)
                {
                    return (T)child;
                }
                else
                {
                    var childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }
            return null;
        }

        private void tboxDstDir_TextChanged(object sender, TextChangedEventArgs e)
        {
            btnStartConv.IsEnabled = !string.IsNullOrEmpty(tboxDstDir.Text);
        }
        private void lvImages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (ImageItem item in e.AddedItems)
            {
                ConvertQueueItems.Add(new ImageItem { IsSelectedConvQueue = false, ImagePathConvQueue = item.ImagePath });
                ImageItems.Remove(item);
            }
        }

        private void lvConvertQueue_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (ImageItem item in e.AddedItems)
            {
                ImageItems.Add(new ImageItem { IsSelected = false, ImagePath = item.ImagePathConvQueue });
                ConvertQueueItems.Remove(item);
            }
        }
    }
    public class ImageItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        private string _imagePath;
        public string ImagePath
        {
            get { return _imagePath; }
            set
            {
                _imagePath = value;
                OnPropertyChanged();
            }
        }

        private bool _isSelectedConvQueue;
        public bool IsSelectedConvQueue
        {
            get { return _isSelectedConvQueue; }
            set
            {
                _isSelectedConvQueue = value;
                OnPropertyChanged();
            }
        }

        private string _imagePathConvQueue;
        public string ImagePathConvQueue
        {
            get { return _imagePathConvQueue; }
            set
            {
                _imagePathConvQueue = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

}