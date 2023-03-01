using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace bucket.manager.wpf.ViewModels
{
    internal class BucketVM : INotifyPropertyChanged
    {

        private string _key = string.Empty;
        private bool _isExpanded = false;
        private ObservableCollection<BucketItemVM> _items = new ObservableCollection<BucketItemVM>();

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

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
