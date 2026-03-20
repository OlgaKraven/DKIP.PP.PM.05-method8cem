# Этап 4. CRUD и запросы с JOIN

## 1. CRUD-операции

CRUD — аббревиатура четырёх базовых операций с данными: **C**reate, **R**ead, **U**pdate, **D**elete. В SQL эти операции реализуются командами `INSERT`, `SELECT`, `UPDATE`, `DELETE` соответственно.

---

### 1.1 INSERT — вставка данных

**Базовый синтаксис:**

```sql
INSERT INTO table_name (column1, column2, column3)
VALUES (value1, value2, value3);
```

**Вставка нескольких строк одним запросом (MySQL, PostgreSQL):**

```sql
INSERT INTO categories (name, description)
VALUES
    ('Сеть',       'Проблемы с сетью'),
    ('Оборудование','Неисправности техники'),
    ('Программы',  'Ошибки ПО');
```

В MS SQL Server синтаксис аналогичен с MySQL 10 начиная с версии 2008:

```sql
INSERT INTO categories (name, description)
VALUES
    (N'Сеть',        N'Проблемы с сетью'),
    (N'Оборудование', N'Неисправности техники');
```

**Получение ID вставленной строки:**

| СУБД | Способ | Пример |
|------|--------|--------|
| MySQL | `LAST_INSERT_ID()` | `SELECT LAST_INSERT_ID();` |
| PostgreSQL | `RETURNING` в INSERT | `INSERT INTO ... RETURNING id;` |
| MS SQL Server | `SCOPE_IDENTITY()` или `OUTPUT` | `SELECT SCOPE_IDENTITY();` |

!!! example "MySQL — вставка с получением ID"
    ```sql
    INSERT INTO tickets (title, description, status, category_id, created_by, created_at)
    VALUES ('Нет интернета', 'Не работает wi-fi', 'new', 1, 3, NOW());

    SELECT LAST_INSERT_ID() AS new_ticket_id;
    ```

!!! example "PostgreSQL — RETURNING"
    ```sql
    INSERT INTO tickets (title, description, status, category_id, created_by)
    VALUES ('Нет интернета', 'Не работает wi-fi', 'new', 1, 3)
    RETURNING ticket_id;
    ```

!!! example "MS SQL Server — SCOPE_IDENTITY"
    ```sql
    INSERT INTO tickets (title, description, status, category_id, created_by)
    VALUES (N'Нет интернета', N'Не работает wi-fi', N'new', 1, 3);

    SELECT SCOPE_IDENTITY() AS new_ticket_id;

    -- Или через OUTPUT:
    INSERT INTO tickets (title, description, status)
    OUTPUT INSERTED.ticket_id
    VALUES (N'Нет интернета', N'Не работает wi-fi', N'new');
    ```

---

### 1.2 SELECT — выборка данных

**Фильтрация с WHERE:**

```sql
-- Все открытые заявки с высоким приоритетом
SELECT ticket_id, title, status, priority, created_at
FROM tickets
WHERE status = 'new'
  AND priority = 'high';
```

**Сортировка ORDER BY:**

```sql
SELECT ticket_id, title, status, created_at
FROM tickets
ORDER BY created_at DESC;  -- сначала новые

-- Сортировка по нескольким полям
ORDER BY priority ASC, created_at DESC;
```

**Ограничение количества строк:**

| СУБД | Синтаксис |
|------|-----------|
| MySQL / PostgreSQL | `LIMIT n` / `LIMIT n OFFSET m` |
| MS SQL Server | `TOP n` / `OFFSET m ROWS FETCH NEXT n ROWS ONLY` |

```sql
-- MySQL / PostgreSQL: первые 10 строк, начиная с 21-й
SELECT * FROM tickets
ORDER BY created_at DESC
LIMIT 10 OFFSET 20;
```

```sql
-- MS SQL Server: аналог
SELECT * FROM tickets
ORDER BY created_at DESC
OFFSET 20 ROWS FETCH NEXT 10 ROWS ONLY;
```

**Подзапрос в WHERE:**

```sql
-- Заявки, назначенные специалистам из отдела ИТ
SELECT t.ticket_id, t.title, t.status
FROM tickets t
WHERE t.assigned_to IN (
    SELECT u.user_id
    FROM users u
    JOIN departments d ON u.department_id = d.department_id
    WHERE d.name = 'ИТ-отдел'
);
```

---

### 1.3 UPDATE — обновление данных

**Базовый синтаксис:**

```sql
UPDATE table_name
SET column1 = value1,
    column2 = value2
WHERE condition;
```

!!! warning "Всегда используйте WHERE в UPDATE"
    Команда `UPDATE` без `WHERE` обновит **все** строки таблицы. Это одна из наиболее распространённых ошибок при работе с БД.

**Обновление через JOIN (MySQL):**

