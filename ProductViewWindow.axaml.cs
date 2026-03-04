using Avalonia.Controls;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Layout;

namespace obuvv;

public partial class ProductViewWindow : Window
{
    private const string ConnectionString = "Host=localhost;Database=Obuv1;Username=postgres;Password=12345;";
    private string _userRole;
    private int _userId;
    private List<ProductViewModel> _allProducts = new();
    
    private Dictionary<string, int> _categoriesDict = new();
    private Dictionary<string, int> _manufacturersDict = new();
    private Dictionary<string, int> _suppliersDict = new();
    private Dictionary<string, int> _unitsDict = new();
    private Dictionary<string, int> _productNamesDict = new();
    
    public ObservableCollection<ProductViewModel> Products { get; set; } = new();
    public ObservableCollection<string> Categories { get; set; } = new();
    public ObservableCollection<string> Manufacturers { get; set; } = new();
    public ObservableCollection<string> Suppliers { get; set; } = new();
    public ObservableCollection<string> Units { get; set; } = new();
    public ObservableCollection<string> ProductNames { get; set; } = new();

    private string _selectedCategory = "Все категории";
    public string SelectedCategory
    {
        get => _selectedCategory;
        set { _selectedCategory = value; ApplyFilters(); }
    }
    
    private string _selectedManufacturer = "Все производители";
    public string SelectedManufacturer
    {
        get => _selectedManufacturer;
        set { _selectedManufacturer = value; ApplyFilters(); }
    }
    
    private string _selectedSupplier = "Все поставщики";
    public string SelectedSupplier
    {
        get => _selectedSupplier;
        set { _selectedSupplier = value; ApplyFilters(); }
    }
    
