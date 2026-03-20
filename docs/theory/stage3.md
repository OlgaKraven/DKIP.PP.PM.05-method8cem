# Этап 3. Безопасность — роли СУБД и хэширование паролей

## 1. Роли и привилегии СУБД

### 1.1 Системные роли СУБД и бизнес-роли приложения

В информационных системах существуют два принципиально разных уровня ролей, которые нельзя путать.

| Параметр | Роли СУБД | Бизнес-роли в таблице приложения |
|----------|-----------|----------------------------------|
| Где хранятся | На сервере СУБД (системные объекты) | В таблице БД (например, `roles`) |
| Кто проверяет | СУБД при подключении | Код приложения при выполнении операций |
| Управляет | DBA (администратор БД) | Администратор приложения |
| Примеры | `app_readonly`, `app_support`, `app_admin` | `client`, `support`, `manager` |
| Гранулярность | На уровне объектов БД (таблица, схема) | На уровне функциональности приложения |

**Роли СУБД** ограничивают то, что приложение (как подключение к БД) может делать на уровне сервера. Например, подключение с ролью `app_readonly` физически не сможет выполнить `INSERT`, даже если в коде приложения ошибка.

**Бизнес-роли** в таблице приложения определяют, что видит и может делать конкретный пользователь системы. Например, пользователь с ролью `client` не видит кнопку «Назначить исполнителя» в интерфейсе.

### 1.2 Принцип минимальных привилегий

Принцип минимальных привилегий (Principle of Least Privilege, PoLP) гласит: каждый субъект (пользователь, процесс, приложение) должен иметь только те права, которые необходимы для выполнения своих функций — не больше.

Практическое применение:

- Приложение для работы с клиентами не должно иметь право `DROP TABLE`.
- Публичный API, доступный из интернета, должен работать через учётную запись только с `SELECT` на нужные таблицы.
- Администратор БД должен использовать привилегированную учётную запись только при необходимости, а для повседневной работы — обычную.

### 1.3 Роли и привилегии в MySQL

MySQL 8.0 ввёл поддержку ролей. В более ранних версиях роли эмулировались через несколько пользователей с одинаковыми правами.

!!! example "MySQL — создание ролей и пользователей"
    ```sql
    -- Создание ролей (MySQL 8+)
    CREATE ROLE IF NOT EXISTS 'role_readonly';
    CREATE ROLE IF NOT EXISTS 'role_editor';
    CREATE ROLE IF NOT EXISTS 'role_admin';

    -- Назначение привилегий ролям
    GRANT SELECT ON mydb.* TO 'role_readonly';

    GRANT SELECT, INSERT, UPDATE ON mydb.* TO 'role_editor';

    GRANT ALL PRIVILEGES ON mydb.* TO 'role_admin';

    -- Создание пользователей
    CREATE USER 'user_viewer'@'localhost' IDENTIFIED BY 'ViewPass!99';
    CREATE USER 'user_editor'@'localhost' IDENTIFIED BY 'EditPass!99';
    CREATE USER 'user_dba'@'localhost'    IDENTIFIED BY 'DbaPass!99';

    -- Назначение ролей пользователям
    GRANT 'role_readonly' TO 'user_viewer'@'localhost';
    GRANT 'role_editor'   TO 'user_editor'@'localhost';
    GRANT 'role_admin'    TO 'user_dba'@'localhost';

    -- Установить роль активной по умолчанию
    SET DEFAULT ROLE ALL TO 'user_viewer'@'localhost';
    SET DEFAULT ROLE ALL TO 'user_editor'@'localhost';
    SET DEFAULT ROLE ALL TO 'user_dba'@'localhost';

    FLUSH PRIVILEGES;
    ```

!!! example "MySQL — просмотр привилегий"
    ```sql
    -- Привилегии конкретного пользователя
    SHOW GRANTS FOR 'user_viewer'@'localhost';

    -- Роли конкретного пользователя
    SHOW GRANTS FOR 'user_editor'@'localhost' USING 'role_editor';

    -- Текущий пользователь
    SHOW GRANTS;
    ```

