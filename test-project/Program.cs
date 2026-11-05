using System.Text.Json;
using MySqlConnector;
var d=JsonDocument.Parse(File.ReadAllText("dataset.json")).RootElement;
Database.Name=d.GetProperty("database").GetString()!;
int failed=0,passed=0;
void Check(string name,bool value){Console.WriteLine((value?"PASS ":"FAIL ")+name);if(value)passed++;else failed++;}
using var c=Database.Open();
string login="qa05_"+Guid.NewGuid().ToString("N")[..10];int uid=0,oid=0;
try
{
    string hash1=Users.Hash("Test-05!"),hash2=Users.Hash("Test-05!");
    Check("Случайная соль; одинаковые пароли дают разные записи",hash1!=hash2&&Users.Verify("Test-05!",hash1)&&!Users.Verify("wrong",hash1));
    uid=Users.Add(c,new(login,"Test-05!","Проверка","User",false));
    Check("Пользователь добавлен с нормализованным логином",Database.Rows(c,"SELECT id FROM users WHERE login=@l",("@l",login)).Count==1);
    bool unique=false;try{Users.Add(c,new(login.ToUpperInvariant(),"Test-05!","Повтор","User",false));}catch(MySqlException e)when(e.Number==1062){unique=true;}
    Check("Повтор логина отклонён с учётом регистра",unique);
    bool validation=false;try{Users.Add(c,new(login+"x","1","Неверный","User",false));}catch(ArgumentException){validation=true;}
    Check("Короткий пароль отклонён до записи",validation);
    LoginResult Go(string password,bool puzzle)=>Users.Login(c,new(login,password,[]),puzzle);
    int Count()=>Convert.ToInt32(Database.Rows(c,"SELECT failed_attempts FROM users WHERE id=@id",("@id",uid))[0]["failed_attempts"]);
    Check("Пустой пароль не увеличивает счётчик",!Go("",true).Success&&Count()==0);
    Check("Первая ошибка пароля — счётчик 1",!Go("wrong",true).Success&&Count()==1);
    Check("Вторая ошибка пазла — счётчик 2",!Go("Test-05!",false).Success&&Count()==2);
    Check("Успех после двух ошибок сбрасывает счётчик",Go("Test-05!",true).Success&&Count()==0);
    Go("wrong",false);Check("Два неверных условия одной попытки дают одну ошибку",Count()==1);
    Go("wrong",true);Check("Две ошибки ещё не блокируют",Count()==2&&!Convert.ToBoolean(Database.Rows(c,"SELECT blocked FROM users WHERE id=@id",("@id",uid))[0]["blocked"]));
    var blocked=Go("wrong",true);Check("Третья ошибка блокирует",blocked.Message==Users.Blocked&&Count()==3);
    using(var other=Database.Open())Check("Блокировка сохранена в новом соединении",Users.Login(other,new(login,"Test-05!",[]),true).Message==Users.Blocked);
    Database.Exec(c,"CALL sp_unblock(@id)",("@id",uid));Check("Снятие блокировки сбрасывает счётчик",Count()==0&&Go("Test-05!",true).Success);
    Users.Edit(c,uid,new(login,"Changed-05!","Изменённый пользователь","User",false));
    Check("Редактирование меняет имя и пароль",Go("Changed-05!",true).Success&&(string)Database.Rows(c,"SELECT full_name FROM users WHERE id=@id",("@id",uid))[0]["full_name"]! =="Изменённый пользователь");
    string customer=(string)Database.Rows(c,"SELECT id FROM customers ORDER BY id LIMIT 1")[0]["id"]!;
    int qty=d.GetProperty("quantity").GetInt32();
    decimal materials=0,operations=0;
    foreach(var n in d.GetProperty("material_norms").EnumerateArray().Where(n=>n.GetProperty("product").GetInt32()==1))
    {var m=d.GetProperty("materials").EnumerateArray().First(m=>m.GetProperty("id").GetInt32()==n.GetProperty("material").GetInt32());materials+=n.GetProperty("norm").GetDecimal()*m.GetProperty("price").GetDecimal();}
    foreach(var n in d.GetProperty("operation_norms").EnumerateArray().Where(n=>n.GetProperty("product").GetInt32()==1))
    {var o=d.GetProperty("operations").EnumerateArray().First(o=>o.GetProperty("id").GetInt32()==n.GetProperty("operation").GetInt32());operations+=n.GetProperty("hours").GetDecimal()*o.GetProperty("rate").GetDecimal();}
    var order=Database.Rows(c,"CALL sp_create_order(@c,@u,1,@q)",("@c",customer),("@u",uid),("@q",qty));oid=Convert.ToInt32(order[0]["id"]);
    decimal expected=Math.Round(qty*(materials+operations),2,MidpointRounding.AwayFromZero);
    Check($"Независимый расчёт заказа: {expected}",Calculations.OrderTotal(c,oid)==expected);
    Database.Exec(c,"UPDATE order_lines SET quantity=@q WHERE order_id=@id",("@q",qty+1),("@id",oid));
    Check("Изменённое количество пересчитывает итог",Calculations.OrderTotal(c,oid)==Math.Round((qty+1)*(materials+operations),2,MidpointRounding.AwayFromZero));
    int before=Database.Rows(c,"SELECT id FROM orders").Count;bool rollback=false;
    try{Database.Rows(c,"CALL sp_create_order(@c,@u,99999,1)",("@c",customer),("@u",uid));}catch(MySqlException){rollback=true;}
    Check("Ошибочная ссылка откатывает заголовок и строки",rollback&&before==Database.Rows(c,"SELECT id FROM orders").Count);
    Database.Exec(c,"CALL sp_change_status(@id,'В производстве')",("@id",oid));
    Check("Заказ на производство связан с заказом покупателя",Database.Rows(c,"SELECT id FROM production_orders WHERE order_id=@id",("@id",oid)).Count==1);
    Check("Два триггера записали создание и смену статуса",Database.Rows(c,"SELECT id FROM order_audit WHERE order_id=@id",("@id",oid)).Count==2);
    int count=Database.Rows(c,"SELECT id FROM customers").Count;
    Database.ImportCustomers(c,"customers.json");Check("Повторный импорт сохраняет количество заказчиков",Database.Rows(c,"SELECT id FROM customers").Count==count);
    Check("JSON addres перенесён в address",Database.Rows(c,"SELECT address FROM customers ORDER BY id").All(r=>r["address"] is string));
}
catch(Exception e){failed++;Console.WriteLine("FAIL Исключение: "+e.Message);}
finally
{
    if(oid>0){Database.Exec(c,"DELETE FROM production_orders WHERE order_id=@id",("@id",oid));Database.Exec(c,"DELETE FROM orders WHERE id=@id",("@id",oid));Database.Exec(c,"DELETE FROM order_audit WHERE order_id=@id",("@id",oid));}
    if(uid>0)Database.Exec(c,"DELETE FROM users WHERE login=@l",("@l",login));
}
Console.WriteLine($"ИТОГО PASS={passed}; FAIL={failed}");Environment.ExitCode=failed==0?0:1;
