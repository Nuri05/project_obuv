using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace obuvv;

public partial class OrderEditWindow : Window
{
    private const string ConnectionString = "Host=localhost;Database=Obuv1;Username=postgres;Password=12345;";
    private string _userRole;
    private OrderViewModel? _orderToEdit;
    private List<ProductViewModel> _availableProducts = new();
    
    public ObservableCollection<OrderItemViewModel> OrderItems { get; set; } = new();
    public ObservableCollection<string> Statuses { get; set; } = new();
    public ObservableCollection<string> PickupPoints { get; set; } = new();
    public ObservableCollection<string> Users { get; set; } = new();

    public OrderEditWindow(string userRole, OrderViewModel? orderToEdit = null)
    {
        InitializeComponent();
        
        _userRole = userRole;
        _orderToEdit = orderToEdit;
        
        DataContext = this;
        InitializeUI();
        LoadInitialData();
    }

    private void InitializeUI()
    {
        string title = _orderToEdit == null ? "НОВЫЙ ЗАКАЗ" : "РЕДАКТИРОВАНИЕ ЗАКАЗА";
        
        var titleTextBlock = this.FindControl<TextBlock>("TitleTextBlock");
        if (titleTextBlock != null) titleTextBlock.Text = title;
        
        this.Title = $"ООО ОБУВЬ - {title.ToLower()}";
        
        if (_orderToEdit != null)
        {
            var clientComboBox = this.FindControl<ComboBox>("ClientComboBox");
            if (clientComboBox != null) clientComboBox.IsVisible = false;
            
            var clientLabel = this.FindControl<TextBlock>("ClientLabel");
            if (clientLabel != null) clientLabel.IsVisible = false;
            
            var selectedClientLabel = this.FindControl<TextBlock>("SelectedClientLabel");
            if (selectedClientLabel != null && _orderToEdit != null)
            {
                selectedClientLabel.Text = _orderToEdit.ClientName;
                selectedClientLabel.IsVisible = true;
            }
        }
    }

    private async void LoadInitialData()
    {
        try
        {
            await Task.WhenAll(
                LoadStatuses(),
                LoadPickupPoints(),
                LoadUsers(),
                LoadAvailableProducts()
            );
            
            if (_orderToEdit != null) LoadOrderData();
            else SetDefaultValues();
        }
        catch (Exception ex)
        {
            await ShowErrorDialog($"Ошибка загрузки данных: {ex.Message}");
            Close();
        }
    }

    private async Task LoadStatuses()
    {
        Statuses.Clear();
        var statusList = new List<string> { "Новый", "В обработке", "Готов к выдаче", "Выдан", "Отменен" };
        
        foreach (var status in statusList) Statuses.Add(status);
        
        var statusComboBox = this.FindControl<ComboBox>("StatusComboBox");
        if (statusComboBox != null)
        {
            statusComboBox.ItemsSource = Statuses;
            statusComboBox.SelectedIndex = 0;
        }
    }

