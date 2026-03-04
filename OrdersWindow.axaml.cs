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

public partial class OrdersWindow : Window
{
    private const string ConnectionString = "Host=localhost;Database=Obuv1;Username=postgres;Password=12345;";
    private string _userRole;
    private List<OrderViewModel> _allOrders = new();
    private OrderViewModel? _selectedOrder = null;
    
    public ObservableCollection<OrderViewModel> Orders { get; set; }
    public ObservableCollection<OrderDetailViewModel> OrderDetails { get; set; }
    public ObservableCollection<string> Statuses { get; set; }
    public ObservableCollection<string> PickupPoints { get; set; }
    public bool IsAdminVisible => _userRole == "Администратор";

    public OrdersWindow(string userRole)
    {
        InitializeComponent();
        _userRole = userRole;
        
        Orders = new ObservableCollection<OrderViewModel>();
        OrderDetails = new ObservableCollection<OrderDetailViewModel>();
        Statuses = new ObservableCollection<string>();
        PickupPoints = new ObservableCollection<string>();
        
        this.DataContext = this;
        ConfigureAccessByRole();
        InitializeUI();
    }

    private void ConfigureAccessByRole()
    {
        this.Title = $"ООО ОБУВЬ - Заказы ({_userRole})";
        
        if (_userRole == "Клиент")
        {
            ShowError("У вас нет доступа к просмотру заказов");
            Close();
        }
    }

    private void InitializeUI()
    {
        if (_userRole == "Администратор")
        {
            InitializeFilters();
        }
        
        InitializeEventHandlers();
        LoadOrders();
    }

    private void InitializeEventHandlers()
    {
        BackButton.Click += BackButton_Click;
        AddOrderButton.Click += AddOrderButton_Click;
        
        if (_userRole == "Администратор")
        {
            SearchTextBox.TextChanged += (s,e) => ApplyFilters();
            StatusComboBox.SelectionChanged += (s,e) => ApplyFilters();
            PickupPointComboBox.SelectionChanged += (s,e) => ApplyFilters();
            SortComboBox.SelectionChanged += (s,e) => ApplyFilters();
            DateFromPicker.SelectedDateChanged += (s,e) => ApplyFilters();
            DateToPicker.SelectedDateChanged += (s,e) => ApplyFilters();
            ClearSearchButton.Click += (s,e) => { SearchTextBox.Text = ""; ApplyFilters(); };
            ResetFiltersButton.Click += (s,e) => ResetFilters();
        }
    }
    
    
    private void InitializeFilters()
    {
        Statuses.Add("Все статусы");
        Statuses.Add("Новый");
        Statuses.Add("В обработке");
        Statuses.Add("Готов к выдаче");
        Statuses.Add("Выдан");
        Statuses.Add("Отменен");
        PickupPoints.Add("Все точки");
        
        StatusComboBox.ItemsSource = Statuses;
        PickupPointComboBox.ItemsSource = PickupPoints;
        SortComboBox.SelectedIndex = 0;
        
        LoadPickupPointsAsync();
    }

