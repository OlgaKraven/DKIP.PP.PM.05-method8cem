# Этап 3. Безопасность — роли СУБД и хэширование паролей

## Задание

На данном этапе необходимо:

1. Создать не менее 3 ролей СУБД с разными уровнями доступа (только чтение, запись, администратор).
2. Создать пользователей MySQL и назначить им роли.
3. Реализовать хэширование паролей пользователей приложения (SHA-256 + соль или bcrypt).
4. Показать процесс регистрации пользователя с хэшированием в C#.
5. Показать проверку пароля при входе в C#.
6. Обновить схему таблицы `users` — добавить поле для хранения соли.
7. Сделать скриншоты: список ролей в MySQL, SHOW GRANTS, форма входа в приложении.

---

!!! warning "Важно"
    Ниже приведён **пример** выполнения этапа для предметной области «Helpdesk» (MySQL + C# Windows Forms). В своём варианте создайте роли и реализуйте хэширование для **вашей** предметной области.

---

## Пример: безопасность системы Helpdesk

### 1. Роли СУБД (MySQL 8+)

#### 1.1 Концепция ролей для Helpdesk

Для системы Helpdesk определены три роли СУБД, соответствующие принципу минимальных привилегий:

| Роль СУБД | Соответствие в приложении | Разрешённые операции |
|-----------|--------------------------|----------------------|
| `helpdesk_readonly` | Просмотр отчётов (клиентский интерфейс) | `SELECT` на все таблицы БД |
| `helpdesk_support` | Специалист поддержки | `SELECT, INSERT, UPDATE` на рабочие таблицы |
| `helpdesk_admin` | Администратор системы | `ALL PRIVILEGES` на БД |

#### 1.2 SQL-скрипт создания ролей и пользователей

```sql
-- ============================================================
-- Создание ролей (MySQL 8+)
-- ============================================================

CREATE ROLE IF NOT EXISTS 'helpdesk_readonly';
CREATE ROLE IF NOT EXISTS 'helpdesk_support';
CREATE ROLE IF NOT EXISTS 'helpdesk_admin';

-- ------------------------------------------------------------
-- Привилегии роли: только чтение
-- ------------------------------------------------------------
GRANT SELECT ON helpdesk.* TO 'helpdesk_readonly';

-- ------------------------------------------------------------
-- Привилегии роли: специалист поддержки
-- ------------------------------------------------------------
GRANT SELECT, INSERT, UPDATE ON helpdesk.tickets         TO 'helpdesk_support';
GRANT SELECT, INSERT, UPDATE ON helpdesk.comments        TO 'helpdesk_support';
GRANT SELECT, INSERT         ON helpdesk.ticket_history  TO 'helpdesk_support';
GRANT SELECT                 ON helpdesk.users           TO 'helpdesk_support';
GRANT SELECT                 ON helpdesk.categories      TO 'helpdesk_support';
GRANT SELECT                 ON helpdesk.departments     TO 'helpdesk_support';
GRANT SELECT                 ON helpdesk.roles           TO 'helpdesk_support';

-- Разрешить вызов хранимых процедур
GRANT EXECUTE ON PROCEDURE helpdesk.sp_change_ticket_status TO 'helpdesk_support';
GRANT EXECUTE ON PROCEDURE helpdesk.sp_assign_ticket        TO 'helpdesk_support';

-- ------------------------------------------------------------
-- Привилегии роли: администратор БД
-- ------------------------------------------------------------
GRANT ALL PRIVILEGES ON helpdesk.* TO 'helpdesk_admin';

-- ============================================================
-- Создание учётных записей MySQL и назначение ролей
-- ============================================================

-- Пользователь для клиентского интерфейса (только чтение)
CREATE USER IF NOT EXISTS 'app_readonly'@'localhost'
    IDENTIFIED BY 'ReadPass!23';
GRANT 'helpdesk_readonly' TO 'app_readonly'@'localhost';
SET DEFAULT ROLE ALL TO 'app_readonly'@'localhost';

-- Пользователь для специалиста поддержки
CREATE USER IF NOT EXISTS 'app_support'@'localhost'
    IDENTIFIED BY 'SuppPass!23';
GRANT 'helpdesk_support' TO 'app_support'@'localhost';
SET DEFAULT ROLE ALL TO 'app_support'@'localhost';

-- Пользователь-администратор
CREATE USER IF NOT EXISTS 'app_admin'@'localhost'
    IDENTIFIED BY 'AdminPass!23';
GRANT 'helpdesk_admin' TO 'app_admin'@'localhost';
SET DEFAULT ROLE ALL TO 'app_admin'@'localhost';

FLUSH PRIVILEGES;
```

#### 1.3 Проверка привилегий

```sql
-- Привилегии пользователя app_support
SHOW GRANTS FOR 'app_support'@'localhost';

-- Привилегии с учётом ролей
SHOW GRANTS FOR 'app_support'@'localhost' USING 'helpdesk_support';
```

---

### 2. Обновление таблицы `users` — добавление соли

В схеме из Этапа 1 таблица `users` содержала только `password_hash VARCHAR(64)`. Для корректной реализации SHA-256 + соль необходимо добавить поле `password_salt`.

```sql
-- Добавить столбец password_salt (если таблица уже создана)
ALTER TABLE users
    ADD COLUMN password_salt VARCHAR(32) NOT NULL DEFAULT ''
    AFTER password_hash;

-- Обновить ограничение: password_salt не может быть пустым
ALTER TABLE users
    MODIFY COLUMN password_salt VARCHAR(32) NOT NULL;
```

Обновлённая структура полей пароля в таблице `users`:

| Поле | Тип | Описание |
|------|-----|----------|
| `password_salt` | `VARCHAR(32)` | Случайная соль — hex-строка (32 символа = 16 байт) |
| `password_hash` | `VARCHAR(64)` | SHA-256 хэш от `(salt + password)`, hex-строка (64 символа) |

#### Обновлённая хранимая процедура `sp_register_user`

Процедура теперь принимает соль и хэш отдельными параметрами. Хэширование выполняется в C#.

```sql
DELIMITER $$

DROP PROCEDURE IF EXISTS sp_register_user$$

CREATE PROCEDURE sp_register_user(
    IN p_login    VARCHAR(80),
    IN p_salt     VARCHAR(32),
    IN p_hash     VARCHAR(64),
    IN p_fullname VARCHAR(150),
    IN p_email    VARCHAR(150),
    IN p_role_id  INT,
    IN p_dept_id  INT
)
BEGIN
    IF EXISTS (SELECT 1 FROM users WHERE login = p_login) THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Логин уже занят';
    END IF;

    IF EXISTS (SELECT 1 FROM users WHERE email = p_email) THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Email уже зарегистрирован';
    END IF;

    INSERT INTO users
        (login, password_salt, password_hash, full_name, email,
         role_id, department_id, is_active, created_at)
    VALUES
        (p_login, p_salt, p_hash, p_fullname, p_email,
         p_role_id, p_dept_id, 1, NOW());
END$$

DELIMITER ;
```

---

### 3. Хэширование паролей в C#

#### 3.1 Класс `PasswordHelper`

```csharp
using System;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Вспомогательный класс для безопасного хэширования паролей.
/// Алгоритм: SHA-256 с уникальной солью для каждого пользователя.
/// </summary>
public static class PasswordHelper
{
    /// <summary>
    /// Генерирует случайную соль: 16 случайных байт → hex-строка (32 символа).
    /// Соль уникальна для каждого пользователя и хранится в БД открыто.
    /// </summary>
    public static string GenerateSalt()
    {
        byte[] saltBytes = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(saltBytes);
        return BitConverter.ToString(saltBytes).Replace("-", "").ToLower();
    }

    /// <summary>
    /// Вычисляет SHA-256 хэш от строки (соль + пароль).
    /// Возвращает hex-строку (64 символа).
    /// </summary>
    public static string HashPassword(string password, string salt)
    {
        using var sha = SHA256.Create();
        byte[] inputBytes = Encoding.UTF8.GetBytes(salt + password);
        byte[] hashBytes  = sha.ComputeHash(inputBytes);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
    }

    /// <summary>
    /// Проверяет введённый пароль: вычисляет хэш от (соль + пароль)
    /// и сравнивает с хранимым хэшем без учёта регистра.
    /// </summary>
    public static bool VerifyPassword(string inputPassword, string salt, string storedHash)
    {
        string computedHash = HashPassword(inputPassword, salt);
        return computedHash.Equals(storedHash, StringComparison.OrdinalIgnoreCase);
    }
}
```

#### 3.2 Регистрация пользователя в C# WinForms

```csharp
using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Windows.Forms;

public partial class RegisterForm : Form
{
    private void btnRegister_Click(object sender, EventArgs e)
    {
        string login    = txtLogin.Text.Trim();
        string password = txtPassword.Text;
        string fullName = txtFullName.Text.Trim();
        string email    = txtEmail.Text.Trim();
        int    roleId   = Convert.ToInt32(comboBoxRole.SelectedValue);
        int    deptId   = Convert.ToInt32(comboBoxDept.SelectedValue);

        // Базовая валидация
        if (string.IsNullOrWhiteSpace(login) || password.Length < 6)
        {
            MessageBox.Show("Логин не может быть пустым, пароль — не менее 6 символов.",
                            "Ошибка ввода", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Генерация соли и хэша в C# (до обращения к БД)
        string salt = PasswordHelper.GenerateSalt();
        string hash = PasswordHelper.HashPassword(password, salt);

        try
        {
            using var conn = DBConnection.GetConnection();
            conn.Open();

            using var cmd = new MySqlCommand("sp_register_user", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("p_login",    login);
            cmd.Parameters.AddWithValue("p_salt",     salt);
            cmd.Parameters.AddWithValue("p_hash",     hash);
            cmd.Parameters.AddWithValue("p_fullname", fullName);
            cmd.Parameters.AddWithValue("p_email",    email);
            cmd.Parameters.AddWithValue("p_role_id",  roleId);
            cmd.Parameters.AddWithValue("p_dept_id",  deptId);

            cmd.ExecuteNonQuery();

            MessageBox.Show($"Пользователь «{login}» успешно зарегистрирован.",
                            "Регистрация", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.Close();
        }
        catch (MySqlException ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка БД",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
```

#### 3.3 Проверка пароля при входе в C# WinForms

```csharp
public partial class LoginForm : Form
{
    private void btnLogin_Click(object sender, EventArgs e)
    {
        string login    = txtLogin.Text.Trim();
        string password = txtPassword.Text;

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show("Введите логин и пароль.", "Ошибка",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            using var conn = DBConnection.GetConnection();
            conn.Open();

            // Шаг 1: получить соль, хэш и данные пользователя по логину
            const string sql = @"
                SELECT u.user_id, u.full_name, u.password_salt, u.password_hash,
                       r.role_name
                FROM users u
                JOIN roles r ON u.role_id = r.role_id
                WHERE u.login = @login AND u.is_active = 1";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@login", login);
            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
            {
                MessageBox.Show("Пользователь не найден или заблокирован.", "Ошибка входа",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int    userId    = reader.GetInt32("user_id");
            string fullName  = reader.GetString("full_name");
            string salt      = reader.GetString("password_salt");
            string storedHash= reader.GetString("password_hash");
            string roleName  = reader.GetString("role_name");
            reader.Close();

            // Шаг 2: проверить пароль
            if (!PasswordHelper.VerifyPassword(password, salt, storedHash))
            {
                MessageBox.Show("Неверный пароль.", "Ошибка входа",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Шаг 3: сохранить данные сессии и открыть главную форму
            AppSession.UserId   = userId;
            AppSession.UserName = fullName;
            AppSession.RoleName = roleName;

            var mainForm = new MainForm();
            mainForm.Show();
            this.Hide();
        }
        catch (MySqlException ex)
        {
            MessageBox.Show($"Ошибка подключения к БД: {ex.Message}", "Ошибка",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

/// <summary>
/// Хранит данные текущего сеанса (статический класс).
/// </summary>
public static class AppSession
{
    public static int    UserId   { get; set; }
    public static string UserName { get; set; }
    public static string RoleName { get; set; }

    public static void Clear()
    {
        UserId   = 0;
        UserName = null;
        RoleName = null;
    }
}
```

---

### Вывод по этапу

На данном этапе реализованы следующие меры безопасности для системы Helpdesk:

**Уровень СУБД:** созданы 3 роли MySQL (`helpdesk_readonly`, `helpdesk_support`, `helpdesk_admin`) с разграниченными привилегиями по принципу минимальных прав. Каждой роли назначен отдельный пользователь MySQL.

**Уровень приложения:** реализовано хэширование паролей по схеме SHA-256 + уникальная соль. Схема таблицы `users` расширена полем `password_salt`. Хэширование выполняется в C# средствами `System.Security.Cryptography.SHA256`. Процедура входа: получить соль → вычислить хэш → сравнить с хранимым.

Пароли в базе данных хранятся **только в виде хэша** — восстановить исходный пароль по хэшу невозможно.
