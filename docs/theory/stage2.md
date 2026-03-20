# Этап 2. Объекты БД — VIEW, хранимые процедуры, триггеры

## 1. Представления (VIEW)

### 1.1 Что такое VIEW

Представление (VIEW) — это именованный хранимый SQL-запрос, к которому можно обращаться как к обычной таблице. VIEW не хранит данные физически (в большинстве СУБД); при каждом обращении к представлению СУБД выполняет исходный запрос и возвращает результат.

Представления используются для:

- **упрощения сложных запросов** — вместо многострочного JOIN можно написать `SELECT * FROM v_my_view`;
- **разграничения доступа** — пользователю предоставляется доступ к VIEW, а не к базовым таблицам, что скрывает чувствительные столбцы;
- **унификации логики** — бизнес-правила формирования выборки определяются один раз в VIEW, а не дублируются в каждом запросе приложения;
- **обратной совместимости** — при изменении структуры таблицы можно сохранить интерфейс VIEW.

### 1.2 Синтаксис CREATE VIEW

!!! example "MySQL"
    ```sql
    -- Создание представления
    CREATE VIEW view_name AS
    SELECT column1, column2, ...
    FROM table_name
    WHERE condition;

    -- Пересоздание (замена) без ошибки
    CREATE OR REPLACE VIEW view_name AS
    SELECT ...;

    -- Удаление
    DROP VIEW IF EXISTS view_name;
    ```

!!! example "PostgreSQL"
    ```sql
    CREATE OR REPLACE VIEW view_name AS
    SELECT column1, column2
    FROM table_name
    WHERE condition;

    -- Удаление
    DROP VIEW IF EXISTS view_name;
    ```

!!! example "MS SQL Server"
    ```sql
    -- Создание
    CREATE VIEW view_name AS
    SELECT column1, column2
    FROM table_name
    WHERE condition;

    -- Изменение (ALTER VIEW заменяет определение)
    ALTER VIEW view_name AS
    SELECT ...;

    -- Удаление
    DROP VIEW IF EXISTS view_name;
    ```

### 1.3 Примеры VIEW

**Простое представление — активные пользователи:**

!!! example "MySQL"
    ```sql
    CREATE OR REPLACE VIEW v_active_users AS
    SELECT
        user_id,
        full_name,
        email,
        created_at
    FROM users
    WHERE is_active = 1;
    ```

!!! example "PostgreSQL"
    ```sql
    CREATE OR REPLACE VIEW v_active_users AS
    SELECT
        user_id,
        full_name,
        email,
        created_at
    FROM users
    WHERE is_active = TRUE;
    ```

!!! example "MS SQL Server"
    ```sql
    CREATE VIEW v_active_users AS
    SELECT
        user_id,
        full_name,
        email,
        created_at
    FROM users
    WHERE is_active = 1;
    ```

**VIEW с JOIN — заказы с именем клиента и категории:**

!!! example "MySQL"
    ```sql
    CREATE OR REPLACE VIEW v_orders_detail AS
    SELECT
        o.order_id,
        o.order_date,
        o.status,
        o.total,
        c.full_name  AS client_name,
        c.email      AS client_email,
        cat.name     AS category_name
    FROM orders o
    JOIN clients c      ON o.client_id   = c.client_id
    JOIN categories cat ON o.category_id = cat.category_id;
    ```

!!! example "PostgreSQL"
    ```sql
    CREATE OR REPLACE VIEW v_orders_detail AS
    SELECT
        o.order_id,
        o.order_date,
        o.status,
        o.total,
        c.full_name  AS client_name,
        c.email      AS client_email,
        cat.name     AS category_name
    FROM orders o
    JOIN clients c      ON o.client_id   = c.client_id
    JOIN categories cat ON o.category_id = cat.category_id;
    ```

!!! example "MS SQL Server"
    ```sql
    CREATE VIEW v_orders_detail AS
    SELECT
        o.order_id,
        o.order_date,
        o.status,
        o.total,
        c.full_name  AS client_name,
        c.email      AS client_email,
        cat.name     AS category_name
    FROM orders o
    JOIN clients c      ON o.client_id   = c.client_id
    JOIN categories cat ON o.category_id = cat.category_id;
    ```

