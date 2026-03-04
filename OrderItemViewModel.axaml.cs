using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace obuvv
{
    public class OrderItemViewModel : INotifyPropertyChanged
    {
        private string _article = "";
        private string _productName = "";
        private decimal _unitPrice;
        private int _quantity;
        private decimal _discount;

        public string Article
        {
            get => _article;
            set
            {
                if (_article != value)
                {
                    _article = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ProductName
        {
            get => _productName;
            set
            {
                if (_productName != value)
                {
                    _productName = value;
                    OnPropertyChanged();
                }
            }
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (_unitPrice != value)
                {
                    _unitPrice = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(UnitPriceFormatted));
                    OnPropertyChanged(nameof(TotalPrice));
                    OnPropertyChanged(nameof(TotalPriceFormatted));
                }
            }
        }

        public int Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value)
                {
                    _quantity = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalPrice));
                    OnPropertyChanged(nameof(TotalPriceFormatted));
                }
            }
        }

        public decimal Discount
        {
            get => _discount;
            set
            {
                if (_discount != value)
                {
                    _discount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DiscountFormatted));
                    OnPropertyChanged(nameof(TotalPrice));
                    OnPropertyChanged(nameof(TotalPriceFormatted));
                }
            }
        }

        // Вычисляемые свойства
        public decimal TotalPrice => (Quantity * UnitPrice) * (1m - Discount / 100m);

        public string UnitPriceFormatted => $"{UnitPrice:N2} ₽";

        public string DiscountFormatted => Discount > 0 ? $"{Discount:F2}%" : "-";

        public string TotalPriceFormatted => $"{TotalPrice:N2} ₽";

        public IBrush DiscountForeground => Discount > 0 ? Brushes.Red : Brushes.Gray;

        // Событие для INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}