!!! example "MySQL — отзыв привилегий и удаление"
    ```sql
    -- Отозвать конкретную привилегию
    REVOKE INSERT ON mydb.orders FROM 'user_editor'@'localhost';

    -- Отозвать роль
    REVOKE 'role_readonly' FROM 'user_viewer'@'localhost';

    -- Удалить пользователя
    DROP USER IF EXISTS 'user_viewer'@'localhost';
    ```

### 1.4 Роли и привилегии в PostgreSQL

В PostgreSQL понятия «пользователь» и «роль» объединены: `CREATE USER` — это синоним `CREATE ROLE ... WITH LOGIN`.

!!! example "PostgreSQL — роли и привилегии"
    ```sql
    -- Создание ролей без возможности входа (группы)
    CREATE ROLE role_readonly;
    CREATE ROLE role_editor;
    CREATE ROLE role_admin;

    -- Привилегии на схему (право использовать объекты схемы)
    GRANT USAGE ON SCHEMA public TO role_readonly;
    GRANT USAGE ON SCHEMA public TO role_editor;

    -- Привилегии на таблицы
    GRANT SELECT ON ALL TABLES IN SCHEMA public TO role_readonly;
    GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO role_editor;
    GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO role_admin;
    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO role_editor;

    -- Создание пользователей
    CREATE USER user_viewer WITH PASSWORD 'ViewPass!99';
    CREATE USER user_editor WITH PASSWORD 'EditPass!99';
    CREATE USER user_dba    WITH PASSWORD 'DbaPass!99';

    -- Назначение ролей
    GRANT role_readonly TO user_viewer;
    GRANT role_editor   TO user_editor;
    GRANT role_admin    TO user_dba;
    ```

### 1.5 Роли и привилегии в MS SQL Server

В MS SQL Server модель безопасности двухуровневая: `LOGIN` (уровень сервера) → `USER` (уровень базы данных).

!!! example "MS SQL Server — создание логина, пользователя, роли"
    ```sql
    -- Уровень сервера: создание логина
    CREATE LOGIN login_viewer WITH PASSWORD = 'ViewPass!99';
    CREATE LOGIN login_editor WITH PASSWORD = 'EditPass!99';
    CREATE LOGIN login_dba    WITH PASSWORD = 'DbaPass!99';

    -- Уровень БД: создание пользователей, привязанных к логинам
    USE mydb;
    GO

    CREATE USER user_viewer FOR LOGIN login_viewer;
    CREATE USER user_editor FOR LOGIN login_editor;
    CREATE USER user_dba    FOR LOGIN login_dba;

    -- Создание ролей БД
    CREATE ROLE role_readonly;
    CREATE ROLE role_editor;
    CREATE ROLE role_admin;

    -- Назначение привилегий ролям
    GRANT SELECT ON SCHEMA::dbo TO role_readonly;
    GRANT SELECT, INSERT, UPDATE ON SCHEMA::dbo TO role_editor;
    GRANT CONTROL ON SCHEMA::dbo TO role_admin;

    -- Включение пользователей в роли
    ALTER ROLE role_readonly ADD MEMBER user_viewer;
    ALTER ROLE role_editor   ADD MEMBER user_editor;
    ALTER ROLE role_admin    ADD MEMBER user_dba;
    ```

!!! note "sp_addrolemember (устаревший способ)"
    В старом коде можно встретить `EXEC sp_addrolemember 'role_readonly', 'user_viewer'`. Начиная с SQL Server 2012 рекомендуется использовать `ALTER ROLE ... ADD MEMBER`.

---

## 2. Хэширование паролей

### 2.1 Почему нельзя хранить пароль в открытом виде

Хранение паролей в открытом виде (plaintext) — критическая уязвимость. При компрометации базы данных злоумышленник получает мгновенный доступ ко всем аккаунтам, а пользователи, использующие один пароль на нескольких сайтах, подвергают риску все свои учётные записи.

Согласно нормам информационной безопасности (OWASP, NIST SP 800-63B), пароли **никогда** не должны храниться в открытом виде. Минимально допустимый способ — одностороннее хэширование с солью.

### 2.2 Шифрование vs хэширование vs соль

