using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace bucket.manager.wpf.ViewModels
{
    // View Model for creating a bucket.
    internal class CreateBucketVM : INotifyPropertyChanged
    {
        private bool _addingGuid = true;
        private string _bucketKey = string.Empty;

        public string BucketKey
        {
            get => _bucketKey;
            set => SetField(ref _bucketKey, value);
        }

        public bool AddingGuid
        {
            get => _addingGuid;
            set => SetField(ref _addingGuid, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// On a property change, invoke the event.
        /// </summary>
        /// <param name="propertyName"></param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// When a field is set, check if the value is different and invoke the event.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
