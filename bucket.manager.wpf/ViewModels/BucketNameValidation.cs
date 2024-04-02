using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace bucket.manager.wpf.ViewModels
{
    internal class BucketNameValidation : ValidationRule
    {
        /// <summary>
        /// Validate the bucket name using regex.
        /// </summary>
        /// <param name="value">value to validate</param>
        /// <param name="cultureInfo">culture</param>
        /// <returns></returns>
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var text = value.ToString();
            if (string.IsNullOrEmpty(text))
                return new ValidationResult(true, null);
            // We will check minimum length when pressing ok.
            if (text.Length is > 128 or < 3)
                return new ValidationResult(false, StringResources.errorBucketName);
            var myRegex = new Regex("[-_.a-z0-9]*");
            var result = myRegex.Match(text);
            return text != result.Value ? new ValidationResult(false, StringResources.errorBucketName) : new ValidationResult(true, null);
        }
    }
}
