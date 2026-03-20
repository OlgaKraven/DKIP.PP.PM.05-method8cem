# Этап 2. Объекты БД — VIEW, хранимые процедуры, триггеры

## Задание

На данном этапе необходимо:

1. Создать не менее 2 представлений (VIEW): одно с JOIN, одно с агрегатом.
2. Написать не менее 3 хранимых процедур, автоматизирующих типовые операции.
3. Создать не менее 2 триггеров (например, аудит изменений и авто-timestamp).
4. Продемонстрировать вызов процедур и работу триггеров.
5. Показать, как VIEW и процедуры вызываются из кода приложения (C#).
6. Сделать скриншоты: объекты в MySQL Workbench, результаты SELECT из VIEW, результаты CALL процедур.

---

!!! warning "Важно"
    Ниже приведён **пример** выполнения этапа для предметной области «Helpdesk» (MySQL + C# Windows Forms). Используйте этот пример как образец. В своём варианте объекты должны соответствовать **вашей** предметной области.

---

## Пример: объекты БД для системы Helpdesk

### 1. Представления (VIEW)

#### 1.1 VIEW `v_tickets_full` — Полная информация о заявках

Это представление объединяет таблицы `tickets`, `users` (дважды: автор и исполнитель), `categories` и `departments`. Оно используется как основной источник данных для `DataGridView` в форме списка заявок.

```sql
CREATE OR REPLACE VIEW v_tickets_full AS
SELECT
    t.ticket_id,
    t.title,
    t.status,
    t.priority,
    c.name          AS category,
    u1.full_name    AS author,
    d.name          AS author_dept,
    u2.full_name    AS assignee,
    t.created_at,
    t.updated_at
FROM tickets t
JOIN  categories c   ON t.category_id   = c.category_id
JOIN  users u1       ON t.created_by    = u1.user_id
JOIN  departments d  ON u1.department_id = d.department_id
LEFT JOIN users u2   ON t.assigned_to   = u2.user_id;
```

Проверка:

```sql
SELECT * FROM v_tickets_full ORDER BY created_at DESC;
```

#### 1.2 VIEW `v_support_stats` — Нагрузка на специалистов поддержки

Это представление показывает, сколько активных и решённых заявок у каждого специалиста. Используется на дашборде администратора.

```sql
CREATE OR REPLACE VIEW v_support_stats AS
SELECT
    u.user_id,
    u.full_name,
    COUNT(CASE WHEN t.status NOT IN ('closed', 'resolved') THEN 1 END)
        AS active_tickets,
    COUNT(CASE WHEN t.status = 'resolved' THEN 1 END)
        AS resolved_tickets
FROM users u
LEFT JOIN tickets t ON u.user_id = t.assigned_to
WHERE u.role_id = (SELECT role_id FROM roles WHERE role_name = 'support')
GROUP BY u.user_id, u.full_name;
```

Проверка:

```sql
SELECT * FROM v_support_stats ORDER BY active_tickets DESC;
```

!!! info "Почему LEFT JOIN в v_support_stats?"
    Используется `LEFT JOIN`, а не `INNER JOIN`, чтобы в результат попали специалисты с нулём заявок. Если применить `INNER JOIN`, специалисты без заявок не отобразятся в статистике.

---

### 2. Хранимые процедуры

#### 2.1 `sp_change_ticket_status` — Смена статуса заявки

Процедура обновляет статус заявки и фиксирует изменение в `ticket_history`. Параметры: ID заявки, новый статус, ID пользователя, примечание.

```sql
DELIMITER $$

CREATE PROCEDURE sp_change_ticket_status(
    IN p_ticket_id  INT,
    IN p_new_status VARCHAR(30),
    IN p_user_id    INT,
    IN p_note       TEXT
)
BEGIN
    DECLARE v_old_status VARCHAR(30);

    -- Получить текущий статус
    SELECT status INTO v_old_status
    FROM tickets
    WHERE ticket_id = p_ticket_id;

    -- Обновить статус заявки
    UPDATE tickets
    SET    status     = p_new_status,
           updated_at = NOW(),
           closed_at  = CASE
                            WHEN p_new_status IN ('closed', 'resolved') THEN NOW()
                            ELSE closed_at
                        END
    WHERE  ticket_id = p_ticket_id;

    -- Зафиксировать изменение в истории
    INSERT INTO ticket_history (ticket_id, changed_by, old_status, new_status, changed_at, note)
    VALUES (p_ticket_id, p_user_id, v_old_status, p_new_status, NOW(), p_note);
END$$

DELIMITER ;
```

Вызов:

```sql
CALL sp_change_ticket_status(1, 'in_progress', 2, 'Принято в работу');
```

#### 2.2 `sp_assign_ticket` — Назначение исполнителя

Процедура назначает исполнителя на заявку. Перед назначением проверяет, что указанный пользователь имеет роль `support`. При ошибке — генерирует исключение.

```sql
DELIMITER $$

CREATE PROCEDURE sp_assign_ticket(
    IN p_ticket_id       INT,
    IN p_support_user_id INT
)
BEGIN
    DECLARE v_role VARCHAR(50);

    -- Получить название роли пользователя
    SELECT r.role_name INTO v_role
    FROM users u
    JOIN roles r ON u.role_id = r.role_id
    WHERE u.user_id = p_support_user_id;

    -- Проверить роль
    IF v_role = 'support' THEN
        UPDATE tickets
        SET assigned_to = p_support_user_id,
            status      = 'in_progress',
            updated_at  = NOW()
        WHERE ticket_id = p_ticket_id;

        INSERT INTO ticket_history (ticket_id, changed_by, old_status, new_status, note)
        SELECT p_ticket_id, p_support_user_id, status, 'in_progress',
               CONCAT('Назначен исполнитель: user_id=', p_support_user_id)
        FROM tickets WHERE ticket_id = p_ticket_id;
    ELSE
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Пользователь не является специалистом поддержки';
    END IF;
END$$

DELIMITER ;
```

Вызов:

```sql
CALL sp_assign_ticket(1, 2);
```

#### 2.3 `sp_register_user` — Регистрация нового пользователя

Процедура принимает готовый хэш пароля (хэширование выполняется в C# перед вызовом) и вставляет запись в таблицу `users`.

```sql
DELIMITER $$

CREATE PROCEDURE sp_register_user(
    IN p_login    VARCHAR(80),
    IN p_hash     VARCHAR(64),
    IN p_fullname VARCHAR(150),
    IN p_email    VARCHAR(150),
    IN p_role_id  INT,
    IN p_dept_id  INT
)
BEGIN
    -- Проверить, не занят ли логин
    IF EXISTS (SELECT 1 FROM users WHERE login = p_login) THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Логин уже занят';
    END IF;

    -- Проверить, не занят ли email
    IF EXISTS (SELECT 1 FROM users WHERE email = p_email) THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Email уже зарегистрирован';
    END IF;

    INSERT INTO users (login, password_hash, full_name, email, role_id, department_id, is_active, created_at)
    VALUES (p_login, p_hash, p_fullname, p_email, p_role_id, p_dept_id, 1, NOW());
END$$

DELIMITER ;
```

Вызов:

```sql
CALL sp_register_user('novak', SHA2('TempPass!1', 256), 'Новак Дмитрий', 'novak@co.ru', 3, 2);
```

---

### 3. Триггеры

#### 3.1 `trg_log_status_change` — Аудит смены статуса

Триггер срабатывает **после** обновления строки в `tickets`. Если статус изменился, записывает событие в `ticket_history`.

```sql
DELIMITER $$

CREATE TRIGGER trg_log_status_change
AFTER UPDATE ON tickets
FOR EACH ROW
BEGIN
    IF OLD.status <> NEW.status THEN
        INSERT INTO ticket_history
            (ticket_id, changed_by, old_status, new_status, changed_at, note)
        VALUES
            (OLD.ticket_id, NULL, OLD.status, NEW.status, NOW(),
             'Автоматическая запись триггера');
    END IF;
END$$

DELIMITER ;
```

!!! note "Триггер vs процедура sp_change_ticket_status"
    Процедура `sp_change_ticket_status` тоже записывает историю вручную. Если использовать и триггер, и процедуру одновременно, в истории появятся дублирующие записи. В учебном примере они показаны оба для иллюстрации двух механизмов. В реальном проекте выберите один подход.

#### 3.2 `trg_update_timestamp` — Авто-обновление `updated_at`

Триггер срабатывает **перед** обновлением строки в `tickets` и автоматически проставляет текущее время в поле `updated_at`.

```sql
DELIMITER $$

CREATE TRIGGER trg_update_timestamp
BEFORE UPDATE ON tickets
FOR EACH ROW
BEGIN
    SET NEW.updated_at = NOW();
END$$

DELIMITER ;
```

Благодаря этому триггеру разработчику не нужно вручную передавать `updated_at = NOW()` в каждом `UPDATE`-запросе — СУБД делает это автоматически.

---

### 4. Работа с VIEW и процедурами из C#

#### 4.1 SELECT из VIEW в C# — как обычная таблица

С точки зрения приложения VIEW ничем не отличается от таблицы. Достаточно указать имя VIEW в `SELECT`.

```csharp
using MySql.Data.MySqlClient;
using System.Data;
using System.Windows.Forms;

public partial class TicketsForm : Form
{
    private void LoadTickets()
    {
        using var conn = DBConnection.GetConnection();
        conn.Open();

        const string sql = @"
            SELECT ticket_id, title, status, priority,
                   category, author, author_dept, assignee, created_at
            FROM v_tickets_full
            ORDER BY created_at DESC";

        using var adapter = new MySqlDataAdapter(sql, conn);
        var table = new DataTable();
        adapter.Fill(table);

        dataGridViewTickets.DataSource = table;

        // Настройка заголовков столбцов
        dataGridViewTickets.Columns["ticket_id"].HeaderText  = "№";
        dataGridViewTickets.Columns["title"].HeaderText      = "Тема";
        dataGridViewTickets.Columns["status"].HeaderText     = "Статус";
        dataGridViewTickets.Columns["priority"].HeaderText   = "Приоритет";
        dataGridViewTickets.Columns["category"].HeaderText   = "Категория";
        dataGridViewTickets.Columns["author"].HeaderText     = "Автор";
        dataGridViewTickets.Columns["author_dept"].HeaderText = "Отдел";
        dataGridViewTickets.Columns["assignee"].HeaderText   = "Исполнитель";
        dataGridViewTickets.Columns["created_at"].HeaderText = "Создана";
    }
}
```

#### 4.2 Вызов хранимой процедуры из C#

Для вызова хранимой процедуры используется `MySqlCommand` с `CommandType.StoredProcedure`. Параметры передаются через коллекцию `Parameters`.

```csharp
using MySql.Data.MySqlClient;
using System.Data;

public class TicketService
{
    /// <summary>
    /// Сменить статус заявки через хранимую процедуру sp_change_ticket_status.
    /// </summary>
    public void ChangeTicketStatus(int ticketId, string newStatus, int userId, string note)
    {
        using var conn = DBConnection.GetConnection();
        conn.Open();

        using var cmd = new MySqlCommand("sp_change_ticket_status", conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        cmd.Parameters.AddWithValue("p_ticket_id",  ticketId);
        cmd.Parameters.AddWithValue("p_new_status", newStatus);
        cmd.Parameters.AddWithValue("p_user_id",    userId);
        cmd.Parameters.AddWithValue("p_note",       note ?? string.Empty);

        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Назначить исполнителя на заявку через sp_assign_ticket.
    /// </summary>
    public void AssignTicket(int ticketId, int supportUserId)
    {
        using var conn = DBConnection.GetConnection();
        conn.Open();

        using var cmd = new MySqlCommand("sp_assign_ticket", conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        cmd.Parameters.AddWithValue("p_ticket_id",       ticketId);
        cmd.Parameters.AddWithValue("p_support_user_id", supportUserId);

        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Зарегистрировать нового пользователя через sp_register_user.
    /// Хэширование выполняется в C# до вызова процедуры.
    /// </summary>
    public void RegisterUser(string login, string password, string fullName,
                              string email, int roleId, int deptId)
    {
        string hash = PasswordHelper.HashPassword(password, PasswordHelper.GenerateSalt());

        using var conn = DBConnection.GetConnection();
        conn.Open();

        using var cmd = new MySqlCommand("sp_register_user", conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        cmd.Parameters.AddWithValue("p_login",    login);
        cmd.Parameters.AddWithValue("p_hash",     hash);
        cmd.Parameters.AddWithValue("p_fullname", fullName);
        cmd.Parameters.AddWithValue("p_email",    email);
        cmd.Parameters.AddWithValue("p_role_id",  roleId);
        cmd.Parameters.AddWithValue("p_dept_id",  deptId);

        cmd.ExecuteNonQuery();
    }
}
```

#### 4.3 Обработка ошибок процедуры (SIGNAL) в C#

Если процедура вызывает `SIGNAL SQLSTATE '45000'`, MySQL генерирует исключение, которое в C# ловится как `MySqlException`.

```csharp
private void btnAssign_Click(object sender, EventArgs e)
{
    if (dataGridViewTickets.SelectedRows.Count == 0) return;

    int ticketId      = Convert.ToInt32(dataGridViewTickets.SelectedRows[0].Cells["ticket_id"].Value);
    int supportUserId = Convert.ToInt32(comboBoxSupport.SelectedValue);

    try
    {
        var service = new TicketService();
        service.AssignTicket(ticketId, supportUserId);
        MessageBox.Show("Исполнитель назначен.", "Успех",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
        LoadTickets(); // обновить DataGridView
    }
    catch (MySql.Data.MySqlClient.MySqlException ex)
    {
        MessageBox.Show($"Ошибка БД: {ex.Message}", "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
```

---

### Вывод по этапу

На данном этапе созданы объекты базы данных системы Helpdesk:

- 2 представления: `v_tickets_full` (JOIN из 5 таблиц) и `v_support_stats` (агрегат с `COUNT` и `CASE`).
- 3 хранимые процедуры: `sp_change_ticket_status`, `sp_assign_ticket`, `sp_register_user` — автоматизируют основные бизнес-операции.
- 2 триггера: `trg_log_status_change` (аудит в `AFTER UPDATE`) и `trg_update_timestamp` (авто-поле в `BEFORE UPDATE`).

Показано обращение к VIEW и вызов процедур из C# через `MySqlCommand` с `CommandType.StoredProcedure`.
