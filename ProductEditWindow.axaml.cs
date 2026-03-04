using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace obuvv;

public partial class ProductEditWindow : Window
{
    private const string ConnectionString = "Host=localhost;Database=Obuv1;Username=postgres;Password=12345;";
    private ProductEditMode _mode;
    private ProductViewModel? _product;
    private Dictionary<string, int> _categories;
    private Dictionary<string, int> _manufacturers;
    private Dictionary<string, int> _suppliers;
    private Dictionary<string, int> _units;
    private Dictionary<string, int> _productNames;
    private string? _selectedImagePath;
    
    public bool? DialogResult { get; private set; }
    
    public ProductEditWindow(
        ProductEditMode mode,
        ProductViewModel? product,
        Dictionary<string, int> categories,
        Dictionary<string, int> manufacturers,
        Dictionary<string, int> suppliers,
        Dictionary<string, int> units,
        Dictionary<string, int> productNames)
    {
        InitializeComponent();
        
        _mode = mode;
        _product = product;
        _categories = categories;
        _manufacturers = manufacturers;
        _suppliers = suppliers;
        _units = units;
        _productNames = productNames;
        
        DataContext = this;
        
        // Подписка на события
        SaveButton.Click += SaveButton_Click;
        CancelButton.Click += CancelButton_Click;
        BrowsePhotoButton.Click += BrowsePhotoButton_Click;
        
        InitializeControls();
    }
    
    private void InitializeControls()
    {
        Title = _mode == ProductEditMode.Add ? "Добавление товара" : "Редактирование товара";
        
        // Заполняем ComboBox'ы
        CategoryComboBox.ItemsSource = _categories.Keys.OrderBy(x => x).ToList();
        ManufacturerComboBox.ItemsSource = _manufacturers.Keys.OrderBy(x => x).ToList();
        SupplierComboBox.ItemsSource = _suppliers.Keys.OrderBy(x => x).ToList();
        UnitComboBox.ItemsSource = _units.Keys.OrderBy(x => x).ToList();
        ProductNameComboBox.ItemsSource = _productNames.Keys.OrderBy(x => x).ToList();
        
        // Если режим редактирования
        if (_mode == ProductEditMode.Edit && _product != null)
        {
            ArticleTextBox.Text = _product.Article;
            ArticleTextBox.IsReadOnly = true;
            ArticleTextBox.Foreground = Brushes.Gray;
            
            ProductNameComboBox.SelectedItem = _product.ProductName;
            CategoryComboBox.SelectedItem = _product.CategoryName;
            ManufacturerComboBox.SelectedItem = _product.ManufacturerName;
            SupplierComboBox.SelectedItem = _product.SupplierName;
            PriceTextBox.Text = _product.PriceValue.ToString("F2");
            UnitComboBox.SelectedItem = _product.UnitName;
            QuantityTextBox.Text = _product.StockValue.ToString();
            DiscountTextBox.Text = _product.DiscountValue.ToString("F2");
            DescriptionTextBox.Text = _product.Description;
            PhotoPathTextBox.Text = Path.GetFileName(_product.PhotoPath);
            _selectedImagePath = _product.PhotoPath;
            
            LoadImage(_product.PhotoPath);
        }
        else
        {
            QuantityTextBox.Text = "0";
            DiscountTextBox.Text = "0";
            LoadDefaultImage();
        }
    }
    
