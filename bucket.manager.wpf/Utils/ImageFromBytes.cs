using System.Windows.Media.Imaging;

namespace bucket.manager.wpf.Utils
{
    internal static class ImageFromBytes
    {
        // Returns a BitmapImage from a byte array.
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