**VIEW с агрегатом — количество заказов и средний чек по клиентам:**

!!! example "MySQL"
    ```sql
    CREATE OR REPLACE VIEW v_client_stats AS
    SELECT
        c.client_id,
        c.full_name,
        COUNT(o.order_id)   AS order_count,
        AVG(o.total)        AS avg_order_total,
        MAX(o.order_date)   AS last_order_date
    FROM clients c
    LEFT JOIN orders o ON c.client_id = o.client_id
    GROUP BY c.client_id, c.full_name;
    ```

!!! example "PostgreSQL"
    ```sql
    CREATE OR REPLACE VIEW v_client_stats AS
    SELECT
        c.client_id,
        c.full_name,
        COUNT(o.order_id)        AS order_count,
        AVG(o.total)             AS avg_order_total,
        MAX(o.order_date)        AS last_order_date
    FROM clients c
    LEFT JOIN orders o ON c.client_id = o.client_id
    GROUP BY c.client_id, c.full_name;
    ```

!!! example "MS SQL Server"
    ```sql
    CREATE VIEW v_client_stats AS
    SELECT
        c.client_id,
        c.full_name,
        COUNT(o.order_id)        AS order_count,
        AVG(o.total)             AS avg_order_total,
        MAX(o.order_date)        AS last_order_date
    FROM clients c
    LEFT JOIN orders o ON c.client_id = o.client_id
    GROUP BY c.client_id, c.full_name;
    ```

### 1.4 Обновляемые и необновляемые VIEW

VIEW считается **обновляемым** (через него можно выполнять `INSERT`, `UPDATE`, `DELETE`), если оно:

- основано ровно на одной таблице;
- не содержит `GROUP BY`, `HAVING`, `DISTINCT`, агрегатных функций;
- не содержит подзапросов в `SELECT`;
- не использует `UNION`.

VIEW с `JOIN`, `GROUP BY` или агрегатами — **необновляемые**. Через них можно только читать данные.

!!! warning "Осторожно с обновляемыми VIEW"
    Даже обновляемые VIEW могут создавать неожиданное поведение. Например, после `INSERT` новая строка может не отображаться в VIEW, если она не соответствует фильтру `WHERE`. Чтобы предотвратить это, используйте опцию `WITH CHECK OPTION`.

### 1.5 Когда использовать VIEW vs запрос

| Ситуация | Рекомендация |
|----------|-------------|
| Запрос используется в одном месте | Обычный SQL-запрос в коде приложения |
| Запрос используется в нескольких местах | VIEW |
| Нужно скрыть структуру таблиц от пользователей | VIEW |
| Нужна максимальная производительность с кешированием | Материализованное VIEW (только PostgreSQL и MS SQL) |
| Сложный запрос в отчёте | VIEW или хранимая процедура |

---

## 2. Хранимые процедуры

### 2.1 Что такое хранимая процедура

Хранимая процедура (Stored Procedure) — это именованный блок SQL-кода, хранящийся на сервере БД и выполняемый по вызову. В отличие от VIEW, процедура может содержать:

- условные операторы (`IF`, `CASE`);
- циклы (`WHILE`, `LOOP`, `FOR`);
- объявление переменных;
- обработку ошибок (исключений);
- транзакции (`COMMIT`, `ROLLBACK`);
- несколько SQL-операторов.

**Преимущества хранимых процедур:**

**Безопасность** — приложение вызывает процедуру, не имея прямого доступа к таблицам; SQL-инъекция через параметры процедуры значительно сложнее.

**Производительность** — план выполнения процедуры компилируется и кешируется СУБД; при повторных вызовах разбор SQL-кода не требуется.

**Повторное использование** — бизнес-логика определяется один раз на сервере и вызывается из любого приложения (веб, десктоп, мобильное).

**Уменьшение трафика** — вместо передачи многострочного SQL-запроса клиент передаёт только имя процедуры и параметры.

### 2.2 Хранимые процедуры в MySQL

В MySQL перед созданием процедуры необходимо изменить разделитель команд с `;` на другой символ (обычно `$$` или `//`), иначе клиент завершит ввод на первой `;` внутри тела процедуры.

**Виды параметров:**

