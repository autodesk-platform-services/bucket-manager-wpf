using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace bucket.manager.wpf.ViewModels
{
    //  View model for a bucket.
    internal class BucketVM : INotifyPropertyChanged
    {

        private string _key = string.Empty;
        private bool _isExpanded = false;
        private ObservableCollection<BucketItemVM> _items = new();

        public string Key
        {
            get => _key;
            set => SetField(ref _key, value);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetField(ref _isExpanded, value);
        }

        public ObservableCollection<BucketItemVM> Items
        {
            get => _items;
            set => SetField(ref _items, value);

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
