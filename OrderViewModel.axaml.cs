using Avalonia.Media;
using System;
using System.ComponentModel;

namespace obuvv;

public class OrderViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private decimal _maxDiscount;
    private bool _isAdmin; 
    
    public int OrderNumber { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime DeliveryDate { get; set; }
    public string ReceiptCode { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public string ClientName { get; set; } = "";
    public string PickupPoint { get; set; } = "";
    
    public bool IsAdmin
    {
        get => _isAdmin;
        set
        {
            if (_isAdmin != value)
            {
                _isAdmin = value;
                OnPropertyChanged(nameof(IsAdmin));
            }
        }
    }
    
    public decimal MaxDiscount
    {
        get => _maxDiscount;
        set
        {
            _maxDiscount = value;
            OnPropertyChanged(nameof(MaxDiscount));
            OnPropertyChanged(nameof(MaxDiscountFormatted));
            OnPropertyChanged(nameof(HasHighDiscount));
            OnPropertyChanged(nameof(RowBackground));
            OnPropertyChanged(nameof(DiscountForeground));
        }
    }
    
    public string OrderDateFormatted => OrderDate.ToString("dd.MM.yyyy");
    public string DeliveryDateFormatted => DeliveryDate.ToString("dd.MM.yyyy");
    public string TotalAmountFormatted => $"{TotalAmount:N2} ₽";
    public string StatusDisplay => Status;
    public string MaxDiscountFormatted => $"{MaxDiscount:N1}%";
    
    public bool HasHighDiscount => MaxDiscount > 15;
    
    public IBrush RowBackground => HasHighDiscount ? 
        new SolidColorBrush(Color.Parse("#E6FFED")) :
        Brushes.Transparent;
    
    public IBrush DiscountForeground => HasHighDiscount ? 
        new SolidColorBrush(Color.Parse("#2E8B57")) : 
        Brushes.Black;
    
    public IBrush StatusColor => Status switch
    {
        "Новый" => Brushes.LightBlue,
        "В обработке" => Brushes.LightYellow,
        "Готов к выдаче" => Brushes.LightGreen,
        "Выдан" => Brushes.LightGray,
        "Отменен" => Brushes.LightCoral,
        _ => Brushes.LightGray
    };
    
    public IBrush StatusForeground => Status switch
    {
        "Новый" => Brushes.DarkBlue,
        "В обработке" => Brushes.DarkOrange,
        "Готов к выдаче" => Brushes.DarkGreen,
        "Выдан" => Brushes.DarkGray,
        "Отменен" => Brushes.DarkRed,
        _ => Brushes.Black
    };
    
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
                OnPropertyChanged(nameof(BackgroundColor));
            }
        }
    }
    
    public IBrush BackgroundColor => IsSelected ? Brushes.LightCyan : Brushes.Transparent;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}