| Вид | Описание |
|-----|----------|
| `IN` | Входной параметр — значение передаётся в процедуру (по умолчанию) |
| `OUT` | Выходной параметр — процедура записывает значение, которое возвращается вызывающей стороне |
| `INOUT` | Входной и выходной одновременно |

!!! example "MySQL — процедура с IN параметром"
    ```sql
    DELIMITER $$

    CREATE PROCEDURE sp_update_order_status(
        IN p_order_id INT,
        IN p_new_status VARCHAR(30)
    )
    BEGIN
        UPDATE orders
        SET    status     = p_new_status,
               updated_at = NOW()
        WHERE  order_id = p_order_id;
    END$$

    DELIMITER ;

    -- Вызов:
    CALL sp_update_order_status(42, 'completed');
    ```

!!! example "MySQL — процедура с OUT параметром"
    ```sql
    DELIMITER $$

    CREATE PROCEDURE sp_get_client_order_count(
        IN  p_client_id  INT,
        OUT p_order_count INT
    )
    BEGIN
        SELECT COUNT(*) INTO p_order_count
        FROM   orders
        WHERE  client_id = p_client_id;
    END$$

    DELIMITER ;

    -- Вызов:
    CALL sp_get_client_order_count(5, @cnt);
    SELECT @cnt;
    ```

!!! example "MySQL — процедура с условием и SIGNAL"
    ```sql
    DELIMITER $$

    CREATE PROCEDURE sp_place_order(
        IN p_client_id  INT,
        IN p_category_id INT,
        IN p_description TEXT
    )
    BEGIN
        DECLARE v_active TINYINT DEFAULT 0;

        SELECT is_active INTO v_active
        FROM   clients
        WHERE  client_id = p_client_id;

        IF v_active = 0 THEN
            SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Клиент заблокирован';
        END IF;

        INSERT INTO orders (client_id, category_id, description, status, order_date)
        VALUES (p_client_id, p_category_id, p_description, 'new', NOW());
    END$$

    DELIMITER ;
    ```

### 2.3 Хранимые процедуры в PostgreSQL

В PostgreSQL хранимые процедуры создаются с помощью `CREATE OR REPLACE PROCEDURE` (с PostgreSQL 11) или через функции (`CREATE FUNCTION ... RETURNS void`). Язык тела — `PL/pgSQL` (процедурный язык).

!!! example "PostgreSQL — процедура (PL/pgSQL)"
    ```sql
    CREATE OR REPLACE PROCEDURE sp_update_order_status(
        p_order_id    INT,
        p_new_status  VARCHAR
    )
    LANGUAGE plpgsql
    AS $$
    BEGIN
        UPDATE orders
        SET    status     = p_new_status,
               updated_at = NOW()
        WHERE  order_id = p_order_id;
    END;
    $$;

    -- Вызов:
    CALL sp_update_order_status(42, 'completed');
    ```

!!! example "PostgreSQL — функция с RETURNS TABLE"
    ```sql
    CREATE OR REPLACE FUNCTION fn_get_client_orders(p_client_id INT)
    RETURNS TABLE (
        order_id   INT,
        order_date TIMESTAMPTZ,
        status     VARCHAR,
        total      NUMERIC
    )
    LANGUAGE plpgsql
    AS $$
    BEGIN
        RETURN QUERY
        SELECT o.order_id, o.order_date, o.status, o.total
        FROM   orders o
        WHERE  o.client_id = p_client_id
        ORDER  BY o.order_date DESC;
    END;
    $$;

    -- Вызов:
    SELECT * FROM fn_get_client_orders(5);
    ```

### 2.4 Хранимые процедуры в MS SQL Server

!!! example "MS SQL Server — процедура"
    ```sql
    CREATE OR ALTER PROCEDURE sp_update_order_status
        @order_id   INT,
        @new_status NVARCHAR(30)
    AS
    BEGIN
        SET NOCOUNT ON;

        UPDATE orders
        SET    status     = @new_status,
               updated_at = SYSDATETIME()
        WHERE  order_id = @order_id;
    END;
    GO

    -- Вызов:
    EXEC sp_update_order_status @order_id = 42, @new_status = N'completed';
    ```

