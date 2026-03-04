using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.ComponentModel;

namespace obuvv;

public class ProductViewModel : INotifyPropertyChanged
{
    private bool _isAdmin;
    private decimal _discountValue;
    private int _stockValue;
    private bool _isSelected;
    private Bitmap? _displayImage;

    // Основные свойства товара
    public string Article { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string ManufacturerName { get; set; } = "";
    public string Supplier { get; set; } = "";
    public string SupplierName { get; set; } = "";
    public string Price { get; set; } = ""; // Сохраняется для совместимости
    public decimal PriceValue { get; set; }
    public string Unit { get; set; } = "";
    public string UnitName { get; set; } = "";
    public string StockQuantity { get; set; } = "";

    // === Свойства для отображения цены со скидкой ===
    public string OriginalPrice { get; set; } = "";        // ← именно это имя использует XAML
    public string DiscountedPrice { get; set; } = "";      // ← именно это имя использует XAML
    public bool HasDiscount => DiscountValue > 0;
    public TextDecorationCollection? OriginalPriceTextDecorations =>
        HasDiscount ? TextDecorations.Strikethrough : null;

    // Склад
    public int StockValue
    {
        get => _stockValue;
        set
        {
            _stockValue = value;
            OnPropertyChanged(nameof(StockValue));
            OnPropertyChanged(nameof(StockQuantity));
            OnPropertyChanged(nameof(StockColor));
            OnPropertyChanged(nameof(IsInStock));
        }
    }

    // Скидка
    public string Discount { get; set; } = "";
    public decimal DiscountValue
    {
        get => _discountValue;
        set
        {
            _discountValue = value;
            OnPropertyChanged(nameof(DiscountValue));
            OnPropertyChanged(nameof(Discount));
            OnPropertyChanged(nameof(DiscountBackground));
            OnPropertyChanged(nameof(DiscountForeground));
            // Обновляем и зависящие от скидки свойства цены
            OnPropertyChanged(nameof(HasDiscount));
            OnPropertyChanged(nameof(OriginalPriceTextDecorations));
        }
    }

    // Изображение
    public string PhotoPath { get; set; } = "";
    public Bitmap? ProductImage { get; set; }
    public Bitmap? DisplayImage
    {
        get => _displayImage ?? ProductImage;
        set
        {
            _displayImage = value;
            OnPropertyChanged(nameof(DisplayImage));
        }
    }

    // Для редактирования
    public int ProductNameId { get; set; }
    public int CategoryId { get; set; }
    public int ManufacturerId { get; set; }
    public int SupplierId { get; set; }
    public int UnitId { get; set; }

    // Админка и выбор
    public bool IsAdmin
    {
        get => _isAdmin;
        set
        {
            _isAdmin = value;
            OnPropertyChanged(nameof(IsAdmin));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
            OnPropertyChanged(nameof(BackgroundColor));
        }
    }

    // Производные свойства
    public override string ToString() => ProductName;

    public string Title => $"{CategoryName} | {ProductName}";

    public IBrush DiscountBackground =>
        DiscountValue > 15
            ? new SolidColorBrush(Color.Parse("#2E8B57"))
            : new SolidColorBrush(Color.Parse("#F5F5F5"));

    public IBrush DiscountForeground =>
        DiscountValue > 15 ? Brushes.White : Brushes.Black;

    public IBrush StockColor =>
        StockValue == 0 ? Brushes.Red :
        StockValue < 10 ? Brushes.Orange : Brushes.Green;

    public IBrush BackgroundColor =>
        IsSelected ? new SolidColorBrush(Color.Parse("#E6F7FF")) : Brushes.Transparent;

    public bool HasImage => DisplayImage != null;
    public bool IsInStock => StockValue > 0;

    public string SearchString =>
        $"{Article} {ProductName} {CategoryName} {Description} {Manufacturer} {Supplier}";

    // INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}