    private async Task LoadPickupPoints()
    {
        PickupPoints.Clear();
        
        string sql = @"SELECT id, city || ', ' || street || ', д. ' || house_number AS pickup_point 
                      FROM pickup_points ORDER BY city, street, house_number";

        try
        {
            using var conn = new NpgsqlConnection(ConnectionString);
            await conn.OpenAsync();
            
            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            
            while (await reader.ReadAsync()) PickupPoints.Add(reader.GetString(1));
            
            var pickupComboBox = this.FindControl<ComboBox>("PickupPointComboBox");
            if (pickupComboBox != null)
            {
                pickupComboBox.ItemsSource = PickupPoints;
                if (PickupPoints.Count > 0) pickupComboBox.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка загрузки точек выдачи: {ex.Message}");
        }
    }

    private async Task LoadUsers()
    {
        Users.Clear();
        
        string sql = @"SELECT id, first_name || ' ' || last_name AS client_name 
                      FROM users u JOIN user_role r ON u.role_id = r.id 
                      WHERE r.role = 'Клиент' ORDER BY first_name, last_name";

        try
        {
            using var conn = new NpgsqlConnection(ConnectionString);
            await conn.OpenAsync();
            
            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            
            while (await reader.ReadAsync()) Users.Add(reader.GetString(1));
            
            var clientComboBox = this.FindControl<ComboBox>("ClientComboBox");
            if (clientComboBox != null)
            {
                clientComboBox.ItemsSource = Users;
                if (Users.Count > 0) clientComboBox.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка загрузки пользователей: {ex.Message}");
        }
    }

    private async Task LoadAvailableProducts()
    {
        _availableProducts.Clear();
        
        string sql = @"SELECT p.article, pn.product_name, p.price, p.current_discount, p.stock_quantity 
                      FROM products p JOIN product_name pn ON p.product_name_id = pn.id 
                      WHERE p.stock_quantity > 0 ORDER BY pn.product_name";

        try
        {
            using var conn = new NpgsqlConnection(ConnectionString);
            await conn.OpenAsync();
            
            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                _availableProducts.Add(new ProductViewModel
                {
                    Article = reader.GetString(0),
                    ProductName = reader.GetString(1),
                    PriceValue = reader.GetDecimal(2),
                    DiscountValue = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                    StockValue = reader.GetInt32(4)
                });
            }
            
            var productComboBox = this.FindControl<ComboBox>("ProductComboBox");
            if (productComboBox != null)
            {
                productComboBox.ItemsSource = _availableProducts;
                if (_availableProducts.Count > 0) productComboBox.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка загрузки товаров: {ex.Message}");
        }
    }

    private async void LoadOrderData()
    {
        if (_orderToEdit == null) return;
        
        try
        {
            await LoadOrderItems(_orderToEdit.OrderNumber);
            
            var orderNumberTextBox = this.FindControl<TextBox>("OrderNumberTextBox");
            var receiptCodeTextBox = this.FindControl<TextBox>("ReceiptCodeTextBox");
            var orderDatePicker = this.FindControl<DatePicker>("OrderDatePicker");
            var deliveryDatePicker = this.FindControl<DatePicker>("DeliveryDatePicker");
            var statusComboBox = this.FindControl<ComboBox>("StatusComboBox");
            var pickupComboBox = this.FindControl<ComboBox>("PickupPointComboBox");
            
            if (orderNumberTextBox != null) orderNumberTextBox.Text = _orderToEdit.OrderNumber.ToString();
            if (receiptCodeTextBox != null) receiptCodeTextBox.Text = _orderToEdit.ReceiptCode;
            if (orderDatePicker != null) orderDatePicker.SelectedDate = _orderToEdit.OrderDate;
            if (deliveryDatePicker != null) deliveryDatePicker.SelectedDate = _orderToEdit.DeliveryDate;
            if (statusComboBox != null) statusComboBox.SelectedItem = _orderToEdit.Status;
            if (pickupComboBox != null) pickupComboBox.SelectedItem = _orderToEdit.PickupPoint;
            
            UpdateTotalAmount();
        }
        catch (Exception ex)
        {
            await ShowErrorDialog($"Ошибка загрузки данных заказа: {ex.Message}");
        }
    }

    private async Task LoadOrderItems(int orderNumber)
    {
        OrderItems.Clear();
        
        string sql = @"SELECT od.article, pn.product_name, od.quantity, od.unit_price, od.discount 
                      FROM order_details od JOIN products p ON od.article = p.article 
                      JOIN product_name pn ON p.product_name_id = pn.id 
                      WHERE od.order_number = @orderNumber";

        try
        {
            using var conn = new NpgsqlConnection(ConnectionString);
            await conn.OpenAsync();
            
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("orderNumber", orderNumber);
            
            using var reader = await cmd.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                OrderItems.Add(new OrderItemViewModel
                {
                    Article = reader.GetString(0),
                    ProductName = reader.GetString(1),
                    Quantity = reader.GetInt32(2),
                    UnitPrice = reader.GetDecimal(3),
                    Discount = reader.GetDecimal(4)
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка загрузки товаров заказа: {ex.Message}");
            throw;
        }
    }

    private void SetDefaultValues()
    {
        var orderDatePicker = this.FindControl<DatePicker>("OrderDatePicker");
        var deliveryDatePicker = this.FindControl<DatePicker>("DeliveryDatePicker");
        
        if (orderDatePicker != null) orderDatePicker.SelectedDate = DateTime.Today;
        if (deliveryDatePicker != null) deliveryDatePicker.SelectedDate = DateTime.Today.AddDays(3);
        
        var receiptCodeTextBox = this.FindControl<TextBox>("ReceiptCodeTextBox");
        if (receiptCodeTextBox != null) receiptCodeTextBox.Text = GenerateReceiptCode();
    }

    private string GenerateReceiptCode()
    {
        return $"RC{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}";
    }

    private void AddProductButton_Click(object? sender, RoutedEventArgs e)
    {
        var productComboBox = this.FindControl<ComboBox>("ProductComboBox");
        var quantityTextBox = this.FindControl<TextBox>("QuantityTextBox");
        
        if (productComboBox?.SelectedItem is not ProductViewModel selectedProduct)
        {
            ShowError("Выберите товар");
            return;
        }
        
        if (string.IsNullOrEmpty(quantityTextBox?.Text) || !int.TryParse(quantityTextBox.Text, out int quantity) || quantity <= 0)
        {
            ShowError("Введите корректное количество");
            return;
        }
        
        if (quantity > selectedProduct.StockValue)
        {
            ShowError($"Недостаточно товара на складе. Доступно: {selectedProduct.StockValue}");
            return;
        }
        
        var existingItem = OrderItems.FirstOrDefault(item => item.Article == selectedProduct.Article);
        if (existingItem != null)
        {
            existingItem.Quantity += quantity;
        }
        else
        {
            OrderItems.Add(new OrderItemViewModel
            {
                Article = selectedProduct.Article,
                ProductName = selectedProduct.ProductName,
                Quantity = quantity,
                UnitPrice = selectedProduct.PriceValue,
                Discount = selectedProduct.DiscountValue
            });
        }
        
        if (quantityTextBox != null) quantityTextBox.Text = "1";
        
        UpdateTotalAmount();
    }

    private void RemoveProductButton_Click(object? sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button?.DataContext is OrderItemViewModel item)
        {
            OrderItems.Remove(item);
            UpdateTotalAmount();
        }
    }

    private void UpdateTotalAmount()
    {
        decimal total = OrderItems.Sum(item => item.TotalPrice);
        
        var totalAmountTextBlock = this.FindControl<TextBlock>("TotalAmountTextBlock");
        if (totalAmountTextBlock != null) totalAmountTextBlock.Text = $"{total:N2} ₽";
    }

    private async void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!ValidateOrderData()) return;
        
        try
        {
            if (_orderToEdit == null) await CreateNewOrder();
            else await UpdateExistingOrder();
            
            Close();
        }
        catch (Exception ex)
        {
            await ShowErrorDialog($"Ошибка сохранения заказа: {ex.Message}");
        }
    }

    private bool ValidateOrderData()
    {
        if (OrderItems.Count == 0)
        {
            ShowError("Добавьте хотя бы один товар в заказ");
            return false;
        }
        
        var deliveryDatePicker = this.FindControl<DatePicker>("DeliveryDatePicker");
        var orderDatePicker = this.FindControl<DatePicker>("OrderDatePicker");
        
        if (deliveryDatePicker?.SelectedDate == null)
        {
            ShowError("Выберите дату доставки");
            return false;
        }
        
        if (orderDatePicker?.SelectedDate == null)
        {
            ShowError("Выберите дату заказа");
            return false;
        }
        
        if (deliveryDatePicker.SelectedDate.Value.DateTime < orderDatePicker.SelectedDate.Value.DateTime)
        {
            ShowError("Дата доставки не может быть раньше даты заказа");
            return false;
        }
        
        var pickupComboBox = this.FindControl<ComboBox>("PickupPointComboBox");
        if (pickupComboBox?.SelectedItem == null)
        {
            ShowError("Выберите точку выдачи");
            return false;
        }
        
        var statusComboBox = this.FindControl<ComboBox>("StatusComboBox");
        if (statusComboBox?.SelectedItem == null)
        {
            ShowError("Выберите статус заказа");
            return false;
        }
        
        return true;
    }

    private async Task CreateNewOrder()
    {
        var clientComboBox = this.FindControl<ComboBox>("ClientComboBox");
        var pickupComboBox = this.FindControl<ComboBox>("PickupPointComboBox");
        var statusComboBox = this.FindControl<ComboBox>("StatusComboBox");
        var orderDatePicker = this.FindControl<DatePicker>("OrderDatePicker");
        var deliveryDatePicker = this.FindControl<DatePicker>("DeliveryDatePicker");
        var receiptCodeTextBox = this.FindControl<TextBox>("ReceiptCodeTextBox");
        
        if (clientComboBox?.SelectedItem == null || pickupComboBox?.SelectedItem == null || 
            statusComboBox?.SelectedItem == null || orderDatePicker?.SelectedDate == null ||
            deliveryDatePicker?.SelectedDate == null || receiptCodeTextBox == null)
        {
            throw new Exception("Не все обязательные поля заполнены");
        }
        
        string clientName = clientComboBox.SelectedItem.ToString() ?? "";
        string pickupPoint = pickupComboBox.SelectedItem.ToString() ?? "";
        string status = statusComboBox.SelectedItem.ToString() ?? "";
        DateTime orderDate = orderDatePicker.SelectedDate.Value.DateTime;
        DateTime deliveryDate = deliveryDatePicker.SelectedDate.Value.DateTime;
        string receiptCode = receiptCodeTextBox.Text.Trim();
        decimal totalAmount = OrderItems.Sum(item => item.TotalPrice);
        
        using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        
        using var transaction = await conn.BeginTransactionAsync();
        
        try
        {
            int clientId = await GetClientId(clientName);
            int pickupPointId = await GetPickupPointId(pickupPoint);
            
            string sqlOrder = @"INSERT INTO orders (order_date, delivery_date, pickup_point_id, client_id, 
                                receipt_code, order_status, total_amount) VALUES 
                                (@order_date, @delivery_date, @pickup_point_id, @client_id, 
                                @receipt_code, @order_status, @total_amount) RETURNING order_number";
            
            using var cmdOrder = new NpgsqlCommand(sqlOrder, conn);
            cmdOrder.Transaction = (NpgsqlTransaction)transaction;
            
            cmdOrder.Parameters.AddWithValue("order_date", orderDate);
            cmdOrder.Parameters.AddWithValue("delivery_date", deliveryDate);
            cmdOrder.Parameters.AddWithValue("pickup_point_id", pickupPointId);
            cmdOrder.Parameters.AddWithValue("client_id", clientId);
            cmdOrder.Parameters.AddWithValue("receipt_code", receiptCode);
            cmdOrder.Parameters.AddWithValue("order_status", status);
            cmdOrder.Parameters.AddWithValue("total_amount", totalAmount);
            
            int orderNumber = Convert.ToInt32(await cmdOrder.ExecuteScalarAsync());
            
            foreach (var item in OrderItems)
            {
                string sqlDetail = @"INSERT INTO order_details (order_number, article, quantity, 
                                    unit_price, discount, total_price) VALUES 
                                    (@order_number, @article, @quantity, @unit_price, @discount, @total_price)";
                
                using var cmdDetail = new NpgsqlCommand(sqlDetail, conn);
                cmdDetail.Transaction = (NpgsqlTransaction)transaction;
                
                cmdDetail.Parameters.AddWithValue("order_number", orderNumber);
                cmdDetail.Parameters.AddWithValue("article", item.Article);
                cmdDetail.Parameters.AddWithValue("quantity", item.Quantity);
                cmdDetail.Parameters.AddWithValue("unit_price", item.UnitPrice);
                cmdDetail.Parameters.AddWithValue("discount", item.Discount);
                cmdDetail.Parameters.AddWithValue("total_price", item.TotalPrice);
                
                await cmdDetail.ExecuteNonQueryAsync();
                
                string sqlUpdateStock = @"UPDATE products SET stock_quantity = stock_quantity - @quantity 
                                         WHERE article = @article";
                
                using var cmdUpdate = new NpgsqlCommand(sqlUpdateStock, conn);
                cmdUpdate.Transaction = (NpgsqlTransaction)transaction;
                
                cmdUpdate.Parameters.AddWithValue("quantity", item.Quantity);
                cmdUpdate.Parameters.AddWithValue("article", item.Article);
                
                await cmdUpdate.ExecuteNonQueryAsync();
            }
            
            await transaction.CommitAsync();
            
            await ShowSuccessDialog($"Заказ №{orderNumber} успешно создан");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task UpdateExistingOrder()
    {
        if (_orderToEdit == null) return;
        
        var pickupComboBox = this.FindControl<ComboBox>("PickupPointComboBox");
        var statusComboBox = this.FindControl<ComboBox>("StatusComboBox");
        var deliveryDatePicker = this.FindControl<DatePicker>("DeliveryDatePicker");
        var receiptCodeTextBox = this.FindControl<TextBox>("ReceiptCodeTextBox");
        
        if (pickupComboBox?.SelectedItem == null || statusComboBox?.SelectedItem == null || 
            deliveryDatePicker?.SelectedDate == null || receiptCodeTextBox == null)
        {
            throw new Exception("Не все обязательные поля заполнены");
        }
        
        string pickupPoint = pickupComboBox.SelectedItem.ToString() ?? "";
        string status = statusComboBox.SelectedItem.ToString() ?? "";
        DateTime deliveryDate = deliveryDatePicker.SelectedDate.Value.DateTime;
        string receiptCode = receiptCodeTextBox.Text.Trim();
        decimal totalAmount = OrderItems.Sum(item => item.TotalPrice);
        
        using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        
        using var transaction = await conn.BeginTransactionAsync();
        
        try
        {
            int pickupPointId = await GetPickupPointId(pickupPoint);
            
            string sqlUpdateOrder = @"UPDATE orders SET delivery_date = @delivery_date, 
                                     pickup_point_id = @pickup_point_id, receipt_code = @receipt_code, 
                                     order_status = @order_status, total_amount = @total_amount 
                                     WHERE order_number = @order_number";
            
            using var cmdOrder = new NpgsqlCommand(sqlUpdateOrder, conn);
            cmdOrder.Transaction = (NpgsqlTransaction)transaction;
            
            cmdOrder.Parameters.AddWithValue("delivery_date", deliveryDate);
            cmdOrder.Parameters.AddWithValue("pickup_point_id", pickupPointId);
            cmdOrder.Parameters.AddWithValue("receipt_code", receiptCode);
            cmdOrder.Parameters.AddWithValue("order_status", status);
            cmdOrder.Parameters.AddWithValue("total_amount", totalAmount);
            cmdOrder.Parameters.AddWithValue("order_number", _orderToEdit.OrderNumber);
            
            await cmdOrder.ExecuteNonQueryAsync();
            
            string sqlDeleteDetails = "DELETE FROM order_details WHERE order_number = @order_number";
            using var cmdDelete = new NpgsqlCommand(sqlDeleteDetails, conn);
            cmdDelete.Transaction = (NpgsqlTransaction)transaction;
            cmdDelete.Parameters.AddWithValue("order_number", _orderToEdit.OrderNumber);
            await cmdDelete.ExecuteNonQueryAsync();
            
            foreach (var item in OrderItems)
            {
                string sqlDetail = @"INSERT INTO order_details (order_number, article, quantity, 
                                    unit_price, discount, total_price) VALUES 
                                    (@order_number, @article, @quantity, @unit_price, @discount, @total_price)";
                
                using var cmdDetail = new NpgsqlCommand(sqlDetail, conn);
                cmdDetail.Transaction = (NpgsqlTransaction)transaction;
                
                cmdDetail.Parameters.AddWithValue("order_number", _orderToEdit.OrderNumber);
                cmdDetail.Parameters.AddWithValue("article", item.Article);
                cmdDetail.Parameters.AddWithValue("quantity", item.Quantity);
                cmdDetail.Parameters.AddWithValue("unit_price", item.UnitPrice);
                cmdDetail.Parameters.AddWithValue("discount", item.Discount);
                cmdDetail.Parameters.AddWithValue("total_price", item.TotalPrice);
                
                await cmdDetail.ExecuteNonQueryAsync();
            }
            
            await transaction.CommitAsync();
            
            await ShowSuccessDialog($"Заказ №{_orderToEdit.OrderNumber} успешно обновлен");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<int> GetClientId(string fullName)
    {
        using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        string query = @"SELECT id FROM users 
                         WHERE (last_name || ' ' || first_name || ' ' || COALESCE(middle_name, '')) LIKE @name 
                         OR (last_name || ' ' || first_name) LIKE @name LIMIT 1";

        using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("name", $"%{fullName.Trim()}%");

        var result = await cmd.ExecuteScalarAsync();
        return result != null ? Convert.ToInt32(result) : 1;
    }

    private async Task<int> GetPickupPointId(string pickupPoint)
    {
        using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        var parts = pickupPoint.Split(',').Select(p => p.Trim()).ToList();
        
        string query = @"SELECT id FROM pickup_points 
                         WHERE city = @city AND street = @street 
                         AND (house_number = @house OR @house LIKE '%' || house_number || '%') LIMIT 1";

        using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("city", parts.Count > 0 ? parts[0] : "");
        cmd.Parameters.AddWithValue("street", parts.Count > 1 ? parts[1] : "");
        
        string house = parts.Count > 2 ? parts[2].Replace("д.", "").Trim() : "";
        cmd.Parameters.AddWithValue("house", house);

        var result = await cmd.ExecuteScalarAsync();
        
        if (result == null)
        {
            using var cmdFallback = new NpgsqlCommand("SELECT id FROM pickup_points WHERE @p LIKE '%' || street || '%' LIMIT 1", conn);
            cmdFallback.Parameters.AddWithValue("p", pickupPoint);
            result = await cmdFallback.ExecuteScalarAsync();
        }

        if (result == null) 
            throw new Exception($"Пункт выдачи не найден в базе. Проверьте адрес: {pickupPoint}");

        return Convert.ToInt32(result);
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ShowError(string message)
    {
        var errorTextBlock = this.FindControl<TextBlock>("ErrorTextBlock");
        if (errorTextBlock != null)
        {
            errorTextBlock.Text = message;
            errorTextBlock.IsVisible = true;
            
            Task.Delay(5000).ContinueWith(_ => 
            {
                Dispatcher.UIThread.InvokeAsync(() => errorTextBlock.IsVisible = false);
            });
        }
    }

    private async Task ShowErrorDialog(string message)
    {
        var dialog = new Window
        {
            Title = "Ошибка",
            Width = 400, Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brushes.White,
            FontFamily = new FontFamily("Times New Roman")
        };
        
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 20 };
        
        panel.Children.Add(new TextBlock 
        { 
            Text = "❌", FontSize = 30, 
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center 
        });
        
        panel.Children.Add(new TextBlock 
        { 
            Text = message, TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Red, FontSize = 14,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center 
        });
        
        var okButton = new Button 
        { 
            Content = "OK", Background = Brushes.LightGray,
            Foreground = Brushes.Black, Width = 100, Height = 35,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center 
        };
        
        okButton.Click += (s, e) => dialog.Close();
        panel.Children.Add(okButton);
        
        dialog.Content = panel;
        await dialog.ShowDialog(this);
    }

    private async Task ShowSuccessDialog(string message)
    {
        var dialog = new Window
        {
            Title = "Успешно",
            Width = 400, Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brushes.White,
            FontFamily = new FontFamily("Times New Roman")
        };
        
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 20 };
        
        panel.Children.Add(new TextBlock 
        { 
            Text = "✅", FontSize = 30,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center 
        });
        
        panel.Children.Add(new TextBlock 
        { 
            Text = message, TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Green, FontSize = 14,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center 
        });
        
        var okButton = new Button 
        { 
            Content = "OK", Background = Brushes.LightGreen,
            Foreground = Brushes.Black, Width = 100, Height = 35,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center 
        };
        
        okButton.Click += (s, e) => dialog.Close();
        panel.Children.Add(okButton);
        
        dialog.Content = panel;
        await dialog.ShowDialog(this);
    }
}