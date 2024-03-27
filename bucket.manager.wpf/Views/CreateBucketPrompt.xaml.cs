using bucket.manager.wpf.Utils;
using System.Windows;
using bucket.manager.wpf.ViewModels;

namespace bucket.manager.wpf.Views
{
    /// <summary>
    /// Interaction logic for CreateBucketPrompt.xaml
    /// </summary>
    public partial class CreateBucketPrompt : Window
    {
        private CreateBucketVM _createBucketVm = new CreateBucketVM();
        internal CreateBucketVM CreateBucketVm
        {
            get => _createBucketVm;
        }
        public CreateBucketPrompt()
        {
            InitializeComponent();
            Icon = ImageFromBytes.GetBitmapImage(StringResources.logo);
            DataContext = _createBucketVm;

        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (_createBucketVm.BucketKey != BucketName.Text)
            {
                MessageBox.Show(StringResources.errorBucketName, StringResources.errorCaption);
                return;
            }
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
