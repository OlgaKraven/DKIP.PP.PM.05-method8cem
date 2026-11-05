using System.Text.Json;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using MySqlConnector;
var dataset=JsonDocument.Parse(File.ReadAllText("dataset.json")).RootElement;
Database.Name=dataset.GetProperty("database").GetString()!;
if(!System.Text.RegularExpressions.Regex.IsMatch(Database.Name,"^pp05_[a-z0-9]+$"))throw new Exception("Недопустимое имя учебной БД");
if(args.Contains("--init")){Database.Init(dataset);return;}
if(args.Contains("--import")){using var c=Database.Open();Console.WriteLine("Прочитано: "+Database.ImportCustomers(c,"customers.json")+"; строк в БД: "+Database.Rows(c,"SELECT id FROM customers").Count);return;}
if(args.Contains("--query"))
{
    try{int index=Array.IndexOf(args,"--query");using var c=Database.Open();Console.WriteLine(JsonSerializer.Serialize(Database.Rows(c,File.ReadAllText(args[index+1])),new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));}
    catch(MySqlException e){Console.Error.WriteLine("Ошибка SQL: "+e.Message);Environment.ExitCode=1;}
    return;
}
var builder=WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("PP05_URL")??"http://127.0.0.1:5055");
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o=>{o.Cookie.Name="pp05-session";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;o.IdleTimeout=TimeSpan.FromMinutes(20);});
var app=builder.Build();app.UseSession();app.UseDefaultFiles();app.UseStaticFiles();
app.Use(async(ctx,next)=>
{
    try
    {
        if(ctx.Request.Method!="GET"&&ctx.Request.Headers.Origin.Count>0&&ctx.Request.Headers.Origin.ToString()!= $"{ctx.Request.Scheme}://{ctx.Request.Host}")
        {ctx.Response.StatusCode=403;await ctx.Response.WriteAsJsonAsync(new {message="Запрос разрешён только из этого приложения."});return;}
        await next(ctx);
    }
    catch(MySqlException e){ctx.Response.StatusCode=e.Number==1062?409:400;await ctx.Response.WriteAsJsonAsync(new {message=e.Number==1062?"Пользователь с указанным логином уже существует.":"Не удалось сохранить данные. Проверить связи, количество и запущенную БД."});}
    catch(ArgumentException e){ctx.Response.StatusCode=400;await ctx.Response.WriteAsJsonAsync(new {message=e.Message});}
    catch(NotImplementedException){ctx.Response.StatusCode=501;await ctx.Response.WriteAsJsonAsync(new {message="Учебная заготовка: выполнить шаг методики и заменить TODO."});}
    catch(Exception e){Console.Error.WriteLine(e);ctx.Response.StatusCode=500;await ctx.Response.WriteAsJsonAsync(new {message="Ошибка выполнения. Проверить журнал терминала и повторить действие."});}
});
int? Actor(HttpContext h,bool admin=false)
{
    int? id=h.Session.GetInt32("user");if(id is null)return null;
    using var c=Database.Open();var users=Database.Rows(c,"SELECT blocked,role FROM users WHERE id=@id",("@id",id));
    if(users.Count==0||Convert.ToBoolean(users[0]["blocked"])||(admin&&(string)users[0]["role"]! !="Administrator"))return null;
    return id;
}
IResult Denied()=>Results.Json(new {message="Вход или полномочия администратора обязательны."},statusCode:403);
app.MapGet("/api/info",()=>new {title=dataset.GetProperty("title").GetString(),database=Database.Name});
app.MapGet("/api/puzzle",(HttpContext h)=>
{
    int[] order=Enumerable.Range(0,4).OrderBy(_=>RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray();
    if(order.SequenceEqual(new[]{0,1,2,3}))order=new[]{1,0,2,3};
    h.Session.SetString("puzzle",JsonSerializer.Serialize(order));
    return new{pieces=order.Select((source,id)=>new{id,source}).ToArray()};
});
app.MapPost("/api/login",(HttpContext h,LoginInput input)=>
{
    string? value=h.Session.GetString("puzzle");h.Session.Remove("puzzle");
    int[] order=value is null?Array.Empty<int>():JsonSerializer.Deserialize<int[]>(value)!;
    bool puzzleOk=order.Length==4&&input.Pieces is not null&&input.Pieces.Length==4&&input.Pieces.Distinct().Count()==4&&input.Pieces.All(i=>i>=0&&i<4)&&input.Pieces.Select(i=>order[i]).SequenceEqual(new[]{0,1,2,3});
    using var c=Database.Open();var r=Users.Login(c,input,puzzleOk);
    if(r.Success){h.Session.Clear();h.Session.SetInt32("user",r.UserId!.Value);}
    return Results.Json(new {r.Success,r.Message,r.Role},statusCode:r.Success?200:401);
});
app.MapPost("/api/logout",(HttpContext h)=>{h.Session.Clear();return Results.Ok();});
app.MapGet("/api/me",(HttpContext h)=>
{var id=Actor(h);if(id is null)return Denied();using var c=Database.Open();return Results.Json(Database.Rows(c,"SELECT id,login,full_name,role FROM users WHERE id=@id",("@id",id))[0]);});
app.MapGet("/api/catalog",(HttpContext h)=>
{if(Actor(h) is null)return Denied();using var c=Database.Open();return Results.Json(new {products=Database.Rows(c,"SELECT * FROM v_product_cost"),customers=Database.Rows(c,"SELECT id,name FROM customers ORDER BY id"),materials=Database.Rows(c,"SELECT * FROM materials"),operations=Database.Rows(c,"SELECT * FROM operations")});});
app.MapGet("/api/orders",(HttpContext h)=>
{var id=Actor(h);if(id is null)return Denied();using var c=Database.Open();return Results.Json(Database.Rows(c,"SELECT v.* FROM v_order_cost v JOIN orders o ON o.id=v.id WHERE o.created_by=@id ORDER BY v.id",("@id",id)));});
app.MapPost("/api/orders",(HttpContext h,OrderInput input)=>
{var id=Actor(h);if(id is null)return Denied();using var c=Database.Open();var rows=Database.Rows(c,"CALL sp_create_order(@c,@a,@p,@q)",("@c",input.Customer), ("@a",id),("@p",input.Product),("@q",input.Quantity));return Results.Json(rows[0]);});
app.MapGet("/api/orders/{id:int}/cost",(HttpContext h,int id)=>
{var actor=Actor(h);if(actor is null)return Denied();using var c=Database.Open();if(Database.Rows(c,"SELECT id FROM orders WHERE id=@id AND created_by=@u",("@id",id),("@u",actor)).Count==0)return Denied();return Results.Json(new {total=Calculations.OrderTotal(c,id)});});
app.MapPut("/api/orders/{id:int}",(HttpContext h,int id,StatusInput input)=>
{var actor=Actor(h);if(actor is null)return Denied();using var c=Database.Open();if(Database.Rows(c,"SELECT id FROM orders WHERE id=@id AND created_by=@u",("@id",id),("@u",actor)).Count==0)return Denied();if(input.Status is not ("Новый" or "В производстве" or "Завершён"))throw new ArgumentException("Выбрать статус из списка.");Database.Exec(c,"CALL sp_change_status(@id,@s)",("@id",id),("@s",input.Status));return Results.Ok(new {message="Статус сохранён; переход записан триггером."});});
app.MapDelete("/api/orders/{id:int}",(HttpContext h,int id)=>
{var actor=Actor(h);if(actor is null)return Denied();using var c=Database.Open();Database.Exec(c,"DELETE FROM orders WHERE id=@id AND created_by=@u AND status='Новый'",("@id",id),("@u",actor));return Results.Ok(new {message="Удалён собственный новый заказ, если он найден. Завершённые заказы сохраняются."});});
app.MapGet("/api/users",(HttpContext h)=>
{if(Actor(h,true) is null)return Denied();using var c=Database.Open();return Results.Json(Database.Rows(c,"SELECT id,login,full_name,role,failed_attempts,blocked FROM users ORDER BY id"));});
app.MapPost("/api/users",(HttpContext h,UserInput input)=>
{if(Actor(h,true) is null)return Denied();using var c=Database.Open();int id=Users.Add(c,input);return Results.Json(new {id,message="Пользователь добавлен."});});
app.MapPut("/api/users/{id:int}",(HttpContext h,int id,UserInput input)=>
{var actor=Actor(h,true);if(actor is null)return Denied();if(actor==id&&(input.Blocked||input.Role!="Administrator"))throw new ArgumentException("Нельзя заблокировать или понизить собственную административную запись.");using var c=Database.Open();Users.Edit(c,id,input);return Results.Ok(new {message="Данные сохранены. При снятии блокировки счётчик сброшен."});});
app.MapPost("/api/users/{id:int}/unblock",(HttpContext h,int id)=>
{if(Actor(h,true) is null)return Denied();using var c=Database.Open();Database.Exec(c,"CALL sp_unblock(@id)",("@id",id));return Results.Ok(new {message="Блокировка снята, ошибок подряд: 0."});});
app.Run();
public record OrderInput(string Customer,int Product,int Quantity);
public record StatusInput(string Status);