    private async void LoadPickupPointsAsync()
    {
        string sql = @"SELECT pp.city || ', ' || pp.street || ', д. ' || pp.house_number FROM pickup_points pp ORDER BY pp.city, pp.street, pp.house_number";

        try
        {
            await using var conn = new NpgsqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                while (reader.Read())
                {
                    var point = reader.GetString(0);
                    if (!string.IsNullOrEmpty(point) && !PickupPoints.Contains(point))
                    {
                        PickupPoints.Add(point);
                    }
                }
                PickupPointComboBox.SelectedIndex = 0;
            });
        }
        catch (Exception ex)
        {
            ShowError($"Не удалось загрузить точки выдачи: {ex.Message}");
        }
    }

    private async void LoadOrders()
    {
        try
        {
            _allOrders = await GetOrdersFromDatabase();
            
            if (_userRole == "Менеджер")
            {
                Orders.Clear();
                foreach (var order in _allOrders.OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.OrderNumber))
                {
                    order.IsAdmin = false;
                    Orders.Add(order);
                }
            }
            else
            {
                ApplyFilters();
            }
        }
        catch (Exception ex)
        {
            ShowError($"Не удалось загрузить заказы: {ex.Message}");
        }
    }

    private async Task<List<OrderViewModel>> GetOrdersFromDatabase()
    {
        var orders = new List<OrderViewModel>();
        
        string sql = @"SELECT o.order_number, o.order_date, o.delivery_date, o.receipt_code, o.order_status, o.total_amount, CONCAT(u.first_name, ' ', u.last_name) AS client_name, CONCAT(p.city, ', ', p.street, ', д. ', p.house_number) AS pickup_point FROM orders o JOIN users u ON o.client_id = u.id JOIN pickup_points p ON o.pickup_point_id = p.id ORDER BY o.order_date DESC, o.order_number DESC";

        try
        {
            await using var conn = new NpgsqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                orders.Add(new OrderViewModel
                {
                    OrderNumber = reader.GetInt32(0),
                    OrderDate = reader.GetDateTime(1),
                    DeliveryDate = reader.GetDateTime(2),
                    ReceiptCode = reader.GetString(3),
                    Status = reader.GetString(4),
                    TotalAmount = reader.GetDecimal(5),
                    ClientName = reader.GetString(6),
                    PickupPoint = reader.GetString(7),
                    IsAdmin = (_userRole == "Администратор")
                });
            }
        }
        catch (Exception ex)
        {
            throw;
        }
        
        return orders;
    }

    private void ApplyFilters()
    {
        try
        {
            var filteredOrders = _allOrders.AsEnumerable();
            
            if (!string.IsNullOrEmpty(SearchTextBox?.Text))
            {
                var searchText = SearchTextBox.Text.ToLower();
                filteredOrders = filteredOrders.Where(o =>
                    o.OrderNumber.ToString().Contains(searchText) ||
                    o.ReceiptCode.ToLower().Contains(searchText) ||
                    o.ClientName.ToLower().Contains(searchText) ||
                    o.PickupPoint.ToLower().Contains(searchText));
            }
            
            if (StatusComboBox?.SelectedItem is string selectedStatus && selectedStatus != "Все статусы")
            {
                filteredOrders = filteredOrders.Where(o => o.Status == selectedStatus);
            }
            
            if (PickupPointComboBox?.SelectedItem is string selectedPoint && selectedPoint != "Все точки")
            {
                filteredOrders = filteredOrders.Where(o => o.PickupPoint == selectedPoint);
            }
            
            if (DateFromPicker?.SelectedDate.HasValue == true)
            {
                filteredOrders = filteredOrders.Where(o => o.OrderDate >= DateFromPicker.SelectedDate.Value.DateTime);
            }
            
            if (DateToPicker?.SelectedDate.HasValue == true)
            {
                var dateToInclusive = DateToPicker.SelectedDate.Value.DateTime.AddDays(1);
                filteredOrders = filteredOrders.Where(o => o.OrderDate < dateToInclusive);
            }
            
            if (SortComboBox?.SelectedItem is ComboBoxItem sortItem)
            {
                var sortBy = sortItem.Content?.ToString();
                filteredOrders = sortBy switch
                {
                    "По дате (новые)" => filteredOrders.OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.OrderNumber),
                    "По дате (старые)" => filteredOrders.OrderBy(o => o.OrderDate).ThenBy(o => o.OrderNumber),
                    "По сумме (возрастание)" => filteredOrders.OrderBy(o => o.TotalAmount),
                    "По сумме (убывание)" => filteredOrders.OrderByDescending(o => o.TotalAmount),
                    "По номеру (возрастание)" => filteredOrders.OrderBy(o => o.OrderNumber),
                    "По номеру (убывание)" => filteredOrders.OrderByDescending(o => o.OrderNumber),
                    _ => filteredOrders.OrderByDescending(o => o.OrderDate)
                };
            }
            
            Orders.Clear();
            foreach (var order in filteredOrders)
            {
                order.IsAdmin = (_userRole == "Администратор");
                Orders.Add(order);
            }
            
            if (_selectedOrder != null && !Orders.Contains(_selectedOrder))
            {
                ClearOrderDetails();
                _selectedOrder = null;
            }
        }
        catch (Exception ex)
        {
            ShowError($"Ошибка фильтрации: {ex.Message}");
        }
    }

    private async void LoadOrderDetails(int orderNumber)
    {
        try
        {
            var details = await GetOrderDetailsFromDatabase(orderNumber);
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                OrderDetails.Clear();
                foreach (var detail in details)
                {
                    OrderDetails.Add(detail);
                }
                
                if (_selectedOrder != null)
                {
                    DetailOrderNumber.Text = _selectedOrder.OrderNumber.ToString();
                    DetailOrderDate.Text = _selectedOrder.OrderDate.ToString("dd.MM.yyyy");
                    DetailDeliveryDate.Text = _selectedOrder.DeliveryDate.ToString("dd.MM.yyyy");
                    DetailClient.Text = _selectedOrder.ClientName;
                    DetailPickupPoint.Text = _selectedOrder.PickupPoint;
                    DetailReceiptCode.Text = _selectedOrder.ReceiptCode;
                    DetailStatus.Text = _selectedOrder.Status;
                    DetailStatusBorder.Background = _selectedOrder.StatusColor;
                    DetailTotalAmount.Text = _selectedOrder.TotalAmount.ToString("N2") + " ₽";
                }
            });
        }
        catch (Exception ex)
        {
            ShowError($"Не удалось загрузить детали заказа: {ex.Message}");
        }
    }

    private async Task<List<OrderDetailViewModel>> GetOrderDetailsFromDatabase(int orderNumber)
    {
        var details = new List<OrderDetailViewModel>();
        
        string sql = @"SELECT od.article, pn.product_name, od.quantity, od.unit_price, od.discount, od.total_price FROM order_details od JOIN products p ON od.article = p.article JOIN product_name pn ON p.product_name_id = pn.id WHERE od.order_number = @orderNumber ORDER BY pn.product_name";

        try
        {
            await using var conn = new NpgsqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("orderNumber", orderNumber);
            await using var reader = await cmd.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                details.Add(new OrderDetailViewModel
                {
                    Article = reader.GetString(0),
                    ProductName = reader.GetString(1),
                    Quantity = reader.GetInt32(2),
                    UnitPrice = reader.GetDecimal(3),
                    Discount = reader.GetDecimal(4),
                    TotalPrice = reader.GetDecimal(5)
                });
            }
        }
        catch (Exception ex)
        {
            throw;
        }
        
        return details;
    }

    private void ClearOrderDetails()
    {
        OrderDetails.Clear();
        DetailOrderNumber.Text = "-";
        DetailOrderDate.Text = "-";
        DetailDeliveryDate.Text = "-";
        DetailClient.Text = "-";
        DetailPickupPoint.Text = "-";
        DetailReceiptCode.Text = "-";
        DetailStatus.Text = "-";
        DetailStatusBorder.Background = Brushes.LightGray;
        DetailTotalAmount.Text = "0.00 ₽";
    }

    private void OrderItem_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is OrderViewModel order)
        {
            if (_selectedOrder != null)
            {
                _selectedOrder.IsSelected = false;
            }
            
            order.IsSelected = true;
            _selectedOrder = order;
            LoadOrderDetails(order.OrderNumber);
        }
    }

    private void ResetFilters()
    {
        StatusComboBox.SelectedIndex = 0;
        PickupPointComboBox.SelectedIndex = 0;
        SortComboBox.SelectedIndex = 0;
        SearchTextBox.Text = "";
        DateFromPicker.SelectedDate = null;
        DateToPicker.SelectedDate = null;
        ApplyFilters();
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        new ProductViewWindow(_userRole, 0).Show();
        this.Close();
    }

    private void AddOrderButton_Click(object? sender, RoutedEventArgs e)
    {
        var editWindow = new OrderEditWindow(_userRole, null);
        editWindow.Closed += (s, e) => LoadOrders();
        editWindow.ShowDialog(this);
    }

    private void EditOrderButton_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.CommandParameter is OrderViewModel order)
        {
            var editWindow = new OrderEditWindow(_userRole, order);
            editWindow.Closed += (s, e) => LoadOrders();
            editWindow.ShowDialog(this);
        }
    }

    private async void DeleteOrderButton_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.CommandParameter is OrderViewModel order)
        {
            var result = await ShowConfirmDialog("Удаление заказа", $"Вы уверены, что хотите удалить заказ №{order.OrderNumber}?");
            
            if (result)
            {
                try
                {
                    await using var conn = new NpgsqlConnection(ConnectionString);
                    await conn.OpenAsync();
                    
                    await using var cmdDetails = new NpgsqlCommand("DELETE FROM order_details WHERE order_number = @orderNumber", conn);
                    cmdDetails.Parameters.AddWithValue("orderNumber", order.OrderNumber);
                    await cmdDetails.ExecuteNonQueryAsync();
                    
                    await using var cmdOrder = new NpgsqlCommand("DELETE FROM orders WHERE order_number = @orderNumber", conn);
                    cmdOrder.Parameters.AddWithValue("orderNumber", order.OrderNumber);
                    await cmdOrder.ExecuteNonQueryAsync();
                    
                    LoadOrders();
                    ShowSuccess($"Заказ №{order.OrderNumber} успешно удален");
                }
                catch (Exception ex)
                {
                    ShowError($"Не удалось удалить заказ: {ex.Message}");
                }
            }
        }
    }

    private async Task<bool> ShowConfirmDialog(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brushes.White,
            FontFamily = new FontFamily("Times New Roman")
        };
        
        var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 20 };
        var messageText = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Black, FontSize = 14 };
        var buttonsPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Spacing = 20 };
        
        var yesButton = new Button { Content = "Да", Background = Brushes.LightCoral, Foreground = Brushes.White, Width = 80, Height = 35, FontWeight = FontWeight.Bold };
        var noButton = new Button { Content = "Нет", Background = Brushes.LightGray, Foreground = Brushes.Black, Width = 80, Height = 35, FontWeight = FontWeight.Bold };
        
        bool result = false;
        yesButton.Click += (s, e) => { result = true; dialog.Close(); };
        noButton.Click += (s, e) => { result = false; dialog.Close(); };
        
        buttonsPanel.Children.Add(yesButton);
        buttonsPanel.Children.Add(noButton);
        panel.Children.Add(messageText);
        panel.Children.Add(buttonsPanel);
        dialog.Content = panel;
        
        await dialog.ShowDialog(this);
        return result;
    }

    private void ShowError(string message)
    {
        var dialog = new Window
        {
            Title = "Ошибка",
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brushes.White,
            FontFamily = new FontFamily("Times New Roman")
        };
        
        var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 20 };
        var errorText = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Red, FontSize = 14 };
        var okButton = new Button { Content = "OK", Background = Brushes.LightGray, Foreground = Brushes.Black, Width = 100, Height = 35 };
        
        okButton.Click += (s, e) => dialog.Close();
        panel.Children.Add(errorText);
        panel.Children.Add(okButton);
        dialog.Content = panel;
        
        dialog.ShowDialog(this);
    }

    private void ShowSuccess(string message)
    {
        var dialog = new Window
        {
            Title = "Успешно",
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brushes.White,
            FontFamily = new FontFamily("Times New Roman")
        };
        
        var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 20 };
        var successText = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Green, FontSize = 14 };
        var okButton = new Button { Content = "OK", Background = Brushes.LightGreen, Foreground = Brushes.Black, Width = 100, Height = 35 };
        
        okButton.Click += (s, e) => dialog.Close();
        panel.Children.Add(successText);
        panel.Children.Add(okButton);
        dialog.Content = panel;
        
        dialog.ShowDialog(this);
    }
}