| Операция | Описание | Обратимость | Применение для паролей |
|----------|----------|-------------|------------------------|
| Шифрование | Преобразование данных с ключом | Обратимо (есть ключ) | Нет — при компрометации ключа все пароли раскрыты |
| Хэширование | Одностороннее преобразование в строку фиксированной длины | Необратимо | Да, но без соли уязвимо к rainbow-таблицам |
| Хэширование + соль | Хэш вычисляется от `salt + password` | Необратимо | Да — рекомендуемый минимум |
| Адаптивное хэширование (bcrypt, Argon2) | Специализированные алгоритмы с настраиваемой стоимостью | Необратимо | Да — наилучший вариант |

**Соль (salt)** — случайная строка, уникальная для каждого пользователя, добавляемая к паролю перед хэшированием. Соль хранится в базе данных рядом с хэшем (она не секретна). Смысл соли:

- два пользователя с одинаковым паролем будут иметь разные хэши;
- rainbow-таблицы (предвычисленные хэши) становятся бесполезны.

### 2.3 MD5 и SHA-1 — устаревшие алгоритмы

**MD5** и **SHA-1** — алгоритмы, созданные не для хранения паролей, а для проверки целостности файлов. Они:

- очень быстрые (злоумышленник может перебирать миллиарды вариантов в секунду на GPU);
- имеют известные коллизии (MD5);
- давно скомпрометированы для использования в качестве хранения паролей.

!!! danger "Не используйте MD5/SHA-1 для паролей"
    Использование `MD5(password)` или `SHA1(password)` для хранения паролей является грубой ошибкой безопасности. Базы данных с MD5-хэшами паролей взламываются за минуты с помощью онлайн-сервисов.

### 2.4 SHA-256 + соль — базовый уровень

SHA-256 — значительно более стойкий алгоритм, чем MD5/SHA-1. В паре с уникальной солью обеспечивает приемлемый уровень защиты для учебных и небольших производственных систем.

Схема хранения:

- столбец `password_salt VARCHAR(32)` — случайная соль (hex-строка)
- столбец `password_hash VARCHAR(64)` — SHA-256 хэш от `(salt + password)`, 64 hex-символа

!!! example "MySQL — SHA-256 хранение"
    ```sql
    -- Вычисление хэша SHA-256 средствами MySQL
    SELECT SHA2('mysaltpassword123', 256);
    -- Результат: 64-символьная hex-строка

    -- При регистрации:
    -- соль генерируется в коде приложения (C#), передаётся в процедуру
    INSERT INTO users (login, password_salt, password_hash, full_name, email)
    VALUES ('ivanov', 'a3f1...', SHA2(CONCAT('a3f1...', 'mypassword'), 256), 'Иванов', 'iv@mail.ru');

    -- Проверка пароля при входе:
    SELECT user_id, full_name
    FROM users
    WHERE login = 'ivanov'
      AND password_hash = SHA2(CONCAT(password_salt, 'mypassword'), 256)
      AND is_active = 1;
    ```

### 2.5 bcrypt — рекомендуемый алгоритм

**bcrypt** — адаптивная хэш-функция, специально разработанная для хранения паролей (Niels Provos, David Mazières, 1999). Её ключевые особенности:

- **Встроенная соль** — bcrypt автоматически генерирует и включает соль в хэш; хранить соль отдельно не нужно.
- **Настраиваемый cost factor** — параметр «стоимости» (work factor, rounds) определяет, сколько вычислений требует хэширование. При росте производительности железа cost factor можно увеличить.
- **Фиксированная длина вывода** — результат всегда 60 символов, включает алгоритм, cost factor и соль.

Хэш bcrypt выглядит так: `$2b$12$LQv3c1yqBWVHxkd0LQ1Cr.eDmqb0KOBfFMNfMz6gFJjpQGhJM9Hs`

- `$2b$` — версия алгоритма
- `12` — cost factor (2^12 = 4096 итераций)
- Следующие 22 символа — соль
- Оставшиеся 31 символ — хэш

