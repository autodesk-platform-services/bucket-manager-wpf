using bucket.manager.wpf.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Autodesk.Forge.Model;
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
