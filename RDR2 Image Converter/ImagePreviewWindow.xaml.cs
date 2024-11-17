using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;

namespace RDR2_Image_Converter
{
    public partial class ImagePreviewWindow : Window
    {
        private List<string> _imagePaths;
        private int _currentIndex;

        public ImagePreviewWindow(List<string> imagePaths)
        {
            InitializeComponent();
            _imagePaths = imagePaths;
            _currentIndex = 0;
            DisplayImage();
        }

        private void DisplayImage()
        {
            if (_imagePaths.Count == 0) return;

            var bitmap = new BitmapImage(new Uri(_imagePaths[_currentIndex]));
            imgPreview.Source = bitmap;
            txtImageIndex.Text = $"{_currentIndex + 1} / {_imagePaths.Count}";
        }

        private void btnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex > 0)
            {
                _currentIndex--;
                DisplayImage();
            }
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex < _imagePaths.Count - 1)
            {
                _currentIndex++;
                DisplayImage();
            }
        }
    }
}