!!! example "PostgreSQL — bcrypt через расширение pgcrypto"
    ```sql
    -- Установка расширения
    CREATE EXTENSION IF NOT EXISTS pgcrypto;

    -- Хэширование пароля (cost = 12)
    SELECT crypt('mypassword', gen_salt('bf', 12));
    -- Результат: строка ~60 символов

    -- Хранение при регистрации:
    INSERT INTO users (login, password_hash, full_name, email)
    VALUES ('ivanov', crypt('mypassword', gen_salt('bf', 12)), 'Иванов', 'iv@mail.ru');

    -- Проверка при входе (хэш сравнивается сам с собой):
    SELECT user_id, full_name
    FROM users
    WHERE login = 'ivanov'
      AND password_hash = crypt('mypassword', password_hash);
    ```

В MySQL встроенной поддержки bcrypt нет. Рекомендуется выполнять хэширование bcrypt в коде приложения (C#) и хранить готовый хэш в базе данных.

### 2.6 Хэширование паролей в C#

!!! example "C# — SHA-256 + соль"
    ```csharp
    using System;
    using System.Security.Cryptography;
    using System.Text;

    public static class PasswordHelper
    {
        /// <summary>
        /// Генерирует случайную соль: 16 байт → hex-строка (32 символа).
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
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(salt + password));
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

        /// <summary>
        /// Проверяет пароль: вычисляет хэш и сравнивает со сохранённым.
        /// </summary>
        public static bool VerifyPassword(string password, string salt, string storedHash)
        {
            string computed = HashPassword(password, salt);
            return computed.Equals(storedHash, StringComparison.OrdinalIgnoreCase);
        }
    }
    ```

Таблица `users` должна содержать поля:

```sql
password_salt VARCHAR(32)  NOT NULL,
password_hash VARCHAR(64)  NOT NULL,
```

### 2.7 Регистрация и вход в C# WinForms

!!! example "C# — регистрация пользователя"
    ```csharp
    private void RegisterUser(string login, string password, string fullName, string email)
    {
        string salt = PasswordHelper.GenerateSalt();
        string hash = PasswordHelper.HashPassword(password, salt);

        using var conn = DBConnection.GetConnection();
        conn.Open();
        using var cmd = new MySqlCommand("sp_register_user", conn)
        {
            CommandType = System.Data.CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("p_login",    login);
        cmd.Parameters.AddWithValue("p_salt",     salt);
        cmd.Parameters.AddWithValue("p_hash",     hash);
        cmd.Parameters.AddWithValue("p_fullname", fullName);
        cmd.Parameters.AddWithValue("p_email",    email);
        cmd.ExecuteNonQuery();
    }
    ```

!!! example "C# — проверка пароля при входе"
    ```csharp
    private bool TryLogin(string login, string inputPassword, out int userId)
    {
        userId = 0;
        using var conn = DBConnection.GetConnection();
        conn.Open();

        // 1. Получить соль и хэш пользователя по логину
        const string query = @"
            SELECT user_id, password_salt, password_hash
            FROM users
            WHERE login = @login AND is_active = 1";

        using var cmd = new MySqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@login", login);
        using var reader = cmd.ExecuteReader();

        if (!reader.Read()) return false;

        userId             = reader.GetInt32("user_id");
        string storedSalt  = reader.GetString("password_salt");
        string storedHash  = reader.GetString("password_hash");
        reader.Close();

        // 2. Проверить пароль
        return PasswordHelper.VerifyPassword(inputPassword, storedSalt, storedHash);
    }
    ```

### 2.8 Сравнительная таблица алгоритмов

| Алгоритм | Скорость | Стойкость к перебору | Рекомендуется для паролей |
|----------|----------|----------------------|---------------------------|
| MD5 | Очень быстрый | Крайне низкая | Нет |
| SHA-1 | Очень быстрый | Низкая | Нет |
| SHA-256 | Быстрый | Средняя (с солью) | С осторожностью |
| SHA-256 + соль | Быстрый | Удовлетворительная | Для учебных проектов |
| bcrypt (cost 12) | Медленный (намеренно) | Высокая | Да |
| Argon2id | Медленный, настраиваемый | Очень высокая | Да (лучший выбор) |

!!! info "Для учебного проекта"
    В рамках производственной практики реализация SHA-256 + соль на C# является достаточной и демонстрирует понимание принципов безопасного хранения паролей. Для реального производственного приложения рекомендуется использовать библиотеку BCrypt.Net-Next.