    private void LoadImage(string? path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                PreviewImage.Source = new Bitmap(path);
            else
                LoadDefaultImage();
        }
        catch
        {
            LoadDefaultImage();
        }
    }
    
    private void LoadDefaultImage()
    {
        try
        {
            var defaultPath = Path.Combine(Directory.GetCurrentDirectory(), "image", "picture.jpg");
            if (File.Exists(defaultPath))
                PreviewImage.Source = new Bitmap(defaultPath);
            else
                PreviewImage.Source = null;
        }
        catch
        {
            PreviewImage.Source = null;
        }
    }
    
    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!ValidateData()) return;
        
        try
        {
            using var conn = new NpgsqlConnection(ConnectionString);
            conn.Open();
            
            if (_mode == ProductEditMode.Add)
                AddProduct(conn);
            else
                UpdateProduct(conn);
            
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError("Ошибка: " + ex.Message);
        }
    }
    
    private void AddProduct(NpgsqlConnection conn)
    {
        string sql = @"
            INSERT INTO products (
                article, product_name_id, unit_id, price, supplier_id, 
                manufacturer_id, category_id, current_discount, stock_quantity, 
                description, photo
            ) VALUES (
                @article, @product_name_id, @unit_id, @price, @supplier_id,
                @manufacturer_id, @category_id, @current_discount, @stock_quantity,
                @description, @photo
            )";
        
        using var cmd = new NpgsqlCommand(sql, conn);
        
        string photo = !string.IsNullOrEmpty(PhotoPathTextBox.Text) && !string.IsNullOrEmpty(_selectedImagePath) ? 
            CopyImageToAppFolder(_selectedImagePath) : "";
        
        string? productName = ProductNameComboBox.SelectedItem?.ToString();
        string? unitName = UnitComboBox.SelectedItem?.ToString();
        string? supplierName = SupplierComboBox.SelectedItem?.ToString();
        string? manufacturerName = ManufacturerComboBox.SelectedItem?.ToString();
        string? categoryName = CategoryComboBox.SelectedItem?.ToString();
        
        if (string.IsNullOrEmpty(productName) || !_productNames.ContainsKey(productName) ||
            string.IsNullOrEmpty(unitName) || !_units.ContainsKey(unitName) ||
            string.IsNullOrEmpty(supplierName) || !_suppliers.ContainsKey(supplierName) ||
            string.IsNullOrEmpty(manufacturerName) || !_manufacturers.ContainsKey(manufacturerName) ||
            string.IsNullOrEmpty(categoryName) || !_categories.ContainsKey(categoryName))
        {
            throw new Exception("Некорректные данные в выпадающих списках");
        }
        
        cmd.Parameters.AddWithValue("article", ArticleTextBox.Text?.Trim() ?? "");
        cmd.Parameters.AddWithValue("product_name_id", _productNames[productName]);
        cmd.Parameters.AddWithValue("unit_id", _units[unitName]);
        cmd.Parameters.AddWithValue("price", decimal.Parse(PriceTextBox.Text ?? "0"));
        cmd.Parameters.AddWithValue("supplier_id", _suppliers[supplierName]);
        cmd.Parameters.AddWithValue("manufacturer_id", _manufacturers[manufacturerName]);
        cmd.Parameters.AddWithValue("category_id", _categories[categoryName]);
        cmd.Parameters.AddWithValue("current_discount", decimal.Parse(DiscountTextBox.Text ?? "0"));
        cmd.Parameters.AddWithValue("stock_quantity", int.Parse(QuantityTextBox.Text ?? "0"));
        cmd.Parameters.AddWithValue("description", DescriptionTextBox.Text ?? "");
        cmd.Parameters.AddWithValue("photo", photo);
        
        cmd.ExecuteNonQuery();
    }
    
    private void UpdateProduct(NpgsqlConnection conn)
    {
        string sql = @"
            UPDATE products SET
                product_name_id = @product_name_id,
                unit_id = @unit_id,
                price = @price,
                supplier_id = @supplier_id,
                manufacturer_id = @manufacturer_id,
                category_id = @category_id,
                current_discount = @current_discount,
                stock_quantity = @stock_quantity,
                description = @description,
                photo = @photo
            WHERE article = @article";
        
        using var cmd = new NpgsqlCommand(sql, conn);
        
        string photo = !string.IsNullOrEmpty(PhotoPathTextBox.Text) && !string.IsNullOrEmpty(_selectedImagePath) ? 
            CopyImageToAppFolder(_selectedImagePath) : "";
        
        string? productName = ProductNameComboBox.SelectedItem?.ToString();
        string? unitName = UnitComboBox.SelectedItem?.ToString();
        string? supplierName = SupplierComboBox.SelectedItem?.ToString();
        string? manufacturerName = ManufacturerComboBox.SelectedItem?.ToString();
        string? categoryName = CategoryComboBox.SelectedItem?.ToString();
        
        if (string.IsNullOrEmpty(productName) || !_productNames.ContainsKey(productName) ||
            string.IsNullOrEmpty(unitName) || !_units.ContainsKey(unitName) ||
            string.IsNullOrEmpty(supplierName) || !_suppliers.ContainsKey(supplierName) ||
            string.IsNullOrEmpty(manufacturerName) || !_manufacturers.ContainsKey(manufacturerName) ||
            string.IsNullOrEmpty(categoryName) || !_categories.ContainsKey(categoryName))
        {
            throw new Exception("Некорректные данные в выпадающих списках");
        }
        
        cmd.Parameters.AddWithValue("article", ArticleTextBox.Text?.Trim() ?? "");
        cmd.Parameters.AddWithValue("product_name_id", _productNames[productName]);
        cmd.Parameters.AddWithValue("unit_id", _units[unitName]);
        cmd.Parameters.AddWithValue("price", decimal.Parse(PriceTextBox.Text ?? "0"));
        cmd.Parameters.AddWithValue("supplier_id", _suppliers[supplierName]);
        cmd.Parameters.AddWithValue("manufacturer_id", _manufacturers[manufacturerName]);
        cmd.Parameters.AddWithValue("category_id", _categories[categoryName]);
        cmd.Parameters.AddWithValue("current_discount", decimal.Parse(DiscountTextBox.Text ?? "0"));
        cmd.Parameters.AddWithValue("stock_quantity", int.Parse(QuantityTextBox.Text ?? "0"));
        cmd.Parameters.AddWithValue("description", DescriptionTextBox.Text ?? "");
        cmd.Parameters.AddWithValue("photo", photo);
        
        cmd.ExecuteNonQuery();
    }
    
    private bool ValidateData()
    {
        if (string.IsNullOrWhiteSpace(ArticleTextBox.Text))
            return ShowError("Артикул не может быть пустым");
        
        if (_mode == ProductEditMode.Add && ArticleExists(ArticleTextBox.Text))
            return ShowError($"Товар с артикулом '{ArticleTextBox.Text}' уже существует");
        
        if (ProductNameComboBox.SelectedItem == null)
            return ShowError("Выберите наименование товара");
        
        if (CategoryComboBox.SelectedItem == null)
            return ShowError("Выберите категорию");
        
        if (ManufacturerComboBox.SelectedItem == null)
            return ShowError("Выберите производителя");
        
        if (SupplierComboBox.SelectedItem == null)
            return ShowError("Выберите поставщика");
        
        if (!decimal.TryParse(PriceTextBox.Text, out decimal price) || price <= 0)
            return ShowError("Введите корректную цену (больше 0)");
        
        if (UnitComboBox.SelectedItem == null)
            return ShowError("Выберите единицу измерения");
        
        if (!int.TryParse(QuantityTextBox.Text, out int qty) || qty < 0)
            return ShowError("Введите корректное количество (≥ 0)");
        
        if (!decimal.TryParse(DiscountTextBox.Text, out decimal disc) || disc < 0 || disc > 100)
            return ShowError("Введите скидку от 0 до 100");
        
        return true;
    }
    
    private bool ArticleExists(string article)
    {
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        using var cmd = new NpgsqlCommand("SELECT COUNT(1) FROM products WHERE article = @article", conn);
        cmd.Parameters.AddWithValue("article", article);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }
    
    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
    
    private async void BrowsePhotoButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите изображение",
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
        });
        
        if (files?.Count > 0)
        {
            var path = files[0].Path.LocalPath;
            _selectedImagePath = path;
            PhotoPathTextBox.Text = Path.GetFileName(path);
            LoadImage(path);
        }
    }
    
    private string CopyImageToAppFolder(string? sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            return "";
        
        try
        {
            var imageFolder = Path.Combine(Directory.GetCurrentDirectory(), "image");
            Directory.CreateDirectory(imageFolder);
            
            var fileName = Path.GetFileName(sourcePath);
            var destPath = Path.Combine(imageFolder, fileName);
            File.Copy(sourcePath, destPath, true);
            return fileName;
        }
        catch
        {
            return Path.GetFileName(sourcePath) ?? "";
        }
    }
    
    private bool ShowError(string message)
    {
        var dialog = new Window
        {
            Title = "Ошибка",
            Width = 400, Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 20 };
        panel.Children.Add(new TextBlock { Text = "⚠️", FontSize = 30, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Red });
        
        var btn = new Button { Content = "OK", Width = 100, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        btn.Click += (s, args) => dialog.Close();
        panel.Children.Add(btn);
        
        dialog.Content = panel;
        dialog.ShowDialog(this);
        return false;
    }
}