    private string _sortBy = "По названию (А-Я)";
    public string SortBy
    {
        get => _sortBy;
        set { _sortBy = value; ApplyFilters(); }
    }
    
    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; ApplyFilters(); }
    }
    
    private bool _showOnlyInStock = false;
    public bool ShowOnlyInStock
    {
        get => _showOnlyInStock;
        set { _showOnlyInStock = value; ApplyFilters(); }
    }

    public ProductViewWindow(string userRole, int userId)
    {
        InitializeComponent();
        _userRole = userRole;
        _userId = userId;
        
        DataContext = this;
        Title = $"ООО ОБУВЬ — Каталог товаров ({userRole})";
        
        LoadDictionaries();
        UpdateUIBasedOnRole();
    }

    private void UpdateUIBasedOnRole()
    {
        bool isAdmin = _userRole == "Администратор";
        bool isManager = _userRole == "Менеджер";
        
        if (ControlPanel != null)
            ControlPanel.IsVisible = isAdmin || isManager;
        
        if (AddProductButton != null)
            AddProductButton.IsVisible = isAdmin;
        
        if (OrdersButton != null)
            OrdersButton.IsVisible = isAdmin || isManager;
    }

    private void LoadDictionaries()
    {
        LoadCategories();
        LoadManufacturers();
        LoadSuppliers();
        LoadUnits();
        LoadProductNames();
        LoadProducts();
    }

    private void LoadCategories()
    {
        Categories.Clear();
        Categories.Add("Все категории");
        _categoriesDict.Clear();
        
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        using var cmd = new NpgsqlCommand("SELECT id, category_name FROM category ORDER BY category_name", conn);
        using var reader = cmd.ExecuteReader();
        
        while (reader.Read())
        {
            string name = reader.GetString(1);
            _categoriesDict[name] = reader.GetInt32(0);
            Categories.Add(name);
        }
    }

    private void LoadManufacturers()
    {
        Manufacturers.Clear();
        Manufacturers.Add("Все производители");
        _manufacturersDict.Clear();
        
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        using var cmd = new NpgsqlCommand("SELECT id, manufacturer_name FROM manufacturer ORDER BY manufacturer_name", conn);
        using var reader = cmd.ExecuteReader();
        
        while (reader.Read())
        {
            string name = reader.GetString(1);
            _manufacturersDict[name] = reader.GetInt32(0);
            Manufacturers.Add(name);
        }
    }

    private void LoadSuppliers()
    {
        Suppliers.Clear();
        Suppliers.Add("Все поставщики");
        _suppliersDict.Clear();
        
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        using var cmd = new NpgsqlCommand("SELECT id, supplier_name FROM supplier ORDER BY supplier_name", conn);
        using var reader = cmd.ExecuteReader();
        
        while (reader.Read())
        {
            string name = reader.GetString(1);
            _suppliersDict[name] = reader.GetInt32(0);
            Suppliers.Add(name);
        }
    }

    private void LoadUnits()
    {
        Units.Clear();
        _unitsDict.Clear();
        
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        using var cmd = new NpgsqlCommand("SELECT id, unit_name FROM unit_of_measure ORDER BY unit_name", conn);
        using var reader = cmd.ExecuteReader();
        
        while (reader.Read())
        {
            string name = reader.GetString(1);
            _unitsDict[name] = reader.GetInt32(0);
            Units.Add(name);
        }
    }

    private void LoadProductNames()
    {
        ProductNames.Clear();
        _productNamesDict.Clear();
        
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        using var cmd = new NpgsqlCommand("SELECT id, product_name FROM product_name ORDER BY product_name", conn);
        using var reader = cmd.ExecuteReader();
        
        while (reader.Read())
        {
            string name = reader.GetString(1);
            _productNamesDict[name] = reader.GetInt32(0);
            ProductNames.Add(name);
        }
    }

    private void LoadProducts()
    {
        try
        {
            _allProducts = GetProductsFromDatabase();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            ShowError("Ошибка загрузки товаров: " + ex.Message);
        }
    }

    private List<ProductViewModel> GetProductsFromDatabase()
    {
        var products = new List<ProductViewModel>();
        string sql = @"
            SELECT 
                p.article,
                pn.product_name,
                c.category_name,
                p.description,
                m.manufacturer_name,
                s.supplier_name,
                p.price,
                u.unit_name,
                p.stock_quantity,
                p.current_discount,
                p.photo,
                pn.id,
                c.id,
                m.id,
                s.id,
                u.id
            FROM products p
            JOIN product_name pn ON p.product_name_id = pn.id
            JOIN category c ON p.category_id = c.id
            JOIN manufacturer m ON p.manufacturer_id = m.id
            JOIN supplier s ON p.supplier_id = s.id
            JOIN unit_of_measure u ON p.unit_id = u.id
            ORDER BY pn.product_name";

        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            decimal price = reader.GetDecimal(6);
            int stock = reader.GetInt32(8);
            decimal discount = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9);
            string manufacturerName = reader.GetString(4);
            string supplierName = reader.GetString(5);

            decimal discountedPrice = discount > 0 ? price * (1 - discount / 100) : price;

        var product = new ProductViewModel
        {
            Article = reader.GetString(0),
            ProductName = reader.GetString(1),
            CategoryName = reader.GetString(2),
            Description = reader.IsDBNull(3) ? "Описание отсутствует" : reader.GetString(3),
            Manufacturer = $"Производитель: {manufacturerName}",
            ManufacturerName = manufacturerName,
            Supplier = $"Поставщик: {supplierName}",
            SupplierName = supplierName,
            Price = $"Цена: {price:C}",
            PriceValue = price,
            OriginalPrice = $"Цена: {price:C}",
            DiscountedPrice = discount > 0 ? $"Цена: {discountedPrice:C}" : "",
            Unit = $"Единица: {reader.GetString(7)}",
            UnitName = reader.GetString(7),
            StockQuantity = $"В наличии: {stock} шт.",
            StockValue = stock,
            Discount = discount > 0 ? $"Скидка: {discount}%" : "Нет скидки",
            DiscountValue = discount,
            PhotoPath = GetImagePath(reader.IsDBNull(10) ? null : reader.GetString(10)),
            ProductImage = LoadImage(GetImagePath(reader.IsDBNull(10) ? null : reader.GetString(10))),
            ProductNameId = reader.GetInt32(11),
            CategoryId = reader.GetInt32(12),
            ManufacturerId = reader.GetInt32(13),
            SupplierId = reader.GetInt32(14),
            UnitId = reader.GetInt32(15),
            IsAdmin = _userRole == "Администратор"
        };
            
            products.Add(product);
        }
        
        return products;
    }

    private string GetImagePath(string? dbPhotoPath)
    {
        if (string.IsNullOrEmpty(dbPhotoPath))
            return GetDefaultImagePath();
        
        string path = Path.Combine(Directory.GetCurrentDirectory(), "image", dbPhotoPath);
        return File.Exists(path) ? path : GetDefaultImagePath();
    }

    private Bitmap? LoadImage(string path)
    {
        try
        {
            return File.Exists(path) ? new Bitmap(path) : null;
        }
        catch
        {
            return null;
        }
    }

    private string GetDefaultImagePath()
    {
        string[] paths = {
            Path.Combine(Directory.GetCurrentDirectory(), "image", "picture.jpg"),
            Path.Combine("image", "picture.jpg"),
            "picture.jpg"
        };
        
        foreach (string path in paths)
            if (File.Exists(path))
                return path;
        
        return "";
    }

    private void ApplyFilters()
    {
        try
        {
            var filtered = _allProducts.AsEnumerable();
            
            if (_userRole == "Гость" || _userRole == "Клиент")
                filtered = filtered.Where(p => p.StockValue > 0);
            
            if (!string.IsNullOrEmpty(SearchText))
            {
                string search = SearchText.ToLower();
                filtered = filtered.Where(p => 
                    p.ProductName.ToLower().Contains(search) ||
                    p.Article.ToLower().Contains(search) ||
                    p.Description.ToLower().Contains(search));
            }
            
            if (SelectedCategory != "Все категории")
                filtered = filtered.Where(p => p.CategoryName == SelectedCategory);
            
            if (SelectedManufacturer != "Все производители")
                filtered = filtered.Where(p => p.ManufacturerName == SelectedManufacturer);
            
            if (SelectedSupplier != "Все поставщики")
                filtered = filtered.Where(p => p.SupplierName == SelectedSupplier);
            
            if (ShowOnlyInStock)
                filtered = filtered.Where(p => p.StockValue > 0);
            
            filtered = SortBy switch
            {
                "По названию (А-Я)" => filtered.OrderBy(p => p.ProductName),
                "По названию (Я-А)" => filtered.OrderByDescending(p => p.ProductName),
                "По цене (возрастание)" => filtered.OrderBy(p => p.PriceValue),
                "По цене (убывание)" => filtered.OrderByDescending(p => p.PriceValue),
                "По количеству (возрастание)" => filtered.OrderBy(p => p.StockValue),
                "По количеству (убывание)" => filtered.OrderByDescending(p => p.StockValue),
                "По скидке (возрастание)" => filtered.OrderBy(p => p.DiscountValue),
                "По скидке (убывание)" => filtered.OrderByDescending(p => p.DiscountValue),
                _ => filtered.OrderBy(p => p.ProductName)
            };
            
            Products.Clear();
            foreach (var p in filtered)
                Products.Add(p);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка фильтрации: " + ex.Message);
        }
    }

    private void AddProductButton_Click(object? sender, RoutedEventArgs e)
    {
        var window = new ProductEditWindow(ProductEditMode.Add, null, _categoriesDict, 
            _manufacturersDict, _suppliersDict, _unitsDict, _productNamesDict);
        
        window.Closed += (s, args) => {
            if (window.DialogResult == true)
            {
                LoadDictionaries();
                LoadProducts();
            }
        };
        
        window.ShowDialog(this);
    }

    private void OrdersButton_Click(object? sender, RoutedEventArgs e)
    {
        new OrdersWindow(_userRole).Show();
        Close();
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        new MainWindow().Show();
        Close();
    }

    private void SearchButton_Click(object? sender, RoutedEventArgs e)
    {
    }

    private void ResetFiltersButton_Click(object? sender, RoutedEventArgs e)
    {
        SelectedCategory = "Все категории";
        SelectedManufacturer = "Все производители";
        SelectedSupplier = "Все поставщики";
        SearchText = "";
        ShowOnlyInStock = false;
        SortBy = "По названию (А-Я)";
    }

    private void EditProductButton_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.CommandParameter is ProductViewModel product)
        {
            var window = new ProductEditWindow(ProductEditMode.Edit, product, _categoriesDict,
                _manufacturersDict, _suppliersDict, _unitsDict, _productNamesDict);
            
            window.Closed += (s, args) => {
                if (window.DialogResult == true)
                {
                    LoadDictionaries();
                    LoadProducts();
                }
            };
            
            window.ShowDialog(this);
        }
    }

    private void DeleteProductButton_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not ProductViewModel product)
            return;
        
        var dialog = new Window
        {
            Title = "Подтверждение",
            Width = 400, Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 20 };
        panel.Children.Add(new TextBlock { Text = $"Удалить товар '{product.ProductName}'?", TextWrapping = TextWrapping.Wrap });
        
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10 };
        var btnYes = new Button { Content = "Удалить", Background = Brushes.Red, Foreground = Brushes.White, Width = 100 };
        var btnNo = new Button { Content = "Отмена", Width = 100 };
        
        btnYes.Click += (s, args) => {
            try
            {
                using var conn = new NpgsqlConnection(ConnectionString);
                conn.Open();
                using var cmd = new NpgsqlCommand("DELETE FROM products WHERE article = @article", conn);
                cmd.Parameters.AddWithValue("article", product.Article);
                cmd.ExecuteNonQuery();
                
                dialog.Close();
                LoadDictionaries();
                LoadProducts();
            }
            catch (Exception ex)
            {
                ShowError("Ошибка удаления: " + ex.Message);
            }
        };
        
        btnNo.Click += (s, args) => dialog.Close();
        
        buttons.Children.Add(btnNo);
        buttons.Children.Add(btnYes);
        panel.Children.Add(buttons);
        dialog.Content = panel;
        dialog.ShowDialog(this);
    }

    private void ShowError(string message)
    {
        var dialog = new Window
        {
            Title = "Ошибка",
            Width = 400, Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 20 };
        panel.Children.Add(new TextBlock { Text = message, Foreground = Brushes.Red });
        
        var btnOk = new Button { Content = "OK", Width = 100, HorizontalAlignment = HorizontalAlignment.Center };
        btnOk.Click += (s, args) => dialog.Close();
        panel.Children.Add(btnOk);
        
        dialog.Content = panel;
        dialog.ShowDialog(this);
    }
}