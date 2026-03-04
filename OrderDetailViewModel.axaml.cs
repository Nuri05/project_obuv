using Avalonia.Media;
using System.ComponentModel;

namespace obuvv;

public class OrderDetailViewModel : INotifyPropertyChanged
{
    public string Article { get; set; } = "";
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalPrice { get; set; }
    
    // Форматированные свойства для отображения
    public string UnitPriceFormatted => $"{UnitPrice:N2} ₽";
    public string DiscountFormatted => Discount > 0 ? $"{Discount:F2}%" : "-";
    public string TotalPriceFormatted => $"{TotalPrice:N2} ₽";
    
    // Цвет скидки
    public IBrush DiscountForeground => Discount > 0 ? Brushes.Red : Brushes.Gray;
    
    // INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}