```sql
-- Переназначить все заявки уволенного сотрудника (user_id=5)
-- другому сотруднику (user_id=8)
UPDATE tickets t
JOIN users u ON t.assigned_to = u.user_id
SET t.assigned_to = 8,
    t.updated_at  = NOW()
WHERE u.user_id = 5
  AND t.status NOT IN ('closed', 'resolved');
```

**Обновление через JOIN (PostgreSQL / MS SQL):**

```sql
-- PostgreSQL: UPDATE с FROM
UPDATE tickets t
SET    assigned_to = 8,
       updated_at  = NOW()
FROM   users u
WHERE  t.assigned_to = u.user_id
  AND  u.user_id = 5
  AND  t.status NOT IN ('closed', 'resolved');
```

---

### 1.4 DELETE — удаление данных

**Базовый синтаксис:**

```sql
DELETE FROM table_name
WHERE condition;
```

**Каскадное удаление:** если таблица `comments` имеет внешний ключ на `tickets` с `ON DELETE CASCADE`, то при удалении заявки все её комментарии удаляются автоматически.

```sql
-- При наличии ON DELETE CASCADE на comments и ticket_history:
DELETE FROM tickets WHERE ticket_id = 42;
-- Автоматически удалятся: comments и ticket_history для этой заявки
```

**Поведение ON DELETE:**

| Действие | Поведение | Когда использовать |
|----------|-----------|-------------------|
| `CASCADE` | Дочерние записи удаляются вместе | Когда дочерние записи не имеют смысла без родителя (комментарии к заявке) |
| `RESTRICT` | Удаление запрещено, если есть дочерние | Когда удаление опасно (заявки к пользователю) |
| `SET NULL` | FK в дочерних ставится в NULL | Когда связь необязательна (заявка без исполнителя) |

---

## 2. JOIN — объединение таблиц

Оператор `JOIN` позволяет комбинировать строки из двух и более таблиц на основе условия связи. Без `JOIN` реляционная БД теряет основное преимущество — способность хранить данные без дублирования и связывать их при запросах.

### 2.1 INNER JOIN

`INNER JOIN` возвращает только те строки, для которых есть совпадение в **обеих** таблицах.

```mermaid
flowchart LR
    A["Таблица A"] --- J["INNER JOIN\n(только совпадения)"] --- B["Таблица B"]
    style J fill:#c8e6c9,stroke:#388e3c
```

///caption
Рисунок 1 — INNER JOIN: строки только при наличии совпадения в обеих таблицах
///

```sql
-- Заявки с именем автора (только заявки, у которых есть пользователь-автор)
SELECT
    t.ticket_id,
    t.title,
    t.status,
    u.full_name AS author
FROM tickets t
INNER JOIN users u ON t.created_by = u.user_id;
```

Ключевое слово `INNER` необязательно: `JOIN` без модификатора — это `INNER JOIN`.

### 2.2 LEFT JOIN

`LEFT JOIN` (LEFT OUTER JOIN) возвращает **все** строки из левой таблицы, и соответствующие строки из правой. Если соответствия нет — правая часть заполняется `NULL`.

```sql
-- Все пользователи, даже если у них нет заявок
SELECT
    u.user_id,
    u.full_name,
    COUNT(t.ticket_id) AS ticket_count
FROM users u
LEFT JOIN tickets t ON u.user_id = t.created_by
GROUP BY u.user_id, u.full_name;
```

**LEFT JOIN для поиска «записей без пары»:**

```sql
-- Найти заявки без назначенного исполнителя
SELECT t.ticket_id, t.title, t.status
FROM tickets t
LEFT JOIN users u ON t.assigned_to = u.user_id
WHERE u.user_id IS NULL;
```

Паттерн `LEFT JOIN ... WHERE right_table.id IS NULL` — стандартный способ найти записи в левой таблице, для которых нет соответствия в правой.

### 2.3 RIGHT JOIN

`RIGHT JOIN` (RIGHT OUTER JOIN) — зеркальный к `LEFT JOIN`: возвращает **все** строки из правой таблицы и соответствующие строки из левой. На практике `RIGHT JOIN` можно заменить `LEFT JOIN` с переставленными таблицами; используется реже.

```sql
-- Все категории, включая те, у которых нет заявок
SELECT
    c.name         AS category,
    COUNT(t.ticket_id) AS ticket_count
FROM tickets t
RIGHT JOIN categories c ON t.category_id = c.category_id
GROUP BY c.category_id, c.name;
```

### 2.4 FULL OUTER JOIN

`FULL OUTER JOIN` возвращает все строки из обеих таблиц. При отсутствии совпадения с одной стороны — `NULL`.

!!! note "FULL OUTER JOIN в MySQL"
    MySQL не поддерживает `FULL OUTER JOIN` напрямую. Его эмулируют через `UNION` двух запросов: `LEFT JOIN UNION RIGHT JOIN`.

```sql
-- MySQL: эмуляция FULL OUTER JOIN через UNION
SELECT u.user_id, u.full_name, t.ticket_id, t.title
FROM users u
LEFT JOIN tickets t ON u.user_id = t.created_by

UNION

SELECT u.user_id, u.full_name, t.ticket_id, t.title
FROM users u
RIGHT JOIN tickets t ON u.user_id = t.created_by;
```