!!! example "MS SQL Server — процедура с OUTPUT параметром"
    ```sql
    CREATE OR ALTER PROCEDURE sp_insert_client
        @full_name   NVARCHAR(150),
        @email       NVARCHAR(150),
        @new_id      INT OUTPUT
    AS
    BEGIN
        SET NOCOUNT ON;
        INSERT INTO clients (full_name, email, created_at, is_active)
        VALUES (@full_name, @email, SYSDATETIME(), 1);

        SET @new_id = SCOPE_IDENTITY();
    END;
    GO

    -- Вызов:
    DECLARE @id INT;
    EXEC sp_insert_client N'Иванов Иван', N'ivanov@mail.ru', @new_id = @id OUTPUT;
    SELECT @id AS new_client_id;
    ```

---

## 3. Триггеры

### 3.1 Что такое триггер

Триггер (Trigger) — это именованный объект БД, автоматически выполняемый СУБД в ответ на событие модификации данных в таблице: `INSERT`, `UPDATE` или `DELETE`.

Триггеры не вызываются явно — они срабатывают автоматически при наступлении связанного события. Это делает их мощным инструментом для:

- **аудита** — записи истории изменений (кто, что, когда изменил);
- **поддержания целостности** — проверки дополнительных бизнес-правил, которые нельзя выразить через `CHECK` ограничения;
- **автоматизации** — автоматического заполнения полей (например, `updated_at`);
- **синхронизации** — каскадного обновления данных в связанных таблицах.

### 3.2 Виды триггеров

| Тип | Описание |
|-----|----------|
| `BEFORE INSERT` | Срабатывает до вставки строки; можно изменить значения `NEW` |
| `AFTER INSERT` | Срабатывает после вставки строки |
| `BEFORE UPDATE` | Срабатывает до обновления; доступны `OLD` (старые значения) и `NEW` (новые) |
| `AFTER UPDATE` | Срабатывает после обновления |
| `BEFORE DELETE` | Срабатывает до удаления; доступны значения `OLD` |
| `AFTER DELETE` | Срабатывает после удаления |

В MySQL и PostgreSQL триггер срабатывает **для каждой строки** (`FOR EACH ROW`). В MS SQL Server триггеры срабатывают **один раз для всей операции**, но через виртуальные таблицы `INSERTED` и `DELETED` можно обработать все затронутые строки.

### 3.3 Триггеры в MySQL

!!! example "MySQL — AFTER UPDATE: логирование изменений (audit log)"
    ```sql
    DELIMITER $$

    CREATE TRIGGER trg_orders_audit
    AFTER UPDATE ON orders
    FOR EACH ROW
    BEGIN
        IF OLD.status <> NEW.status THEN
            INSERT INTO orders_audit_log
                (order_id, old_status, new_status, changed_at)
            VALUES
                (OLD.order_id, OLD.status, NEW.status, NOW());
        END IF;
    END$$

    DELIMITER ;
    ```

!!! example "MySQL — BEFORE UPDATE: автоматический timestamp"
    ```sql
    DELIMITER $$

    CREATE TRIGGER trg_orders_updated_at
    BEFORE UPDATE ON orders
    FOR EACH ROW
    BEGIN
        SET NEW.updated_at = NOW();
    END$$

    DELIMITER ;
    ```

!!! example "MySQL — BEFORE INSERT: проверка значений"
    ```sql
    DELIMITER $$

    CREATE TRIGGER trg_orders_check_total
    BEFORE INSERT ON orders
    FOR EACH ROW
    BEGIN
        IF NEW.total < 0 THEN
            SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Сумма заказа не может быть отрицательной';
        END IF;
    END$$

    DELIMITER ;
    ```

В MySQL таблицы `OLD` и `NEW` содержат значения строки до и после изменения:

| Событие | `OLD` | `NEW` |
|---------|-------|-------|
| `INSERT` | недоступно | вставляемая строка |
| `UPDATE` | строка до изменения | строка после изменения |
| `DELETE` | удаляемая строка | недоступно |

### 3.4 Триггеры в PostgreSQL

В PostgreSQL триггер состоит из двух объектов: **триггерной функции** (возвращает тип `TRIGGER`) и самого **триггера**, привязывающего функцию к таблице и событию.

