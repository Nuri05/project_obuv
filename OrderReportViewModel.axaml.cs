using System;
using System.ComponentModel;

namespace obuvv;

public class OrderReportViewModel : INotifyPropertyChanged
{
    private int _orderNumber;
    private DateTime _orderDate;
    private DateTime _deliveryDate;
    private string _pickupPoint = "";
    private string _client = "";
    private string _status = "";
    private decimal _totalAmount;
    
    public int OrderNumber
    {
        get => _orderNumber;
        set
        {
            _orderNumber = value;
            OnPropertyChanged(nameof(OrderNumber));
        }
    }
    
    public DateTime OrderDate
    {
        get => _orderDate;
        set
        {
            _orderDate = value;
            OnPropertyChanged(nameof(OrderDate));
            OnPropertyChanged(nameof(OrderDateFormatted));
        }
    }
    
    public DateTime DeliveryDate
    {
        get => _deliveryDate;
        set
        {
            _deliveryDate = value;
            OnPropertyChanged(nameof(DeliveryDate));
            OnPropertyChanged(nameof(DeliveryDateFormatted));
        }
    }
    
    public string PickupPoint
    {
        get => _pickupPoint;
        set
        {
            _pickupPoint = value;
            OnPropertyChanged(nameof(PickupPoint));
        }
    }
    
    public string Client
    {
        get => _client;
        set
        {
            _client = value;
            OnPropertyChanged(nameof(Client));
        }
    }
    
    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            OnPropertyChanged(nameof(Status));
        }
    }
    
    public decimal TotalAmount
    {
        get => _totalAmount;
        set
        {
            _totalAmount = value;
            OnPropertyChanged(nameof(TotalAmount));
            OnPropertyChanged(nameof(TotalAmountFormatted));
        }
    }
    
    public string OrderDateFormatted => OrderDate.ToString("dd.MM.yyyy");
    public string DeliveryDateFormatted => DeliveryDate.ToString("dd.MM.yyyy");
    public string TotalAmountFormatted => $"{TotalAmount:N2} ₽";
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}