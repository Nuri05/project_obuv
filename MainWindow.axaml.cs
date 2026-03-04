using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using Npgsql;
using Avalonia.Threading;

namespace obuvv;

public partial class MainWindow : Window
{
    private const string ConnectionString = "Host=localhost;Database=Obuv1;Username=postgres;Password=12345;"; 

    public MainWindow()
    {
        InitializeComponent();
        this.Title = "ООО Обувь - Вход";
    }

    private void LoginButton_Click(object? sender, RoutedEventArgs e)
    {
        if (TxtLogin == null || TxtPassword == null || TxtErrorMessage == null) 
        {
            ShowError("Внутренняя ошибка приложения.");
            return;
        }

        string login = TxtLogin.Text?.Trim() ?? "";
        string password = TxtPassword.Text?.Trim() ?? "";


        if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
        {
            ShowError("Пожалуйста, введите логин и пароль.");
            return;
        }

        TxtErrorMessage.IsVisible = false; 
        
        User user = AuthenticateUser(login, password);

        if (user != null)
        {

            var productView = new ProductViewWindow(user.Role, user.Id);
            productView.Show();
            this.Close(); 
        }
        else
        {
            ShowError("Неверный логин или пароль.");
        }
    }
    
    private void GuestButton_Click(object? sender, RoutedEventArgs e)
    {
        var productView = new ProductViewWindow("Гость", 0);
        productView.Show();
        this.Close();
    }

    private void ShowError(string message)
    {
        if (TxtErrorMessage != null)
        {
            TxtErrorMessage.Text = message;
            TxtErrorMessage.IsVisible = true;
        }
    }

    private class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Role { get; set; } = "";
    }

    private User? AuthenticateUser(string login, string password)
    {
        try
        {
            using (var conn = new NpgsqlConnection(ConnectionString))
            {
                conn.Open();
                
                string sql = @"
                    SELECT 
                        u.id, 
                        u.first_name || ' ' || u.last_name AS full_name, 
                        r.role 
                    FROM users u
                    JOIN user_role r ON u.role_id = r.id
                    WHERE u.login = @login AND u.password = @password";
                
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("login", login);
                    cmd.Parameters.AddWithValue("password", password);
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            User foundUser = new User();
                            foundUser.Id = reader.GetInt32(0); 
                            foundUser.Name = reader.GetString(1);
                            foundUser.Role = reader.GetString(2); 
                            
                            return foundUser;
                        }
                    }
                }
            }
            
            return null; 
        }
        catch (NpgsqlException ex)
        {
            Dispatcher.UIThread.InvokeAsync(() => 
                ShowError("Ошибка базы данных: Не удалось подключиться к базе данных."));
            
            Console.WriteLine("Ошибка подключения к БД: " + ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.InvokeAsync(() => 
                ShowError("Произошла ошибка при входе в систему."));
            
            Console.WriteLine("Другая ошибка: " + ex.Message);
            return null;
        }
    }
}