```sql
-- PostgreSQL / MS SQL Server: прямой синтаксис
SELECT u.user_id, u.full_name, t.ticket_id, t.title
FROM users u
FULL OUTER JOIN tickets t ON u.user_id = t.created_by;
```

### 2.5 Self JOIN

Self JOIN — таблица объединяется сама с собой. Используется, когда таблица хранит иерархические или рекурсивные связи (например, сотрудник → руководитель, категория → родительская категория).

```sql
-- Сотрудники с именем их руководителя
-- (employees.manager_id → employees.employee_id)
SELECT
    e.employee_id,
    e.full_name      AS employee,
    m.full_name      AS manager
FROM employees e
LEFT JOIN employees m ON e.manager_id = m.employee_id;
```

### 2.6 JOIN трёх и более таблиц

Практические запросы часто объединяют три и более таблиц. Каждый следующий `JOIN` добавляет связь.

```sql
-- Полный вид заявки: заявка + автор + отдел автора + категория + исполнитель
SELECT
    t.ticket_id,
    t.title,
    t.status,
    t.priority,
    c.name          AS category,
    u1.full_name    AS author,
    d.name          AS author_dept,
    u2.full_name    AS assignee
FROM tickets t
JOIN  categories c   ON t.category_id   = c.category_id
JOIN  users u1       ON t.created_by    = u1.user_id
LEFT JOIN departments d  ON u1.department_id = d.department_id
LEFT JOIN users u2   ON t.assigned_to   = u2.user_id
ORDER BY t.created_at DESC;
```

Здесь используется `INNER JOIN` для обязательных связей (заявка обязательно имеет автора и категорию) и `LEFT JOIN` для необязательных (заявка может не иметь исполнителя или автор может не принадлежать отделу).

### 2.7 Агрегат с GROUP BY и HAVING

`GROUP BY` группирует строки по значению столбца, `HAVING` фильтрует уже сгруппированные данные (аналог `WHERE`, но для агрегатов).

```sql
-- Количество заявок по статусам, показать только статусы с > 5 заявками
SELECT
    status,
    COUNT(*) AS cnt
FROM tickets
GROUP BY status
HAVING COUNT(*) > 5
ORDER BY cnt DESC;
```

```sql
-- Нагрузка по специалистам: только те, у кого > 3 активных заявок
SELECT
    u.full_name,
    COUNT(t.ticket_id) AS active_count
FROM users u
JOIN tickets t ON u.user_id = t.assigned_to
WHERE t.status NOT IN ('closed', 'resolved')
GROUP BY u.user_id, u.full_name
HAVING COUNT(t.ticket_id) > 3
ORDER BY active_count DESC;
```

### 2.8 Подзапрос в WHERE и EXISTS

**Подзапрос (subquery)** — запрос, вложенный в другой запрос. Может использоваться в `WHERE`, `FROM`, `SELECT`.

```sql
-- Пользователи, у которых нет ни одной заявки (NOT IN)
SELECT user_id, full_name
FROM users
WHERE user_id NOT IN (
    SELECT DISTINCT created_by FROM tickets
);

-- Аналог через NOT EXISTS (часто эффективнее)
SELECT u.user_id, u.full_name
FROM users u
WHERE NOT EXISTS (
    SELECT 1 FROM tickets t WHERE t.created_by = u.user_id
);
```

### 2.9 Различия между СУБД

| Особенность | MySQL | PostgreSQL | MS SQL Server |
|-------------|-------|------------|---------------|
| `FULL OUTER JOIN` | Через UNION | Поддерживается | Поддерживается |
| `LIMIT n` | `LIMIT n` | `LIMIT n` | `TOP n` или `FETCH NEXT n ROWS ONLY` |
| Псевдоним таблицы в UPDATE/DELETE | Поддерживается | Поддерживается (через FROM) | Поддерживается |
| `RETURNING` после DML | Нет | Поддерживается | Через `OUTPUT` |
| Последний вставленный ID | `LAST_INSERT_ID()` | `RETURNING id` или `lastval()` | `SCOPE_IDENTITY()` |

---

## 3. Итоги

Операции CRUD и JOIN являются основой работы с реляционными базами данных. Понимание различных видов JOIN позволяет правильно формулировать запросы:

- `INNER JOIN` — когда нужны только записи с совпадением в обеих таблицах;
- `LEFT JOIN` — когда нужны все записи из основной таблицы, включая те, у которых нет связанных записей;
- `RIGHT JOIN` — когда нужны все записи из присоединяемой таблицы;
- `FULL OUTER JOIN` — когда нужны все записи из обеих таблиц.

Правильное использование `WHERE`, `GROUP BY`, `HAVING`, `ORDER BY` и подзапросов позволяет получать точные выборки, необходимые для работы информационной системы.
