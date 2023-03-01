using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace bucket.manager.wpf.Utils
{
    internal static class ImageFromBytes
    {

        public static BitmapImage GetBitmapImage(byte[] bytes)
        {
            var image = new BitmapImage();
            using var ms = new System.IO.MemoryStream(bytes);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            return image;

        }
    }
}