!!! example "PostgreSQL — AFTER UPDATE: логирование изменений"
    ```sql
    -- Шаг 1: создаём функцию-триггер
    CREATE OR REPLACE FUNCTION fn_orders_audit()
    RETURNS TRIGGER
    LANGUAGE plpgsql
    AS $$
    BEGIN
        IF OLD.status IS DISTINCT FROM NEW.status THEN
            INSERT INTO orders_audit_log (order_id, old_status, new_status, changed_at)
            VALUES (OLD.order_id, OLD.status, NEW.status, NOW());
        END IF;
        RETURN NEW;
    END;
    $$;

    -- Шаг 2: создаём триггер, привязанный к функции
    CREATE OR REPLACE TRIGGER trg_orders_audit
    AFTER UPDATE ON orders
    FOR EACH ROW
    EXECUTE FUNCTION fn_orders_audit();
    ```

!!! example "PostgreSQL — BEFORE UPDATE: автоматический timestamp"
    ```sql
    CREATE OR REPLACE FUNCTION fn_set_updated_at()
    RETURNS TRIGGER
    LANGUAGE plpgsql
    AS $$
    BEGIN
        NEW.updated_at := NOW();
        RETURN NEW;
    END;
    $$;

    CREATE OR REPLACE TRIGGER trg_orders_updated_at
    BEFORE UPDATE ON orders
    FOR EACH ROW
    EXECUTE FUNCTION fn_set_updated_at();
    ```

!!! note "IS DISTINCT FROM в PostgreSQL"
    В PostgreSQL для сравнения значений, которые могут быть `NULL`, используйте `IS DISTINCT FROM` вместо `<>`. Оператор `<>` вернёт `NULL` при сравнении с `NULL`, тогда как `IS DISTINCT FROM` корректно обрабатывает `NULL`.

### 3.5 Триггеры в MS SQL Server

В MS SQL Server триггеры создаются непосредственно как объекты таблицы. Вместо `OLD`/`NEW` используются виртуальные таблицы `INSERTED` (новые/изменённые строки) и `DELETED` (удалённые/старые строки).

| Событие | `INSERTED` | `DELETED` |
|---------|------------|-----------|
| `INSERT` | вставленные строки | пуста |
| `UPDATE` | новые значения строк | старые значения строк |
| `DELETE` | пуста | удалённые строки |

!!! example "MS SQL Server — AFTER UPDATE: логирование изменений"
    ```sql
    CREATE OR ALTER TRIGGER trg_orders_audit
    ON orders
    AFTER UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;

        INSERT INTO orders_audit_log (order_id, old_status, new_status, changed_at)
        SELECT
            d.order_id,
            d.status    AS old_status,
            i.status    AS new_status,
            SYSDATETIME()
        FROM DELETED d
        JOIN INSERTED i ON d.order_id = i.order_id
        WHERE d.status <> i.status;
    END;
    GO
    ```

!!! example "MS SQL Server — INSTEAD OF INSERT: перехват вставки"
    ```sql
    -- Триггер INSTEAD OF полностью заменяет стандартную операцию
    CREATE OR ALTER TRIGGER trg_clients_insert_log
    ON clients
    INSTEAD OF INSERT
    AS
    BEGIN
        SET NOCOUNT ON;
        -- Нормально вставляем строки
        INSERT INTO clients (full_name, email, created_at, is_active)
        SELECT full_name, email, SYSDATETIME(), 1
        FROM INSERTED;

        -- Дополнительно: записываем в журнал
        INSERT INTO insert_log (table_name, record_count, logged_at)
        VALUES (N'clients', @@ROWCOUNT, SYSDATETIME());
    END;
    GO
    ```

### 3.6 Рекомендации по использованию триггеров

Триггеры — мощный инструмент, но злоупотребление ими приводит к проблемам:

- **Сложность отладки** — триггер срабатывает неявно; поведение системы может быть неочевидным.
- **Производительность** — каждая строка, затронутая `INSERT`/`UPDATE`/`DELETE`, активирует триггер; при массовых операциях это существенная нагрузка.
- **Цепочки триггеров** — триггер может вызвать изменение другой таблицы, которая имеет свой триггер, и т.д.

Рекомендуется использовать триггеры для: аудита изменений, автоматического заполнения служебных полей (`updated_at`, `created_at`) и простых проверок целостности. Сложную бизнес-логику лучше выносить в хранимые процедуры или код приложения.
